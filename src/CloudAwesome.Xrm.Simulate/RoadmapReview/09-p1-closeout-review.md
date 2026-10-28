# P1 close-out review and P2 handoff

Date reviewed: 2026-10-26; close-out implementation: 2026-10-28

Original reviewed source: `959c952` (`P1 roadmap fixes`, #399). All nine existing RoadmapReview documents, the four projects, targeted implementation/test paths, and repository-root CI were inspected on 26 October. The 28 October follow-up implements the three findings and retains corrected-contract unit and live fixtures.

## Decision

**The three P1 implementation findings are closed.** Ordinary CRUD now isolates supported mutable SDK values, unselected linked columns drive ordering before top/paging, and unsupported qualified query inputs fail explicitly before execution. Qualified filters/orders and column comparisons remain unsupported features; rejecting them is the bounded close-out contract, not a claim of full alias parity.

The new regression suite passes **1,043 unit cases, 0 skipped, 0 failed** (130 additions). The full live run passes **174 cases, with five existing restricted-user cases skipped**; the configured follow-up passes **all five, with no skips**. All **179 integration cases** therefore have fresh pass evidence across the two runs, including the 21 new close-out cases with mutable-value CRUD assertions. Broader metadata, relationship, security-depth, activity, and query breadth remain P2/P4/P6 work.

## Closed P1 findings - 28 October 2026

| Status | Finding | Historical reproduction on 26 October | Delivered close-out |
| --- | --- | --- | --- |
| Closed | Ordinary CRUD object isolation | All-column Retrieve returned the stored entity; selected Retrieve shared mutable SDK values. Create/Update retained caller values, allowing unaudited mutation. | Clone Create/Update inputs before processors, clone committed Create rows and Update patches, and deep-clone Retrieve results. Direct methods and request wrappers share the fixes. Regressions cover mutable SDK graphs, early-bound types, success, injected/processor/ownership/identity failures, security denial, unchanged audits, and explicitly mutable raw fixture access. |
| Closed | Ordering on unselected linked columns | Ordering by unselected contact lastname with `TopCount = 1` returned Zulu instead of Alpha. | Carry execution-only sort keys through joins, apply linked orders with declared precedence, then remove keys before projection/distinct. Top and simple paging consume the ordered rows. QueryExpression/FetchXML regressions cover ascending/descending, top, pages/counts, distinct, null/outer rows, base precedence, sibling/nested links, and no hidden output/store pollution. |
| Closed | Qualified QueryExpression inputs silently misexecute | A matching base criterion on alias `c.lastname = Alpha` returned zero rows because qualification was ignored. | Reject `ConditionExpression.EntityName`, `OrderExpression.EntityName`, `CompareColumns`, dotted input attribute names, and `AnyAllFilterLinkEntity` with `NotSupportedException`, recursively and independently of metadata/data presence. FetchXML qualified orders are also rejected. Live comparisons distinguish successful platform execution from deliberate simulator rejection; unqualified inner-link criteria remain supported. |

### Supported boundaries and evidence

- [CRUD regressions](../CloudAwesome.Xrm.Simulate.Test/BasicServiceRequestTests/CrudIsolationTests.cs) cover `Money`, options/collections, lookup references and keys, bytes, aliased values, nested entities/collections, related entities, formatted values, shared references and cyclic entity graphs. Successful service Create no longer writes generated ids/system fields into the caller; use its returned id. Processors receive detached inputs and cannot mutate committed values afterward through retained references.
- [Query regressions](../CloudAwesome.Xrm.Simulate.Test/QueryParserTests/P1CloseoutQueryTests.cs) and [input validator](../CloudAwesome.Xrm.Simulate/QueryParsers/QueryInputValidator.cs) enforce the supported execution boundary before security-filtered data execution and success auditing. Public QueryExpression parser entry points also validate inputs.
- [Live close-out fixture](../CloudAwesome.Xrm.Simulate.Integration/QueryTests/P1CloseoutParityTests.cs), category `P1Closeout`, adds 21 cases: 12 linked-order/top/page cases, three successful-live/explicit-simulator-rejection comparisons, two unqualified-filter controls, and four direct/request plus all/selected CRUD cases. The CRUD cases also assert stored Money/option values after caller/result mutation.
- Raw `.Simulated().Data().Add/Get` remains a mutable fixture API, with an explicit regression. Fixture/options/security graph isolation and arbitrary custom attribute objects remain P4 work. `MockedEntityDataService.Update` now detaches patches to protect all service mutation callers.
- Linked/distinct/grouped cookies remain unsupported; linked ordering uses the existing simple paging contract. Wider alias propagation, post-join predicates, column comparisons, advanced joins, platform collation and lookup/choice label sorting remain P2/P6 work.

## Verification - 28 October 2026

- Release unit suite: **1,043 passed, 0 skipped, 0 failed**. Local artifact: `CloudAwesome.Xrm.Simulate.Test/TestResults/p1-closeout-unit.trx`.
- Four-project Release build: **succeeded, 28 existing compiler warnings, 0 errors** on the final incremental run. No clean-build warning or coverage-percentage baseline is claimed.
- Full live suite: **174 passed, 5 skipped, 0 failed**, 179 discovered. The five existing restricted-caller scenarios skipped because `DATAVERSE_SECURITY_USER_ID` was unset in that process. Local artifact: `CloudAwesome.Xrm.Simulate.Integration/TestResults/p1-closeout-live-full.trx`.
- Restricted-caller follow-up: **5 passed, 0 skipped, 0 failed**, using the user id already present in local settings, supplied as a process-only environment variable. Local artifact: `CloudAwesome.Xrm.Simulate.Integration/TestResults/p1-closeout-live-restricted.trx`. These five passes supplement the full run; this is not a claim that one full invocation passed 179 with no skips.
- Both final live TRX files contain **zero stderr elements and zero reported cleanup failures**. All 21 `P1Closeout` cases pass in the full run; the earlier focused run also passed 21 cases before the CRUD Money/option assertions were strengthened.
- Live execution used network access and existing sandbox configuration. No credential/settings files, schema, users, or roles were changed. A preliminary restricted-network run failed during connection initialization before scenario arrangement; it is not parity evidence.
- No published-package, hosted CI, external documentation, schema endpoint, or companion CLI audit is claimed.

Rerun unit/build and the close-out parity subset with the existing sandbox configuration:

```powershell
dotnet test CloudAwesome.Xrm.Simulate.Test/CloudAwesome.Xrm.Simulate.Test.csproj --no-restore --configuration Release
dotnet build CloudAwesome.Xrm.Simulate.sln --no-restore --configuration Release
dotnet test CloudAwesome.Xrm.Simulate.Integration/CloudAwesome.Xrm.Simulate.Integration.csproj --no-restore --configuration Release --filter "TestCategory=P1Closeout"
```

For full live verification, omit the category filter and supply `DATAVERSE_SECURITY_USER_ID` for the five existing restricted-user cases. Do not count skipped cases as passes.

## Original diagnostic evidence - 26 October 2026

The following source observations describe `959c952` before the fixes, not the current implementation.

### Ordinary CRUD evidence

- [EntityRetriever](../CloudAwesome.Xrm.Simulate/ServiceRequests/EntityRetriever.cs) assigns the stored row directly for AllColumns and copies selected values by reference. Both `Retrieve` and `RetrieveRequest` reproduce the issue: changing returned `Money.Value` from 10 to 99 changes the stored value without Update; AllColumns also allows direct string replacement in the stored row.
- [EntityCreator](../CloudAwesome.Xrm.Simulate/ServiceRequests/EntityCreator.cs) adds its processed input entity directly to the store. Changing the original entity after Create changes stored strings and Money values.
- [EntityUpdater](../CloudAwesome.Xrm.Simulate/ServiceRequests/EntityUpdater.cs) and [data-store Update](../CloudAwesome.Xrm.Simulate/DataServices/MockedEntityDataService.cs) retain incoming attribute objects. Changing a Money instance after Update changes the stored value again without another Update audit.
- This undermines authorization/audit assertions in simulator tests. It is a simulator correctness issue, not a claim about a live Dataverse security vulnerability. Query cloning, cloned Upsert payloads, and the hardened queue operations do not repair these separate direct CRUD paths.

### Linked query evidence

- [LinkedEntities](../CloudAwesome.Xrm.Simulate/QueryParsers/LinkedEntities.cs) calls MergeEntities, which projects linked attributes, before ApplyLinkedOrders reads the projected alias value. With an unselected sort column, every sort key becomes null and source insertion order survives.
- [QueryExpressionParser](../CloudAwesome.Xrm.Simulate/QueryParsers/QueryExpressionParser.cs) applies base filters before links. [Filter](../CloudAwesome.Xrm.Simulate/QueryParsers/Filter.cs) and [EqualConditionHandler](../CloudAwesome.Xrm.Simulate/QueryParsers/ConditionHandlers/EqualConditionHandler.cs) do not resolve the condition's EntityName. A qualified linked condition is neither honored nor explicitly rejected.
- Microsoft documents [linked ordering and qualified OrderExpression.EntityName](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/queryexpression/order-rows) and [qualified conditions for joined tables](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/queryexpression/join-tables). The new observations are local reproductions, not new live parity results.

Nine temporary diagnostic cases confirmed these behaviors, including a selected-sort-column control. They asserted the defects and were removed; they were not part of the 913-test baseline. The 28 October follow-up retains 130 tests asserting the corrected contract and its boundaries.

## Historical verification boundary - 26 October 2026

- Fresh Release unit execution on 26 October: **913 passed, 0 skipped, 0 failed**.
- Four-project Release build succeeded with **14 existing warnings, 0 errors** on this incremental run; this is not a clean-build warning baseline. Current integration discovery remains **158 cases**.
- Existing local TRX files confirm the **23 October** full live run: **158 passed, 0 skipped, 0 failed**. The final queue cleanup follow-up contains **30 passes and no test stderr/cleanup failures**.
- No live sandbox tests were rerun on 26 October. Historical pass counts remain evidence for their stated scenarios; they do not cover the new findings or prove general platform parity.
- No published-package, hosted CI, external documentation, schema endpoint, or companion CLI audit is claimed.

## Documentation reconciliation - 26 October 2026 (historical)

- Executive summary: current evidence now precedes the historical baseline; query and mutation findings are reopened with links to this review. Security/identity and the scoped paging/aggregate pass remain delivered.
- Simulation parity and architecture: distinguish deep query cloning from unsafe direct CRUD references; mark delivered metadata validation, auditing, and removed TODO placeholders accurately.
- Integration: distinguish fresh discovery from historical live results; retain opt-in/configuration/CI and strict cleanup reporting as P2 work.
- Benchmarking: linked security filtering and typed aggregate output are delivered dependencies; benchmarks remain unimplemented P7 work.
- Metadata: close the delivered FetchXML/projection/ownership/aggregate checklist entries while retaining deeper type, state/status, key uniqueness, and relationship semantics.
- Security: exact denied-operation envelopes remain P2 evidence work; do not leave that evidence-only task labeled as an unresolved P1 implementation gate.
- Queues: separate delivered lifecycle cleanup from outstanding live security and queue-transaction evidence.
- Query review: retain the genuine P1 sorting/qualified-input gaps, remove obsolete claims of no live execution, and stop listing completed fixture work as future additions.

## P2 starting sequence

1. Completed on 28 October: close the three P1 findings with unit regressions and focused parity/rejection cases. Retain the new results and maintain the explicit unsupported-feature boundary above.
2. Define explicit opt-in execution, missing-configuration/readiness behavior, restricted-user requirements, sandbox permissions, and a checked-in settings example containing placeholders.
3. Add a manual live-parity workflow with protected sandbox secrets, exact category filters, timeouts, TRX retention, and cleanup failures visible as job failures. Verify Linux path portability: the existing Ubuntu unit workflow stores TEST_PROJECT_PATH with Windows backslashes; a hosted failure was not reproduced by this Windows review.
4. Expand the highest-value evidence: provisioned alternate keys, security depths/denied operations, team/queue privileges, queue transaction rollback, and currently unsupported query shapes. Keep implementation work scoped to captured behavior or explicit rejection.
5. Add scheduled/release parity gates after manual runs and cleanup are reliable. Package/docs/version policy remain P3; complete metadata/relationships remain P4; benchmark baselines remain P7.
