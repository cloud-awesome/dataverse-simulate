# CloudAwesome.Xrm.Simulate maturity roadmap

Date reviewed: 2026-10-28; implementation history: 2026-10-22/23/28

Original review: 2026-06-02, with subsequent September updates

## Executive update - 28 October 2026

**The three remaining P1 implementation findings are closed.** Ordinary CRUD now isolates caller/stored mutable SDK values, linked ordering uses unselected columns before top/paging, and unsupported qualified query inputs fail explicitly. Alias-qualified filters/orders and column comparisons remain explicit limitations. See the [completed findings, evidence, and P2 handoff](09-p1-closeout-review.md).

Fresh Release unit execution passes **1,043 tests with no skips** (130 additions), and the four-project Release build succeeds with existing warnings. The full live run passes **174 tests with five restricted-user skips**; the configured follow-up passes **all five with no skips**. All **179 integration cases** have fresh pass evidence across those two runs, including 21 new close-out cases, with no reported cleanup errors. See [exact results and limitations](09-p1-closeout-review.md#verification---28-october-2026). The next implementation priority is P2 repeatability and deeper scoped parity.

The project has moved from implementing its foundations to hardening a substantially broader simulator. Most original correctness fixes and the first implementations of metadata, security, queues, common organization requests, query projection, paging, and aggregation are now present. The immediate sequence in the previous review has largely been delivered, sometimes in a different order.

The next milestone should be a trustworthy, documented release of that existing surface. Preserve the completed scoped contracts below, and prioritize repeatable live parity and release automation before another broad feature expansion. A green unit suite proves the current simulator contract; it does not by itself establish exact Dataverse behavior.

## Scope and evidence

The 26 October close-out review inspects the four projects, repository-root CI, tests, and all roadmap documents at `959c952` (`P1 roadmap fixes`, #399). Only markdown in `RoadmapReview` is retained from this review. The following original baseline inspected `2620652` (`Security model enhancements`, 2026-09-23); its counts are historical.

Verification on 2026-10-22:

- Unit suite: **560 passed, 4 skipped, 0 failed** using `dotnet test CloudAwesome.Xrm.Simulate.Test/CloudAwesome.Xrm.Simulate.Test.csproj --no-restore`.
- Four-project Release build: succeeded with 0 warnings and 0 errors reported by this incremental build, using `dotnet build CloudAwesome.Xrm.Simulate.sln --no-restore --configuration Release`.
- Integration discovery: **40 tests** using `dotnet test CloudAwesome.Xrm.Simulate.Integration/CloudAwesome.Xrm.Simulate.Integration.csproj --no-build --no-restore --configuration Release --list-tests`. Live sandbox execution was not performed during this review.
- The four older empty projection/link TODOs were subsequently removed in the first P1 pass; active focused tests cover those behaviors. There are no skips in the current 1,043-test suite.

Public documentation, NuGet, GitHub issue counts, and the separate `dvcli` repository were not refreshed. The previous claims about published version `1.0.4` and 60 open issues are historical, not current evidence. The local library project declares **1.1.1**; this does not prove publication.

Throughout this review, a checked item means the stated implementation exists and has the indicated local evidence. It does not mean every edge case is complete or that live tests passed today. Partial items and live verification work remain explicitly open.

## Completed foundations

- [x] Sparse updates merge attributes, preserve omitted values, set modification fields, use update processors, and audit; selected-column retrieval selects the requested id.
- [x] Disassociation and a separate relationship store replace the former target-payload mutation approach.
- [x] Missing-record and duplicate-id faults use `FaultException<OrganizationServiceFault>`; unknown requests produce a helpful `NotSupportedException`.
- [x] Organization-service and service-provider contexts are instance-scoped. Factory-created services reuse their provider's data and audit state.
- [x] **29 built-in organization request handlers**, including direct equivalents, upsert, state, batches/transactions, sharing, team membership, queues, and four metadata retrieval requests.
- [x] Optional metadata contract `1.0` loading, including split files, runtime models, CRUD/query validation, and SDK metadata projection.
- [x] Optional security graph, privilege depths, owner teams, explicit shares, base-row query filtering, request enforcement, XML role import, default business-unit teams, and role replication/association behavior.
- [x] Typed queue setup, shared queue service, and add/remove/pick/release/route requests over ordinary entity rows.
- [x] Query filtering and ordering before final projection, inner/outer links with `AliasedValue`, projected distinct, page slicing/count metadata for all three query forms, distinct aggregate count, and aggregate-limit handling.
- [x] Reusable live/simulator parity harness and focused integration fixtures now exist.
- [x] Repository-root GitHub Actions builds the solution, runs unit tests with coverage, and uploads results for pull requests.

These are completed first implementations. Metadata, security, transactions, and query execution still have fidelity gaps described in the companion reviews.

## Updated order of work

This is the authoritative sequence for the companion documents. P1 and P2 should be developed together: capture live behavior for a gap, implement the correction, then retain the regression test.

| Order             | Work item                                            | Why now / completion criterion                                                                                                                                                                                                        |
|-------------------|------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **P1 - complete** | Harden the implemented behavior                      | The three final correctness gaps have implementations and focused unit/live fixtures. Preserve explicit unsupported contracts rather than claiming whole-subsystem parity.                                                            |
| **P2**            | Make live parity repeatable                          | Extend the harness beyond the delivered P1 comparisons into deeper metadata/security/activity cases; add opt-in/manual CI and a release parity run with recorded results.                                                             |
| **P3**            | Prepare a documented, reproducible release           | Publish a support/limitations matrix and current examples; add package metadata, package validation, version policy, symbols/source support, and a release workflow. Existing PR CI is the starting point.                            |
| **P4**            | Complete metadata and relationship semantics         | Implement relationship validation/direction, lookup/intersect mutation, ownership-aware defaults, declared alternate-key enforcement, type validation, and selected cascade/concurrency behavior as real unit-test scenarios require. |
| **P5**            | Improve isolated plugin context and setup ergonomics | Complete context property wiring, parent/transaction context fixtures, and documented setup helpers. Preserve the unit-testing scope; automatic plugin registration and cascading execution are excluded.                             |
| **P6**            | Add targeted request/query breadth                   | Prioritize `In`/`NotIn` and real consumer scenarios, then scoped/fiscal/multiselect operators, advanced joins, global option-set retrieval, bulk upsert or activity/platform helpers as justified.                                    |
| **P7**            | Establish performance baselines                      | Add BenchmarkDotNet over supported scenarios, including security, paging, queues, and batches, before considering indexes or other optimizations.                                                                                     |

### P1 implementation status and close-out

- [x] **Query output and input semantics:** the first pass delivered projection/distinct identity, SDK query isolation, nested/OR FetchXML and metadata/input validation, with **23 passing live cases**. The 28 October follow-up closes unselected linked ordering and silent qualified-input misexecution through hidden sort values and explicit input rejection, with direct/FetchXML regressions and focused live comparisons. Wider qualified/post-join/column-comparison support remains P2/P6. See [close-out evidence](09-p1-closeout-review.md) and the [query contract](08-query-execution-phase-review.md).
- [x] **Security and identity:** the 23 October implementation filters linked rows before joins/counts/paging/aggregates, isolates factory caller identity over shared state, rejects unsupported custom `ISecurityModel` implementations, and unifies owner validation/owning fields across Create, Assign, Update, and Upsert. Metadata drives organization-owned table access/defaults. **63 new regression cases; 669 unit tests passing.** The initial live run passed seven cases and skipped five; the configured restricted-user follow-up passed all five linked cases. Live evidence corrected `WhoAmI`: it reports the authenticated connection user, while CRUD/query permissions follow the impersonated caller. Exact privilege-denial envelopes, broader depth parity, cascades, and modernized business-unit ownership remain open in the [security implementation notes](06-security-model.md#p1-implementation-and-verification---23-october-2026).
- [x] **Paging and aggregates:** the 23 October pass implements value-based cookie continuation for supported base queries, standard-table page caps, final-page cookie metadata, and verified paging-option faults. Aggregates now return typed `AliasedValue` results with empty ids, correct integer averages, and verified empty/grouped output. Live evidence confirms that explicit `aggregatelimit` intentionally aggregates up to `limit + 1` arbitrary rows. **121 new regression cases; 790 unit tests passing; 36 new live parity cases passing.** Linked/distinct/grouped cookie support, elastic-table limits, and timezone/fiscal grouping fidelity remain explicitly scoped follow-up work in the [paging/aggregate implementation notes](08-query-execution-phase-review.md#p1-paging-and-aggregates-implementation---23-october-2026).
- [x] **Mutation/response consistency:** the 23 October pass delivered success-only auditing, cloned Upsert payloads/key semantics, indexed transaction faults/response defaults and safe queue moves, with **123 new regressions and 47 new live cases** at that stage. The 28 October follow-up isolates ordinary Create/Update inputs, processor-retained values and Retrieve responses for direct/request forms, including mutable SDK graphs and failed/denied-operation audit regressions. Raw fixture access remains mutable. See [close-out evidence](09-p1-closeout-review.md). Global key uniqueness and full queue metadata/privileges remain P2/P4 work in the [mutation notes](01-simulation-parity.md) and [queue review](07-queue-simulation.md).

## Milestone exit criteria

The next release should have a named supported subset, a passing unit suite with resolved skip dispositions, recorded live results for that subset, reproducible packages, current documentation, and explicit limitations for remaining gaps. P4-P7 do not all need to be complete to release; anything deferred must be visible to consumers.

Do not carry forward the old proposed version numbers as commitments. Choose the next version from the actual published baseline and the compatibility policy, particularly where correcting simulator behavior can break existing tests.

## Companion reviews

- [01 - Simulation parity and support matrix](01-simulation-parity.md)
- [02 - Architecture, tests, documentation, CI/CD, and publishing](02-architecture-tests-docs-ci.md)
- [03 - Integration testing](03-integration-testing.md)
- [04 - Benchmarking](04-benchmarking.md)
- [05 - Metadata simulation](05-metadata-simulation.md)
- [06 - Security model](06-security-model.md)
- [07 - Queue simulation](07-queue-simulation.md)
- [08 - Query execution](08-query-execution-phase-review.md)
- [09 - P1 close-out review and P2 handoff](09-p1-closeout-review.md)
- [10 - Fluent configuration API review and implementation plan](10-fluent-configuration-api.md)
