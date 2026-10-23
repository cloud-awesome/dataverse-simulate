# Query execution phase review

Date reviewed: 2026-10-22; implementation updates: 2026-10-22/23

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
- [x] Base and linked read security filters before the query pipeline.
- [x] Live comparison fixtures exist for selected single-entity and linked query behavior.

## Current execution order

`EntityMultipleRetriever` obtains base/linked security-filtered data. The service's `QueryExpressionParser.Parse` path then applies:

1. clone base rows;
2. base filters;
3. linked rows/criteria and linked ordering;
4. aggregation;
5. base ordering;
6. projection before distinct where requested;
7. distinct;
8. TopCount;
9. `QueryPaging` continuation/page slicing and total-count metadata using unprojected ordering values;
10. final projection for non-distinct queries.

`QueryPaging.ToCollection` packages the projected page and its previously calculated metadata. Nonempty supported base-query pages include a cookie even on the last page; empty pages do not. Public parser overloads retain their existing unpaged pipeline contract.

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

- [x] Filter base and linked tables by read access before joins and aggregation/count/paging, including nested/self links (23 October security pass).
- [x] Preserve left-outer rows without exposing unreadable matches. Unit regressions and all five configured restricted-user live join/count/aggregate cases pass. See [security verification](06-security-model.md#p1-implementation-and-verification---23-october-2026).
- [ ] Ordering on a linked attribute currently operates after linked projection; an unselected sort column can be lost. Add a live case and a regression for it.
- [ ] Check nested alias propagation and alias-referenced/post-join filter behavior; base filters currently execute before joins.

### Paging completion

Implemented on 23 October: simple pages and value-based cookie continuation across QueryExpression, QueryByAttribute, and FetchXML for non-distinct base-row queries; default/capped standard-table page size 5,000; final-page cookies; MoreRecords and total-count flags; selected exact input faults. See the implementation contract below.

Still open:

- [x] Consume incoming simulator cookies using ordering values, including when the boundary row is deleted or no longer matches the filter.
- [x] Cap standard-table pages at 5,000; zero/default counts use that size, and negative SDK counts also use the default as observed live.
- [x] Preserve unselected ordering values for continuation and use the primary id as a deterministic base-query tie-breaker, including metadata-backed nonconventional primary-id names.
- [x] Match live negative-page and TopCount/paging/total-count conflict codes/messages; reject malformed/incompatible simulator cookies and malformed integer FetchXML options explicitly.
- [ ] Expand cookie support beyond base rows. Linked, distinct, and aggregate/grouped queries currently use simple paging and return no simulator cookie; supplying a cookie for those shapes is explicitly unsupported.
- [ ] Elastic-table 500-row limits, complete SQL collation/choice-label/lookup-name ordering, and remaining invalid-cookie/option fault envelopes need targeted evidence and implementation.

Compare cookie continuation and boundary metadata against live results; do not require byte-identical cookie text unless that is the intended tested contract.

### Aggregate completion

Implemented: SDK alias wrappers/source metadata, empty result ids, typed numeric output, verified empty/grouped behavior, distinct countcolumn, and aggregate limits. The default 50,000-row limit faults when exceeded. Explicit `aggregatelimit` intentionally takes at most **limit + 1** arbitrary rows, confirmed by live comparisons and Microsoft documentation.

Still open:

- [x] Compare explicit-limit input boundaries live and match the exact negative/zero/oversized-limit fault code/message. Tests compare counts rather than assuming which arbitrary rows are selected.
- [x] Return `AliasedValue` values with source entity/attribute names and `Guid.Empty` aggregate ids.
- [x] Verify integer, Money, Double, and Decimal aggregate output, integer average truncation, empty input, null groups, distinct/non-null counts, and empty count output live. Int64 numeric output and grouped SDK Money/option/reference isolation also have unit coverage; broader live wrapper coverage remains open.
- [ ] Verify timezone/user-local date grouping, fiscal settings, joins, and aggregate input-limit counting.
- [x] Preserve source-attribute and order-alias metadata validation; use loaded numeric attribute types for empty-table aggregates, and reject duplicate aggregate/group aliases rather than overwriting output.

### P1 paging and aggregates implementation - 23 October 2026

The new [regression fixture](../CloudAwesome.Xrm.Simulate.Test/QueryParserTests/PagingAggregateRegressionTests.cs) adds **121 cases**, taking the full unit suite from **669 to 790 passed**, with no failures or skips. Existing aggregate tests now inspect SDK alias values instead of raw scalars; the final-page cookie expectation was corrected from live output. The earlier security test that combined TopCount with paging now tests those supported forms separately.

The [live fixture](../CloudAwesome.Xrm.Simulate.Integration/QueryTests/PagingAggregateParityTests.cs), category `PagingAggregates`, contains **36 cases**, all passing against the configured sandbox. It covers four-page walks, cookies/simple paging and default counts across query forms; continuation after inserting an earlier row and removing the boundary from the matching set; aggregate source types and identity; ungrouped/grouped empty input; a null group; distinct counts; explicit-limit boundaries; and selected invalid input codes/messages. Tests use transient accounts with cleanup and copy actual SDK seed values where currency/exchange-rate defaults matter. No 5,001/50,001-row live dataset was created: large page/count/aggregate boundaries have unit coverage and documented platform evidence.

Final verification: the four-project Release build succeeded with existing compiler warnings; the Release unit suite passed **790/790** with no skips. The combined live Release filter `TestCategory=PagingAggregates|TestCategory=QuerySemantics|TestCategory=SecurityIdentity` passed **71/71**, with no failures or skips, rechecking the earlier query and restricted-security contracts alongside this pass. Full integration discovery found **111 cases**; the remaining original 40 were not rerun by that combined filter.

Supported contract:

- Standard-table pages default to and are capped at **5,000**. Total counts are calculated before continuation/page slicing, capped at 5,000, and flagged when exceeded; unrequested totals remain `-1`. Simple paging is limited to 50,000 rows. This is not an elastic-table implementation.
- Base-query cookies store the last ordering values plus the primary id, so unselected sort columns, descending/null values, deleted anchors, and earlier inserts/removals do not degrade to offset continuation. Security and filters run again on every request. This is dynamic paging, not a snapshot: changing a row's ordering values can move that row relative to the boundary.
- Cookies use an opaque, versioned simulator XML format and must be passed unchanged with the next page number. Their bytes are not Dataverse cookies and cannot be interchanged with a live environment. Entity/order/page mismatches, malformed XML, DTDs, and unsupported key types fail explicitly. Linked/distinct/grouped shapes fall back to simple paging and return no cookie. Supplying a cookie for those shapes throws `NotSupportedException`.
- QueryExpression/QueryByAttribute counts of zero or less use the default page size; page zero means the first page. Negative page numbers fault with code `-2147220989` and `A non valid page number was received: <n>`. TopCount with explicit paging or total-count requests faults with `-2147220891` and `The Top.Count = <n> can't be specified with pagingInfo`. Other unsupported/malformed input contracts remain explicit simulator exceptions unless a platform fault has been captured.
- Aggregate rows have `Guid.Empty` ids, and result/group values are `AliasedValue` with the source entity/attribute names. Integer sum/min/max/average remain integers; integer averages truncate toward zero. Money retains its wrapper; Double and Decimal retain their types. Counts are Int32. Int64 preservation has local coverage rather than a live BigInt fixture.
- Ungrouped empty input returns one row: counts and integer numeric aggregates are zero; empty Money/Double/Decimal aggregates have an alias whose value is null. Grouped empty input returns no rows. A null group exists but omits its group alias. Metadata supplies numeric types when no typed seed values exist; otherwise seeded SDK value types are used, with an integer fallback when no type evidence exists.
- The default aggregate input limit remains 50,000 and faults with `-2147164125` above that threshold. Explicit `aggregatelimit` is in 1..50,000; invalid values fault with `-2147220989` and `Expected value between 1 and 50000 inclusive.` It aggregates up to `limit + 1` arbitrary matching rows, so tests must not assume which rows are sampled. Numeric overflow, currency conversion across multiple currencies, user-local/timezone and fiscal grouping, and complete group/linked-aggregate/cookie fidelity remain explicit follow-up work.

Platform references: [QueryExpression paging](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/queryexpression/page-results), [FetchXML paging](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/fetchxml/page-results), and [aggregate data/limits](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/fetchxml/aggregate-data). These support the documented subset; passing this fixture does not establish exact parity for every query shape.

Rerun the focused fixture with:

```powershell
dotnet test CloudAwesome.Xrm.Simulate.Integration/CloudAwesome.Xrm.Simulate.Integration.csproj --no-restore --configuration Release --filter "TestCategory=PagingAggregates"
```

It uses the existing `DATAVERSE_APPSETTINGS` JSON-path/connection mechanism. No persistent environment settings, roles, credentials, or external schema were changed.

## P2 - Parity fixtures to add

The original 7 single-entity and 9 linked-query tests are now accompanied by the query input/output, restricted security, and 36 paging/aggregate cases. The integration project contains **111 cases** in total after this pass. Wider cookie shapes, large live limits, and timezone/currency behavior remain follow-up fixtures.

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
