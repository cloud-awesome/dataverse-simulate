# Query execution phase review

Date reviewed: 2026-09-19

## Summary

Query execution is no longer a major Phase 1 blocker. Since the original roadmap was written, filtering, projection ordering, distinct handling, top count, selected-column behavior, basic FetchXML conversion, common link semantics, and basic aggregation have all received substantial implementation and test coverage.

The original roadmap item to "rework query execution so filtering, joining, ordering, distinct, paging, projection, and aggregation match Dataverse behavior instead of LINQ convenience behavior" should now be treated as mostly complete, with a smaller query maturity tail remaining.

## Already substantially covered

- `QueryExpression`, `FetchExpression`, and `QueryByAttribute` all flow through query execution.
- Base filtering now runs before final projection.
- Ordering can use attributes that are not returned in the final `ColumnSet`.
- `TopCount` is applied after ordering.
- `Distinct` compares projected output values rather than hidden source attributes or record identity.
- Linked entities support inner and left outer joins.
- Linked entity attributes are returned as `AliasedValue` instances.
- Linked entity projection, filtering, ordering, multiple aliases, and source-entity immutability have focused tests.
- Basic aggregate support exists for `sum`, `avg`, `min`, `max`, `count`, and `countcolumn`.
- Group-by, date grouping, fiscal-year grouping, and fiscal settings are covered by tests.
- Live parity fixtures now exist for selected single-entity query behavior and linked entity behavior.

## Remaining Phase 1 query work

### Real paging

This is the most obvious remaining gap. `QueryExpression` handling currently caps results at 5000 and partly shapes `TotalRecordCount`, but does not implement:

- `PageInfo.PageNumber`
- `PageInfo.Count`
- `PagingCookie`
- `MoreRecords`
- page slicing semantics

FetchXML and `QueryByAttribute` also do not currently get equivalent `EntityCollection` metadata handling.

Recommendation: prioritize this as the main remaining query execution item before closing Phase 1 query work.

### Aggregate completion

Basic aggregation is in place, but the following remain:

- distinct aggregate count
- aggregate record limits
- explicit aggregate-limit behavior and error shape

There is already an ignored test for distinct aggregate count and TODO notes for aggregate limits.

Recommendation: keep this in the query backlog, but it is less urgent than paging unless users are relying heavily on aggregate FetchXML.

## Better suited to Phase 2 or later

### FetchXML breadth

FetchXML is currently parsed into `QueryExpression`, which is pragmatic and now covers common cases. Mature parity still needs FetchXML-specific behavior:

- multiple `<value>` nodes
- wider operator mapping
- `returntotalrecordcount`
- FetchXML `page`, `count`, and `paging-cookie`
- `uiname` and `uitype` lookup behavior
- stricter invalid FetchXML exception behavior

One risky current behavior is that unknown FetchXML operators default to `Equal`. That should eventually fail clearly or match Dataverse's exception shape.

### Condition operator tail

The common operators are implemented, including many date-relative operators. Remaining operators are mostly advanced Dataverse semantics:

- `In` and `NotIn`
- current user, team, and business-unit scoped operators
- child business-unit and hierarchy operators
- fiscal period and fiscal year operators
- multi-select option set operators
- mask operators

Recommendation: implement these incrementally as parity fixtures or real user scenarios demand them.

### Advanced link operators

Inner and left outer links are covered well enough for Phase 1. Remaining link work is advanced:

- `Exists`
- `In`
- `MatchFirstRowUsingCrossApply`
- any/all style operators available in the SDK version
- alias-reference edge cases

Recommendation: treat these as Phase 2 query maturity unless a specific user scenario requires them sooner.

## Roadmap implication

For Phase 1, query execution should probably be reframed from a major rework to:

- complete paging behavior
- finish or explicitly defer aggregate distinct and limits
- clean up stale TODO comments/tests that still describe older projection/link limitations
- add a small number of parity fixtures for the remaining high-risk query paths

The deeper metadata-aware validation, relationship-aware behavior, and advanced FetchXML/link operator work fits better with Phase 2 platform maturity.

## Verification note

At the time of this review, the unit test suite was run with:

```powershell
dotnet test 'CloudAwesome.Xrm.Simulate.Test/CloudAwesome.Xrm.Simulate.Test.csproj' --no-restore
```

Result:

- 543 passed
- 5 skipped
- 0 failed

The skipped tests are query-related TODOs, mainly distinct aggregate count and two older projection/link TODOs that appear to have been superseded by newer tests.
