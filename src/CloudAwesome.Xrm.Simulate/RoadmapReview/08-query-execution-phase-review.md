# Query execution phase review

Date reviewed: 2026-10-28; implementation updates: 2026-10-22/23/28

Previous focused review: 2026-09-19

## Updated assessment

The old broad query-pipeline rework is substantially delivered. Since the September review, paging, FetchXML count/page metadata, distinct aggregate count, and aggregate-limit handling have also been implemented. They should no longer appear as entirely missing features.

Exact query parity remains scoped. The next work is repeatable live execution and deeper evidence for the implemented subset, followed by selective operator/alias breadth. Use [P2 for evidence and P6 for expansion](00-executive-summary.md#updated-order-of-work).

The [28 October close-out implementation](09-p1-closeout-review.md) closes the two final query P1 defects: unselected linked ordering now uses hidden execution keys, and qualified inputs/column comparisons fail explicitly before execution. The Release unit suite passes 1,043 cases; 21 new focused live cases passed, with the full live regression result tracked in document 09. Wider qualified/post-join execution remains unsupported.

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

`EntityMultipleRetriever` validates query input flags and metadata before obtaining base/linked security-filtered data. The service's `QueryExpressionParser.Parse` path also validates unsupported input forms, then applies:

1. clone base rows;
2. base filters;
3. linked rows/criteria and linked ordering using hidden keys, removed before projection/distinct;
4. aggregation;
5. base ordering;
6. projection before distinct where requested;
7. distinct;
8. TopCount;
9. `QueryPaging` continuation/page slicing and total-count metadata using unprojected ordering values;
10. final projection for non-distinct queries.

`QueryPaging.ToCollection` packages the projected page and its previously calculated metadata. Nonempty supported base-query pages include a cookie even on the last page; empty pages do not. Public parser overloads retain their existing unpaged pipeline contract.

This fixes the former early-projection problem. It does not establish correct semantics for every alias filter, linked sort, aggregate output, or paging combination.

## P1 - Delivered correctness contracts

### P1 query input/output implementation - 22 October 2026

The first P1 implementation covers the projection and FetchXML input gaps below. The regression fixture is [QueryInputOutputSemanticsTests](../CloudAwesome.Xrm.Simulate.Test/QueryParserTests/QueryInputOutputSemanticsTests.cs); live comparisons are in [QueryInputOutputSemanticsParityTests](../CloudAwesome.Xrm.Simulate.Integration/QueryTests/QueryInputOutputSemanticsParityTests.cs), category `QuerySemantics`.

The supported contract is:

- Empty/default QueryExpression and QueryByAttribute column sets return the primary-id attribute and `Entity.Id`. Selected projections include that id automatically. Loaded metadata supplies its name; without metadata the existing `<logicalname>id` convention applies. Tables with a different primary-id name require metadata.
- Distinct compares the selected attributes and does not carry a hidden source `Id`. Selecting the primary id explicitly, including through `AllColumns`, preserves it. Live execution established that an empty base QueryExpression column set defaults to primary-id output even when distinct is requested, including when linked columns are selected; the implementation and regressions preserve each row's id in those cases.
- FetchXML without base `<attribute>` elements defaults to all base columns. `<all-attributes/>` is supported on base and linked tables. A link without selected attributes still returns no linked columns. Aggregate expressions retain their output rather than being mistaken for an empty projection; the subsequent 23 October pass corrected their wrappers/identity as described below.
- Base and linked FetchXML filters preserve nested AND/OR structure. Multiple sibling filters are combined with AND. Literal equality values retain `%`; wildcard conversion applies only to `like`/`not-like`.
- FetchXML executes the same loaded-metadata validation as QueryExpression, including nested criteria, links, selected columns, ordering, and aggregate source attributes. Aggregate order aliases resolve to their source attributes for validation.
- Empty/malformed XML, missing required structure/attributes, and invalid condition value arity throw `ArgumentException`. Unsupported condition operators, filter types, non-aggregate attribute aliases, `entityname`/`valueof` conditions, and child `<value>` elements throw `NotSupportedException`. Metadata failures retain `SimulatedMetadataException`. Exact Dataverse fault envelopes are not claimed. `In`/`NotIn` and multiple-value execution remain P6 work.
- Query cloning isolates standard mutable SDK values (`EntityReference`, `Money`, option values/collections, `AliasedValue`, entities/related collections, and byte arrays), including repeated/cyclic entity references. The [28 October follow-up](09-p1-closeout-review.md) applies the same cloning contract to ordinary Create/Update service inputs/commits and Retrieve results. Arbitrary custom attribute objects remain outside the cloning contract. `uiname`/`uitype` display hints and `no-lock` have no simulated execution effect.

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
- [x] P1: preserve unselected linked sort values through joins, apply linked order precedence, and remove hidden keys before projection/distinct/output. Direct/FetchXML ascending/descending/top/page/count cases have focused live comparisons; unit cases also cover null/outer rows, base precedence, distinct, nested/sibling links and store immutability (28 October).
- [x] P1: reject `ConditionExpression.EntityName`, `OrderExpression.EntityName`, `CompareColumns`, dotted input attribute names and `AnyAllFilterLinkEntity` recursively before execution/auditing, including empty data and metadata-enabled queries. FetchXML qualified orders also fail explicitly. Live comparisons prove qualified conditions/orders and column comparisons can succeed on Dataverse while the simulator deliberately throws `NotSupportedException`; unqualified inner-link filters remain supported (28 October).
- [ ] P2/P6: establish wider nested alias propagation, post-join behavior, and advanced query support after the P1 explicit-rejection/correctness boundary is in place.

### Paging completion

Implemented on 23 October: simple pages and value-based cookie continuation across QueryExpression, QueryByAttribute, and FetchXML for non-distinct base-row queries; default/capped standard-table page size 5,000; final-page cookies; MoreRecords and total-count flags; selected exact input faults. See the implementation contract below.

Paging requirement status:

- [x] Consume incoming simulator cookies using ordering values, including when the boundary row is deleted or no longer matches the filter.
- [x] Cap standard-table pages at 5,000; zero/default counts use that size, and negative SDK counts also use the default as observed live.
- [x] Preserve unselected ordering values for continuation and use the primary id as a deterministic base-query tie-breaker, including metadata-backed nonconventional primary-id names.
- [x] Match live negative-page and TopCount/paging/total-count conflict codes/messages; reject malformed/incompatible simulator cookies and malformed integer FetchXML options explicitly.
- [ ] P2/P4: expand cookie support beyond base rows as justified. Linked, distinct, and aggregate/grouped queries use simple paging and return no simulator cookie; supplying a cookie for those shapes is explicitly unsupported.
- [ ] P2/P4: elastic-table 500-row limits, full SQL collation/choice-label/lookup-name ordering, and remaining invalid-cookie/option fault envelopes need targeted evidence and implementation.

Compare cookie continuation and boundary metadata against live results; do not require byte-identical cookie text unless that is the intended tested contract.

### Aggregate completion

Implemented: SDK alias wrappers/source metadata, empty result ids, typed numeric output, verified empty/grouped behavior, distinct countcolumn, and aggregate limits. The default 50,000-row limit faults when exceeded. Explicit `aggregatelimit` intentionally takes at most **limit + 1** arbitrary rows, confirmed by live comparisons and Microsoft documentation.

Aggregate requirement status:

- [x] Compare explicit-limit input boundaries live and match the exact negative/zero/oversized-limit fault code/message. Tests compare counts rather than assuming which arbitrary rows are selected.
- [x] Return `AliasedValue` values with source entity/attribute names and `Guid.Empty` aggregate ids.
- [x] Verify integer, Money, Double, and Decimal aggregate output, integer average truncation, empty input, null groups, distinct/non-null counts, and empty count output live. Int64 numeric output and grouped SDK Money/option/reference isolation also have unit coverage; broader live wrapper coverage remains open.
- [ ] P2/P4: verify wider timezone/user-local date grouping, fiscal settings, joins, and aggregate input-limit counting.
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

The original 7 single-entity and 9 linked-query cases are accompanied by 23 QuerySemantics, 12 SecurityIdentity (including five restricted linked cases), and 36 PagingAggregates cases. The 47 mutation cases brought the inventory to 158, all passing in the 23 October full live run. The 28 October follow-up adds 21 `P1Closeout` cases, taking the current inventory to **179**; see [current evidence](09-p1-closeout-review.md).

The earlier projection, nested/OR parsing, read security, base-cookie continuation, and typed/empty/limit aggregate additions are delivered. The final P1 follow-up also delivers unselected linked sorting/top/pages and explicit rejection of unsupported qualified conditions/orders/column comparisons. Next fixtures should cover:

1. P2/P4: wider cookie shapes, large live limits, and timezone/fiscal/mixed-currency grouping.
2. P2/P4: broader security depths, linked/grouped aggregation, lookup/choice ordering, and remaining exact fault envelopes.

Retain small deterministic datasets, exact assertions, and cleanup through the existing harness. New `P1Closeout` cases cover the bounded P1 corrections; broader nested/post-join and advanced semantics still require additional evidence and implementation.

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

Review baseline result: **560 passed, 4 skipped, 0 failed**. The first P1 implementation reached **606 passed, 0 skipped, 0 failed**; security, paging/aggregates, and mutation subsequently reached **913 passes with no skips**, rechecked on 26 October.

The ignored distinct aggregate count test from the September review is active and passing. The four empty TODOs for base filter/link projection in QueryExpression and FetchXML were removed during the P1 input/output implementation; active focused tests cover those scenarios.

The older 543-passed/5-skipped result described the September baseline. Historical intermediate counts remain dated evidence; the 28 October baseline is **1,043 passing unit cases** and **179 integration cases**. The three final P1 findings are implemented with focused fixtures; deeper parity limits remain explicit in [09](09-p1-closeout-review.md).

## Evidence anchors

- [Execution pipeline](../CloudAwesome.Xrm.Simulate/QueryParsers/QueryExpressionParser.cs)
- [Paging and base security filtering](../CloudAwesome.Xrm.Simulate/ServiceRequests/EntityMultipleRetriever.cs)
- [FetchXML conversion](../CloudAwesome.Xrm.Simulate/QueryParsers/FetchExpressionParser.cs)
- [Projection/cloning](../CloudAwesome.Xrm.Simulate/QueryParsers/EntityCloner.cs)
- [Linked rows and ordering](../CloudAwesome.Xrm.Simulate/QueryParsers/LinkedEntities.cs)
- [Aggregate output/limits](../CloudAwesome.Xrm.Simulate/QueryParsers/Aggregates.cs)
- [Distinct count and limit tests](../CloudAwesome.Xrm.Simulate.Test/QueryParserTests/FetchExpressionAggregateTests.cs)
