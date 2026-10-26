# P1 close-out review and P2 handoff

Date reviewed: 2026-10-26

Reviewed source: `959c952` (`P1 roadmap fixes`, #399). All nine existing RoadmapReview documents, the four projects, targeted implementation/test paths, and repository-root CI were inspected. This review changes roadmap markdown only; it does not implement the findings below.

## Decision

P2 preparation can start in parallel, but **P1 is not fully closed**. The four implementation passes delivered substantial corrections and live evidence. The previous all-P1-complete summary overlooked two query items still open in document 08 and overstated ordinary CRUD object isolation.

Close the three bounded correctness findings below before signing off P1. Broader metadata, relationship, security-depth, activity, and query breadth remain P2/P4/P6 work; they are not all prerequisites for this close-out.

## Outstanding P1 findings

| Priority | Finding | Reproduced behavior | Required close-out |
| --- | --- | --- | --- |
| P1 | Ordinary CRUD object isolation | All-column Retrieve returns the stored entity; selected Retrieve shares mutable SDK values. Create stores its caller's entity, and Update retains mutable attribute references. Subsequent caller mutations change stored data without another service operation or audit. | Clone service inputs/results and stored mutable values at the appropriate boundary for direct calls and request wrappers. Add regressions for successful and denied/failed operations, mutable SDK values, and unchanged audits. Keep raw `.Simulated().Data()` fixture access explicitly separate. |
| P1 | Ordering on unselected linked columns | A link ordered by contact lastname with `TopCount = 1` returns the Zulu account when lastname is not selected; selecting lastname returns the Alpha account. Projection discards the sort value before ordering. | Preserve hidden linked sort values until ordering/top/paging have finished, without exposing unwanted output columns. Add direct/FetchXML regressions and live parity for this supported join shape. |
| P1 | Qualified QueryExpression inputs silently misexecute | A base criterion on alias `c.lastname = Alpha` returns zero rows despite a matching joined contact. `ConditionExpression.EntityName` is ignored and the filter runs before the join. | Support the qualified semantics or explicitly reject unsupported forms before execution. Audit related qualified ordering/column-comparison flags, add rejection/correctness regressions, and capture focused live comparisons. Full advanced alias/join support need not be implemented to close the silent-misexecution gap. |

### Ordinary CRUD evidence

- [EntityRetriever](../CloudAwesome.Xrm.Simulate/ServiceRequests/EntityRetriever.cs) assigns the stored row directly for AllColumns and copies selected values by reference. Both `Retrieve` and `RetrieveRequest` reproduce the issue: changing returned `Money.Value` from 10 to 99 changes the stored value without Update; AllColumns also allows direct string replacement in the stored row.
- [EntityCreator](../CloudAwesome.Xrm.Simulate/ServiceRequests/EntityCreator.cs) adds its processed input entity directly to the store. Changing the original entity after Create changes stored strings and Money values.
- [EntityUpdater](../CloudAwesome.Xrm.Simulate/ServiceRequests/EntityUpdater.cs) and [data-store Update](../CloudAwesome.Xrm.Simulate/DataServices/MockedEntityDataService.cs) retain incoming attribute objects. Changing a Money instance after Update changes the stored value again without another Update audit.
- This undermines authorization/audit assertions in simulator tests. It is a simulator correctness issue, not a claim about a live Dataverse security vulnerability. Query cloning, cloned Upsert payloads, and the hardened queue operations do not repair these separate direct CRUD paths.

### Linked query evidence

- [LinkedEntities](../CloudAwesome.Xrm.Simulate/QueryParsers/LinkedEntities.cs) calls MergeEntities, which projects linked attributes, before ApplyLinkedOrders reads the projected alias value. With an unselected sort column, every sort key becomes null and source insertion order survives.
- [QueryExpressionParser](../CloudAwesome.Xrm.Simulate/QueryParsers/QueryExpressionParser.cs) applies base filters before links. [Filter](../CloudAwesome.Xrm.Simulate/QueryParsers/Filter.cs) and [EqualConditionHandler](../CloudAwesome.Xrm.Simulate/QueryParsers/ConditionHandlers/EqualConditionHandler.cs) do not resolve the condition's EntityName. A qualified linked condition is neither honored nor explicitly rejected.
- Microsoft documents [linked ordering and qualified OrderExpression.EntityName](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/queryexpression/order-rows) and [qualified conditions for joined tables](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/queryexpression/join-tables). The new observations are local reproductions, not new live parity results.

Nine temporary diagnostic cases confirmed these behaviors, including a selected-sort-column control. They asserted the observed defects for diagnosis and were removed afterward; they are not retained regressions or part of the 913-test baseline. The follow-up implementation should add tests asserting the corrected contract.

## Verification boundary

- Fresh Release unit execution on 26 October: **913 passed, 0 skipped, 0 failed**.
- Four-project Release build succeeded with **14 existing warnings, 0 errors** on this incremental run; this is not a clean-build warning baseline. Current integration discovery remains **158 cases**.
- Existing local TRX files confirm the **23 October** full live run: **158 passed, 0 skipped, 0 failed**. The final queue cleanup follow-up contains **30 passes and no test stderr/cleanup failures**.
- No live sandbox tests were rerun on 26 October. Historical pass counts remain evidence for their stated scenarios; they do not cover the new findings or prove general platform parity.
- No published-package, hosted CI, external documentation, schema endpoint, or companion CLI audit is claimed.

## Documentation reconciliation

- Executive summary: current evidence now precedes the historical baseline; query and mutation findings are reopened with links to this review. Security/identity and the scoped paging/aggregate pass remain delivered.
- Simulation parity and architecture: distinguish deep query cloning from unsafe direct CRUD references; mark delivered metadata validation, auditing, and removed TODO placeholders accurately.
- Integration: distinguish fresh discovery from historical live results; retain opt-in/configuration/CI and strict cleanup reporting as P2 work.
- Benchmarking: linked security filtering and typed aggregate output are delivered dependencies; benchmarks remain unimplemented P7 work.
- Metadata: close the delivered FetchXML/projection/ownership/aggregate checklist entries while retaining deeper type, state/status, key uniqueness, and relationship semantics.
- Security: exact denied-operation envelopes remain P2 evidence work; do not leave that evidence-only task labeled as an unresolved P1 implementation gate.
- Queues: separate delivered lifecycle cleanup from outstanding live security and queue-transaction evidence.
- Query review: retain the genuine P1 sorting/qualified-input gaps, remove obsolete claims of no live execution, and stop listing completed fixture work as future additions.

## P2 starting sequence

1. Close the three P1 findings with unit regressions and focused parity/rejection cases; retain their results as the new baseline.
2. Define explicit opt-in execution, missing-configuration/readiness behavior, restricted-user requirements, sandbox permissions, and a checked-in settings example containing placeholders.
3. Add a manual live-parity workflow with protected sandbox secrets, exact category filters, timeouts, TRX retention, and cleanup failures visible as job failures. Verify Linux path portability: the existing Ubuntu unit workflow stores TEST_PROJECT_PATH with Windows backslashes; a hosted failure was not reproduced by this Windows review.
4. Expand the highest-value evidence: provisioned alternate keys, security depths/denied operations, team/queue privileges, queue transaction rollback, and currently unsupported query shapes. Keep implementation work scoped to captured behavior or explicit rejection.
5. Add scheduled/release parity gates after manual runs and cleanup are reliable. Package/docs/version policy remain P3; complete metadata/relationships remain P4; benchmark baselines remain P7.
