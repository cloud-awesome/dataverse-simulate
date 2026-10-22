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

### Projection and identity

- [ ] `EntityCloner.Project` currently treats an empty column list like all columns. A non-aggregate `ColumnSet(false)` therefore returns every stored attribute. Capture the intended primary-id-only contract and distinguish it from aggregate-expression projection.
- [ ] Primary-id inclusion currently finds matching GUID attributes whose names end in `id`, rather than using a definitive primary-id definition. Avoid accidentally exposing another id-valued attribute and use metadata where available.
- [ ] Distinct output still retains a source entity's `Id` through cloning/projection. Verify identity/output shape against live results, including empty projections.
- [ ] Cloning copies SDK attribute values/related collections shallowly. Current top-level immutability tests do not prove isolation when a returned `EntityReference`, `Money`, option collection, or related entity is mutated.

### FetchXML parsing and validation

- [ ] `ParseFilter` ignores `type='or'` and child filters, so nested/OR semantics are not implemented. Preserve logical structure rather than silently evaluating only direct conditions as AND.
- [ ] Unknown condition operators currently default to `Equal`; reject them clearly or reproduce a captured platform fault.
- [ ] Multiple `<value>` elements are not parsed, and `In`/`NotIn` have no condition handlers yet.
- [ ] The FetchExpression retrieval path converts and executes without `MetadataValidator.ValidateQuery`; loaded metadata is bypassed for that path.
- [ ] Empty/malformed/unsupported FetchXML currently returns empty results or leaks parser exceptions on some paths. Define supported input/fault behavior explicitly.
- [ ] Validate `<all-attributes/>`, aliases, `uiname`/`uitype`, `no-lock`, and other constructs before describing them as supported; expand or reject as needed.

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

Result: **560 passed, 4 skipped, 0 failed**.

The ignored distinct aggregate count test from the September review is now active and passing. The four remaining skips are empty TODOs for base filter/link projection in QueryExpression and FetchXML; newer focused tests cover the core scenarios. Reconcile/remove those placeholders and stale comments in a later code change. They were deliberately left unchanged during this documentation-only review.

The older 543-passed/5-skipped result described the September baseline and is superseded by this run. The query phase can be described as a completed foundation with explicit P1 hardening, not either wholly unimplemented or fully live-compatible.

## Evidence anchors

- [Execution pipeline](../CloudAwesome.Xrm.Simulate/QueryParsers/QueryExpressionParser.cs)
- [Paging and base security filtering](../CloudAwesome.Xrm.Simulate/ServiceRequests/EntityMultipleRetriever.cs)
- [FetchXML conversion](../CloudAwesome.Xrm.Simulate/QueryParsers/FetchExpressionParser.cs)
- [Projection/cloning](../CloudAwesome.Xrm.Simulate/QueryParsers/EntityCloner.cs)
- [Linked rows and ordering](../CloudAwesome.Xrm.Simulate/QueryParsers/LinkedEntities.cs)
- [Aggregate output/limits](../CloudAwesome.Xrm.Simulate/QueryParsers/Aggregates.cs)
- [Distinct count and limit tests](../CloudAwesome.Xrm.Simulate.Test/QueryParserTests/FetchExpressionAggregateTests.cs)
