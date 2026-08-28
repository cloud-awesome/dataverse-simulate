# QueryExpression Linked Entity Fidelity Review

## Scope

This review was prompted by `LinkedEntityTests.LinkedEntity_Should_Return_AliasedValue`, which models a real consumer pattern:

```csharp
if (e.Contains(roleNameAddress) && e[roleNameAddress] is AliasedValue aliasedValue)
{
    roleName = aliasedValue.Value?.ToString();
}
```

The test currently fails because `RetrieveMultiple(QueryExpression)` returns `roleAlias.name` as a raw `string` rather than an `AliasedValue`.

## Current Failure

Command run:

```powershell
dotnet test 'CloudAwesome.Xrm.Simulate.Test/CloudAwesome.Xrm.Simulate.Test.csproj' --no-restore --filter FullyQualifiedName~LinkedEntityTests --verbosity minimal
```

Result:

```text
Failed LinkedEntity_Should_Return_AliasedValue
Expected type to be Microsoft.Xrm.Sdk.AliasedValue, but found System.String.
CloudAwesome.Xrm.Simulate.Test\QueryParserTests\LinkedEntityTests.cs:82
```

The query result proves that the join is finding the related role row, but the result materialization shape does not match Dataverse.

## Existing Implementation Observations

`QueryExpressionParser.Parse` currently applies the pipeline in this order:

1. Base entity criteria.
2. Base column projection.
3. Linked entity merge.
4. Aggregates.
5. Order.
6. Distinct.
7. Top count.

Relevant code:

- `CloudAwesome.Xrm.Simulate/QueryParsers/QueryExpressionParser.cs`
- `CloudAwesome.Xrm.Simulate/QueryParsers/LinkedEntities.cs`
- `CloudAwesome.Xrm.Simulate/QueryParsers/Columns.cs`
- `CloudAwesome.Xrm.Simulate/QueryParsers/FetchExpressionParser.cs`

The key implementation gaps are:

1. `LinkedEntities.MergeEntities` copies linked attributes directly into the base entity attribute bag. It should wrap projected linked columns in `AliasedValue`.
2. `LinkedEntities.MergeEntities` mutates the base `Entity` instance in place. Because the in-memory store returns the same instances that tests inserted, linked query results can leak merged attributes back into stored records.
3. `LinkedEntities.JoinEntities` only performs inner joins. `JoinOperator.LeftOuter` and FetchXML `link-type="outer"` are parsed or present in SDK objects but are not respected.
4. `LinkEntity.Columns` is ignored. All attributes on the linked row are copied, not only the selected linked columns.
5. Base projection is applied before joins. That forces join attributes to be present in the base `ColumnSet`, which is already called out in ignored tests and in code comments. Dataverse can filter and join using attributes that are not returned.
6. `LinkEntity.LinkCriteria` is evaluated only against linked records before the join. That is a useful start, but the implementation does not distinguish inner join filtering from left outer join behavior.
7. Nested link entities are applied to linked records by mutating those records before the parent join. That makes aliasing and projection fragile for nested links.
8. Missing join keys are accessed through `Attributes[...]`, so absent attributes throw `KeyNotFoundException` rather than behaving like a non-match or null comparison.
9. Ordering currently reads direct attribute keys only. It does not fully model linked entity order expressions, alias-based order expressions, or `AliasedValue.Value` comparisons.
10. FetchXML conversion creates `LinkEntity` objects, but does not map `link-type` to `JoinOperator`. QueryExpression fidelity improvements should be shared by FetchXML rather than duplicated.

## Expected Dataverse Shape For The Prompted Test

For the current test query:

- Base entity: `teamroles`
- Link target: `role`
- Alias: `roleAlias`
- Linked column: `name`
- Result attribute key: `roleAlias.name`
- Result attribute value type: `AliasedValue`
- `AliasedValue.EntityLogicalName`: `role`
- `AliasedValue.AttributeLogicalName`: `name`
- `AliasedValue.Value`: `"Basic User"`

The consuming code should pass because `e.Contains("roleAlias.name")` is true and `e["roleAlias.name"] is AliasedValue` is true.

## Recommended Implementation Plan

### Phase 1: Lock The Immediate Contract

Add focused tests around the current scenario before changing production code:

1. Linked QueryExpression columns are returned as `AliasedValue`.
2. `AliasedValue.EntityLogicalName`, `AttributeLogicalName`, and `Value` are populated correctly.
3. The raw value type is preserved for common Dataverse SDK types: `string`, `Guid`, `EntityReference`, `OptionSetValue`, `Money`, `bool`, and `DateTime`.
4. Only requested linked columns are returned when `LinkEntity.Columns` is explicit.
5. The base result does not include unrequested linked columns.

Then update `LinkedEntities.MergeEntities` to materialize projected linked attributes as:

```csharp
result[$"{alias}.{attributeName}"] =
    new AliasedValue(linkedEntityLogicalName, attributeName, rawValue);
```

This phase should be small, but it should not simply wrap every merged value. It should also pass the linked entity logical name and selected `LinkEntity.Columns` into the merge step.

### Phase 2: Stop Mutating Stored Entities

Introduce result cloning before any query pipeline stage mutates or projects data.

Required behavior:

1. Each returned `Entity` should be a new instance with the same logical name and ID.
2. Attributes and formatted values should be copied into the returned instance.
3. Query-time aliases should only exist on returned rows, not on records stored in `MockedEntityDataService`.

Add a regression test that runs a linked query, then retrieves the base row from the simulated data store and verifies that `roleAlias.name` was not persisted into the stored `teamroles` entity.

### Phase 3: Reorder The Query Pipeline

Change the internal pipeline so filtering and joining operate over source rows before final projection.

Recommended order:

1. Start with cloned source rows.
2. Apply base criteria using source attributes.
3. Apply link processing, including link criteria and join semantics.
4. Apply aggregates if aggregate expressions are present.
5. Apply ordering.
6. Apply distinct.
7. Apply top count and paging constraints.
8. Apply final base and linked column projection.

The important change is that `ColumnSet` should be a final result-shaping step for non-aggregate queries, not an early data-removal step.

### Phase 4: Implement Join Semantics Deliberately

Support these first:

1. `JoinOperator.Inner`
2. `JoinOperator.LeftOuter`

For `Inner`, a base row appears once for each matching linked row.

For `LeftOuter`, a base row appears even when no linked row matches. If no linked row matches, no aliased attributes should be added for that link.

Handle key comparison centrally:

1. `EntityReference` compares by `Id`.
2. Nullable values compare by their underlying value.
3. Missing or null keys do not match non-null keys.
4. Missing linked entity collections produce no inner matches and preserve rows for left outer joins.

Defer advanced operators such as `Exists`, `In`, `MatchFirstRowUsingCrossApply`, and natural joins unless real consumers need them. Mark unsupported join operators with a clear `NotSupportedException` rather than silently returning incorrect results.

### Phase 5: Make Linked Projection Explicit

`LinkEntity.Columns` should control which linked attributes become aliased output attributes.

Expected rules:

1. `new ColumnSet("name")` returns only `alias.name`.
2. `new ColumnSet(false)` returns no linked attributes, while still allowing the join to filter base rows.
3. `new ColumnSet(true)` returns all populated attributes from the linked row as aliased values.
4. Primary ID attributes should only be included if selected or if `AllColumns` is true.

Alias behavior to confirm with an integration test before freezing:

1. Explicit aliases use `alias.attribute`.
2. Missing aliases should match Dataverse's generated alias behavior or be documented if the simulator intentionally requires explicit aliases.

### Phase 6: Cover FetchXML As A Consumer Of QueryExpression

Because `FetchExpressionParser.Parse` converts FetchXML into a `QueryExpression` and then calls `QueryExpressionParser.Parse`, most runtime fixes should be shared.

FetchXML-specific work:

1. Parse `link-type="outer"` into `JoinOperator.LeftOuter`.
2. Preserve `link-type="inner"` as `JoinOperator.Inner`.
3. Add FetchXML tests proving linked attributes are returned as `AliasedValue`.
4. Keep existing FetchXML tests passing, but update assertions that currently expect raw linked strings.

### Phase 7: Ordering, Distinct, Aggregates, And Paging

After linked values become `AliasedValue`, later pipeline stages must unwrap them where appropriate.

Required checks:

1. Ordering by an aliased linked attribute should compare `AliasedValue.Value`.
2. Distinct should compare aliased values by logical name, attribute name, and value, not by object reference.
3. Aggregates should either explicitly support aliased input values or reject unsupported linked aggregate scenarios with a clear exception.
4. `EntityMultipleRetriever` enumerates `results` multiple times when setting total record counts. Materialize once after parsing to avoid repeated query execution after the parser becomes more complex.
5. QueryExpression `PageInfo.PageNumber`, `Count`, and `PagingCookie` are currently not implemented; document or implement separately from the AliasedValue fix.

## Suggested Test Matrix

Minimum tests for this change:

1. `LinkedEntity_Should_Return_AliasedValue`
2. `LinkedEntity_Should_Preserve_AliasedValue_Metadata`
3. `LinkedEntity_Should_Only_Return_Selected_Linked_Columns`
4. `LinkedEntity_Should_Not_Mutate_Stored_Base_Entity`
5. `LinkedEntity_Should_Not_Require_Join_Column_In_Base_ColumnSet`
6. `LinkedEntity_Should_Not_Require_Linked_Filter_Column_In_Linked_ColumnSet`
7. `LinkedEntity_LeftOuter_Should_Return_Base_Row_When_No_Linked_Record_Matches`
8. `LinkedEntity_Inner_Should_Exclude_Base_Row_When_No_Linked_Record_Matches`
9. `FetchXml_LinkedEntity_Should_Return_AliasedValue`
10. `FetchXml_Outer_Link_Should_Map_To_LeftOuter_Join`

Useful second wave tests:

1. Nested linked entity aliases.
2. Multiple linked records create multiple result rows for inner joins.
3. Multiple links on one base entity do not overwrite each other.
4. Linked `OptionSetValue`, `Money`, `EntityReference`, and `DateTime` values preserve SDK types.
5. Ordering by `alias.attribute` works after aliased values are introduced.

## Recommended Code Shape

The current static parser stages are small, so a full rewrite is not required immediately. The safest next step is a targeted refactor around linked entity processing:

1. Add an internal row/result helper that clones entities and can add aliased attributes.
2. Change `LinkedEntities.Apply` to return new result rows instead of mutating input rows.
3. Pass the whole `LinkEntity` object into join/merge helpers instead of passing individual strings.
4. Add a projection helper for linked columns.
5. Move base `Columns.Apply` to the end of the non-aggregate query path, or replace it with a final projector that preserves aliased attributes.

The final projector should keep:

1. Requested base columns from `query.ColumnSet`.
2. Any aliased linked attributes created by link processing.
3. The base `Id` and logical name.

## Risks

1. Existing tests currently assert raw linked values in FetchXML linked queries. Those tests should be updated because the existing expectation is lower fidelity than Dataverse.
2. Moving projection later can expose bugs in condition handlers that currently assume an attribute exists. Those handlers should treat missing attributes consistently.
3. Entity equality and distinct behavior will change once linked values are `AliasedValue` instances. `EntityComparer` should be updated in the same change set or covered by tests.
4. Aggregate support is already partially implemented and has its own alias semantics. Keep aggregate aliases separate from linked entity aliases to avoid mixing two different output shapes.

## Recommendation

Treat `AliasedValue` support as the first QueryExpression fidelity milestone, not as an isolated hotfix. The initial implementation can remain small if it is scoped to explicit aliases, selected columns, inner joins, and left outer joins, but it should establish the correct output model and avoid mutating stored entities. Once that foundation is in place, FetchXML linked-entity behavior will improve through the existing conversion path.
