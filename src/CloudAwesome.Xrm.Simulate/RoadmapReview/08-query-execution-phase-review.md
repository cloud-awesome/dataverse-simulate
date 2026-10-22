# Query execution phase review

Date reviewed: 2026-10-22

Previous focused review: 2026-09-19

## Updated assessment

The old broad query-pipeline rework is substantially delivered. Since the September review, paging, FetchXML count/page metadata, distinct aggregate count, and aggregate-limit handling have also been implemented. They should no longer appear as entirely missing features.

Exact query parity is still partial. The next work is a focused correctness pass with live fixtures for the remaining semantic differences, followed by selective operator breadth. Use [P1/P2 for hardening and P6 for expansion](00-executive-summary.md#updated-order-of-work).

## Delivered query behavior

- [x] QueryExpression is the shared execution path for converted FetchXML and QueryByAttribute.
- [x] Base records are cloned before the query pipeline.
- [x] Base filters operate before final projection.
- [x] Base ordering can use unreturned attributes and occurs before TopCount.
- [x] Distinct compares projected attributes rather than hidden source attributes/record identity in the covered cases.
- [x] Inner and left outer joins, selected linked columns, and `AliasedValue` metadata/raw values.
- [x] Join/filter attributes need not be selected in the tested base/linked column sets.
- [x] Focused linked-column, multiple-alias, ordering, and top-level source-immutability tests.
- [x] Sum, average, min, max, count, countcolumn, grouping, date grouping, fiscal-year settings, and distinct countcolumn.
- [x] Default aggregate input limit handling and an explicit FetchXML limit path.
- [x] PageNumber/Count slicing, MoreRecords, generated cookies, and total-count/limit flags for all three query forms.
- [x] FetchXML `page`, `count`, `paging-cookie`, and `returntotalrecordcount` parse into `PagingInfo`.
- [x] Base-table read security filters before the query pipeline.
- [x] Live comparison fixtures exist for selected single-entity and linked query behavior.

## Current execution order

`EntityMultipleRetriever` obtains base-security-filtered data. `QueryExpressionParser.Parse` then applies:

1. clone base rows;
2. base filters;
3. linked rows/criteria and linked ordering;
4. aggregation;
5. base ordering;
6. projection before distinct where requested;
7. distinct;
8. TopCount;
9. final projection for non-distinct queries.

`EntityMultipleRetriever.ToEntityCollection` then materializes and slices pages, builds a cookie where more rows remain, and applies total-count metadata.

This fixes the former early-projection problem. It does not establish correct semantics for every alias filter, linked sort, aggregate output, or paging combination.

## P1 - Remaining correctness work

### P1 query input/output implementation - 22 October 2026

The first P1 implementation covers the projection and FetchXML input gaps below. The regression fixture is [QueryInputOutputSemanticsTests](../CloudAwesome.Xrm.Simulate.Test/QueryParserTests/QueryInputOutputSemanticsTests.cs); live comparisons are in [QueryInputOutputSemanticsParityTests](../CloudAwesome.Xrm.Simulate.Integration/QueryTests/QueryInputOutputSemanticsParityTests.cs), category `QuerySemantics`.

The supported contract is:

- Empty/default QueryExpression and QueryByAttribute column sets return the primary-id attribute and `Entity.Id`. Selected projections include that id automatically. Loaded metadata supplies its name; without metadata the existing `<logicalname>id` convention applies. Tables with a different primary-id name require metadata.
- Distinct compares the selected attributes and does not carry a hidden source `Id`. Selecting the primary id explicitly, including through `AllColumns`, preserves it. Live execution established that an empty base QueryExpression column set defaults to primary-id output even when distinct is requested, including when linked columns are selected; the implementation and regressions preserve each row's id in those cases.
- FetchXML without base `<attribute>` elements defaults to all base columns. `<all-attributes/>` is supported on base and linked tables. A link without selected attributes still returns no linked columns. Aggregate expressions retain their existing output rather than being mistaken for an empty projection; aggregate wrapper/identity fidelity remains a separate P1 item.
- Base and linked FetchXML filters preserve nested AND/OR structure. Multiple sibling filters are combined with AND. Literal equality values retain `%`; wildcard conversion applies only to `like`/`not-like`.
- FetchXML executes the same loaded-metadata validation as QueryExpression, including nested criteria, links, selected columns, ordering, and aggregate source attributes. Aggregate order aliases resolve to their source attributes for validation.
- Empty/malformed XML, missing required structure/attributes, and invalid condition value arity throw `ArgumentException`. Unsupported condition operators, filter types, non-aggregate attribute aliases, `entityname`/`valueof` conditions, and child `<value>` elements throw `NotSupportedException`. Metadata failures retain `SimulatedMetadataException`. Exact Dataverse fault envelopes are not claimed. `In`/`NotIn` and multiple-value execution remain P6 work.
- Query cloning isolates standard mutable SDK values (`EntityReference`, `Money`, option values/collections, `AliasedValue`, entities/related collections, and byte arrays), including repeated/cyclic entity references. Arbitrary custom attribute objects are outside this cloning contract. `uiname`/`uitype` display hints and `no-lock` have no simulated execution effect.

The four old empty, ignored projection/link TODO tests were removed; active focused projection/link fixtures cover those behaviors. Legacy tests that inspect returned names now select those names explicitly, and the invalid FetchXML operator spelling `equal` was corrected to `eq`. The live fixture's generated last name is 41 characters, within the platform's 50-character limit. Invalid-input fixtures also check query-specific fault messages so an unrelated database outage cannot pass as an expected rejection.

Verification on 2026-10-22: the updated unit suite passes **606 tests with no skips**; the four-project Release build succeeds with existing compiler warnings. The final focused live run passes **all 23 `QuerySemantics` cases**. Authentication was refreshed, fixture last names were shortened, and the empty distinct projection contract was corrected using live output. A transient Dataverse database outage interrupted an intermediate run; the final run succeeded with no cleanup errors reported. These results cover the stated scenarios, not exact parity for the wider query surface. Rerun with:

```powershell
dotnet test CloudAwesome.Xrm.Simulate.Integration/CloudAwesome.Xrm.Simulate.Integration.csproj --no-restore --configuration Release --filter "TestCategory=QuerySemantics"
```

### Projection and identity

- [x] Empty non-aggregate `ColumnSet(false)` projects only the primary id; aggregate results remain intact.
- [x] Primary-id projection uses loaded metadata or the documented conventional fallback, without matching unrelated GUID attributes.
- [x] Distinct projection removes hidden source identity for selected base columns; empty base column sets include the default primary id. Explicit-id, empty, and linked projections have passing focused live comparisons.
- [x] Standard mutable SDK attribute values and related collections are cloned, with mutation/cyclic-reference regressions.

### FetchXML parsing and validation

- [x] Recursive base/linked filter parsing preserves AND/OR and sibling filter structure.
- [x] Unknown condition operators fail explicitly instead of defaulting to `Equal`.
- [ ] `In`/`NotIn` and multiple `<value>` execution remain P6; these inputs now fail explicitly.
- [x] FetchExpression retrieval validates the converted query against loaded metadata before execution.
- [x] Empty/malformed/unsupported FetchXML has the explicit simulator input-error contract above. Exact platform fault envelopes remain open.
- [x] `<all-attributes/>` is parsed, non-aggregate aliases are rejected, and display/locking hints have documented no-op behavior. Other FetchXML breadth remains demand-driven work.

### Read security and links

- [ ] Filter linked tables by read access before joins and aggregation/count/paging. Only the base table is currently replaced with readable rows.
- [ ] Preserve left-outer semantics when a matching physical linked row is unreadable.
- [ ] Ordering on a linked attribute currently operates after linked projection; an unselected sort column can be lost. Add a live case and a regression for it.
- [ ] Check nested alias propagation and alias-referenced/post-join filter behavior; base filters currently execute before joins.

### Paging completion

Implemented: offset-style page slicing, requested count, default count 5,000, MoreRecords, a generated first/last-id cookie, and total count capped at 5,000 with an exceeded flag.

Still open:

- [ ] Incoming `PagingCookie` is parsed/stored but never consumed by `ToEntityCollection`; continuation is currently offset-based only.
- [ ] Positive requested page sizes are not capped at the platform maximum.
- [ ] Stable ordering/tie-breaking across pages and cookie continuation after data changes.
- [ ] Exact invalid page/count/cookie and TopCount/paging/count combinations.
- [ ] Correct behavior for result types/table limits that differ from the default assumptions.

Compare cookie continuation and boundary metadata against live results; do not require byte-identical cookie text unless that is the intended tested contract.

### Aggregate completion

Implemented: distinct countcolumn is active and tested; the default 50,000-row limit produces an OrganizationServiceFault when exceeded. The explicit `aggregatelimit` implementation takes at most **limit + 1** rows and aggregates that set, with a unit test asserting that behavior.

Still open:

- [ ] Compare the explicit-limit path with live behavior, including validation of negative/zero/oversized limits and fault behavior. Having a unit test for truncation does not establish platform fidelity.
- [ ] Return the correct SDK result shapes: `Aggregates` currently emits raw alias values and random entity ids rather than establishing Dataverse aggregate `AliasedValue`/identity conventions.
- [ ] Verify numeric types, money/reference/option wrapping, empty input, all-null groups, and empty count behavior.
- [ ] Verify timezone/user-local date grouping, fiscal settings, joins, and aggregate input-limit counting.
- [ ] Validate aggregate expressions/aliases against loaded metadata without treating aliases as table columns.

## P2 - Parity fixtures to add

The integration project currently has 7 single-entity and 9 linked-query tests. They cover selected projection/filter/order/distinct/alias cases and count shape; they are not a multi-page, aggregate, or secured-join suite.

Add the P1 cases in this order:

1. Empty/selected column projection and output identity.
2. OR/nested FetchXML and metadata validation/rejection.
3. Secure inner/outer links and ordering on unselected linked attributes.
4. Two-page/cookie continuation and count limits across query forms.
5. Aggregate output types, distinct/empty input, and limit behavior.

Live fixtures were not executed during this review. Retain small deterministic datasets, exact contract assertions, and cleanup through the existing harness.

## P6 - Targeted query breadth

- [ ] `In`/`NotIn` and FetchXML multiple values first, as common query scenarios.
- [ ] User/team/business-unit/hierarchy operators, using the existing security graph where appropriate.
- [ ] Fiscal period/year conditions, multi-select contains-values, and mask operators.
- [ ] Wider lookup/money/option/bool/date and null semantics across handlers.
- [ ] `Exists`, `In`, `MatchFirstRowUsingCrossApply`, any/all joins, and more advanced alias cases.
- [ ] FetchXML-specific features that cannot be represented faithfully in the current conversion, only as demand requires.

Unsupported QueryExpression operators already fail rather than silently execute: condition handlers throw `NotImplementedException`, and unsupported join operators throw `NotSupportedException`. Align and document unsupported behavior rather than promising every SDK enum value.

## Verification and stale test cleanup

The unit command on 2026-10-22 was:

```powershell
dotnet test 'CloudAwesome.Xrm.Simulate.Test/CloudAwesome.Xrm.Simulate.Test.csproj' --no-restore
```

Review baseline result: **560 passed, 4 skipped, 0 failed**. The subsequent P1 implementation result is **606 passed, 0 skipped, 0 failed**.

The ignored distinct aggregate count test from the September review is active and passing. The four empty TODOs for base filter/link projection in QueryExpression and FetchXML were removed during the P1 input/output implementation; active focused tests cover those scenarios.

The older 543-passed/5-skipped result described the September baseline and is superseded by this run. The query phase can be described as a completed foundation with explicit P1 hardening, not either wholly unimplemented or fully live-compatible.

## Evidence anchors

- [Execution pipeline](../CloudAwesome.Xrm.Simulate/QueryParsers/QueryExpressionParser.cs)
- [Paging and base security filtering](../CloudAwesome.Xrm.Simulate/ServiceRequests/EntityMultipleRetriever.cs)
- [FetchXML conversion](../CloudAwesome.Xrm.Simulate/QueryParsers/FetchExpressionParser.cs)
- [Projection/cloning](../CloudAwesome.Xrm.Simulate/QueryParsers/EntityCloner.cs)
- [Linked rows and ordering](../CloudAwesome.Xrm.Simulate/QueryParsers/LinkedEntities.cs)
- [Aggregate output/limits](../CloudAwesome.Xrm.Simulate/QueryParsers/Aggregates.cs)
- [Distinct count and limit tests](../CloudAwesome.Xrm.Simulate.Test/QueryParserTests/FetchExpressionAggregateTests.cs)
