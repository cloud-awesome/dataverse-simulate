# Integration testing roadmap

Date reviewed: 2026-10-26; live evidence through 2026-10-23

## Current position

The original connectivity-only stage is complete. A reusable dual-execution harness and several focused live/simulator parity suites now exist. The next priority is reliable execution and deeper coverage of the implemented features, not creating another harness.

**Current verification boundary:** the 26 October local review rechecks the Release build and discovery of **158 cases**. The last full live result is **158 passes on 23 October**, with the final queue cleanup follow-up also passing. No live sandbox tests were rerun on 26 October. The original 40-case inventory below is retained as a dated baseline, followed by the implementation history.

The [close-out review](09-p1-closeout-review.md) identifies three remaining P1 gates despite those green results. P2 configuration/automation can begin in parallel; the new CRUD-isolation and query-order/qualification cases should accompany the P1 follow-up.

## Delivered harness

- [x] `DataverseParityScenario<TResult>` defines live arrangement, simulator options/arrangement, a shared operation, normalization, and equivalence assertions.
- [x] `DataverseParityHarness` executes live and simulated services within one scenario.
- [x] Shared `DataverseScenarioState` carries ids and values between arrangements.
- [x] `AfterLiveAct` supports live result/cleanup bookkeeping.
- [x] `LiveDataverseCleanupScope` tracks records, deduplicates them, and deletes in reverse order on disposal.
- [x] The base fixture disposes the cached Dataverse connection at fixture teardown.
- [x] Tests include focused snapshots/assertions for selected faults, query outputs, response shapes, and side effects.

The harness does not automatically capture/compare every exception or every SDK response field. Fault scenarios capture the contract they need themselves. Extend shared capture helpers only where this reduces repetition without hiding important assertions.

## Baseline fixture inventory - 22 October 2026

| Fixture area | Tests discovered | Current purpose |
| --- | --- | --- |
| Smoke | 3 | Create id, QueryExpression retrieval, configured `WhoAmI`. |
| Missing-record/duplicate-id faults | 8 | Direct and request forms of retrieve/update/delete missing rows and duplicate create ids. |
| Single-entity queries | 7 | QueryExpression/FetchXML filter and projection, unselected ordering before top, distinct, and selected count metadata. |
| Linked queries | 9 | Aliased values/types, selected columns, unselected join/filter attributes, inner/outer joins, linked ordering, and FetchXML links. |
| Relationships | 4 | Direct/request association and disassociation do not mutate retrieved target `RelatedEntities`. This is narrower than full relationship side-effect parity. |
| Core organization requests | 4 | Upsert create response, set-state mutation, ExecuteMultiple fault response, ExecuteTransaction rollback. |
| Security request faults | 3 | Missing share target/principal and missing team. These do not establish privilege-depth or access-denial parity. |
| Metadata/security interaction | 2 | Selected metadata retrieval results can be obtained independently of the simulated table-read permission. |
| **Total** | **40** | Source/discovery coverage only; live results require a separate run. |

## Prioritized additions

Follow [P1-P7](00-executive-summary.md#updated-order-of-work). P2 fixtures should accompany P1 fixes, not wait until all implementation work is finished.

### P1/P2 - Existing behavior with the highest risk

The first query input/output implementation added **23 `QuerySemantics` cases** for projection/identity, default/all FetchXML columns, OR/nested filters, metadata rejection, invalid operators, malformed input, and empty base projections with selected/filter-only links. Together with the original 40 cases, that pass brought the inventory to 63. After refreshing authentication and shortening fixture last names to meet the platform limit, the final focused live run on 2026-10-22 passed **all 23 cases**. It also corrected the empty distinct projection behavior and tightened rejection assertions to exclude unrelated service failures. This result covered that category, not a rerun of all integration fixtures. See the [query implementation notes](08-query-execution-phase-review.md#p1-query-inputoutput-implementation---22-october-2026) for the command and simulator exception limitations.

The 23 October security pass adds **12 `SecurityIdentity` cases** (75 integration cases in total). The initial focused Release run passed seven and skipped five. After configuring the restricted user, **all five restricted linked visibility/count/aggregate cases passed live**, establishing pass evidence for all twelve. The follow-up corrected an invalid `WhoAmI` assertion: that request reports the authenticated connection user, while record reads and joins apply the impersonated caller's permissions. The fixture now checks both connection identity and effective visible/hidden record access. Restricted cases require `DATAVERSE_SECURITY_USER_ID` and skip before connecting when absent. See [security setup, commands, and limits](06-security-model.md#p1-implementation-and-verification---23-october-2026). Factory null/empty semantics have unit coverage and SDK documentation evidence, not a live plugin-host parity result.

The paging/aggregate pass adds **36 `PagingAggregates` cases**, all passing live, taking integration discovery to **111 cases**. The fixture compares SDK wrappers and numeric types, empty/grouped/limit output, multi-page metadata, default SDK counts, value-based continuation after boundary changes, and selected exact input faults. See [the supported contract and command](08-query-execution-phase-review.md#p1-paging-and-aggregates-implementation---23-october-2026). Large 5,001/50,001-row boundaries remain unit/documentation evidence rather than expensive live fixtures; special cookie shapes, elastic tables, timezones, and mixed-currency aggregation remain open.

The paging pass's combined Release run passed **71 tests, 0 skipped, 0 failed** using `--filter "TestCategory=PagingAggregates|TestCategory=QuerySemantics|TestCategory=SecurityIdentity"`. This reran those three P1 categories, including the configured restricted-user cases; it did not rerun the original 40 integration cases. The later full run is recorded below.

The mutation/response pass adds **47 `MutationConsistency` cases**: 17 response/upsert/transaction cases and 30 `Queues` cases, taking the current inventory to **158 integration cases**. Focused live runs cover all 47. They compare response names/keys/SDK ids, Upsert request isolation and undeclared matching keys, transaction default/explicit responses and indexed faults, all batch settings, task queue move identity, selected exact validation faults, removal, release, routing, and target survival. See [the mutation contract](01-simulation-parity.md#p1-mutationresponse-implementation---23-october-2026) and [queue limits](07-queue-simulation.md#p1p2-implementation-and-verification---23-october-2026). These use the existing app connection and temporary records; no schema keys, users, or roles are provisioned.

The full Release integration run passed **158 tests, 0 skipped, 0 failed**, including all original fixtures and the configured restricted-user cases, using `dotnet test CloudAwesome.Xrm.Simulate.Integration/CloudAwesome.Xrm.Simulate.Integration.csproj --no-build --no-restore --configuration Release`. Its TRX is `CloudAwesome.Xrm.Simulate.Integration/TestResults/mutation-consistency-release.trx`. Initial queue cleanup exposed platform deletion faults and one remaining fixture queue/task; the exact tracked records were subsequently released/removed and deleted. Cleanup now releases/removes queue items through SDK messages, retries failures once after dependent records, and accepts confirmed missing-record faults as already clean.

After the cleanup correction, the final Release queue run passed **30 tests, 0 skipped, 0 failed**, with **no cleanup errors reported**, using `--filter "TestCategory=Queues"`. The local artifact is `CloudAwesome.Xrm.Simulate.Integration/TestResults/queue-cleanup-release.trx`. The earlier 47-case mutation rerun also passed; the final queue run verifies the last harness change without repeating unrelated query/security cases.

- [x] Initial empty/explicit column-set, primary-id projection, and distinct identity comparisons, covered by the 23 query input/output cases.
- [x] Initial FetchXML OR/nested filters, explicit unsupported-operator/multiple-value rejection, malformed input, and metadata-backed unknown entity/attribute comparisons; richer syntax and full fault envelopes remain open.
- [x] Initial multi-page/cookie/default-count/TopCount parity with result-set mutation and final-page metadata. Standard-table large count/page caps have unit coverage; large live, special-cookie, and elastic-table cases remain open.
- [x] Initial typed aggregate/empty/null-group/distinct/explicit-limit/invalid-limit parity. Timezone/fiscal/mixed-currency and default-limit large live cases remain open.
- [x] Execute the five restricted-user linked read/count/aggregate cases: all passed in the configured follow-up. Broader paging/depth parity remains open.
- [ ] Add live plugin-host service-factory caller/null/system comparisons; the per-service implementation and unit regressions are complete.
- [x] Add/run initial ownership-change, missing-assignee, organization-owned read, and stale-owning-field parity cases: seven live passes on 23 October. Cross-BU/access-team/denied-privilege parity remains open.
- [x] Initial ordinary-task queue add/remove/pick/release/route responses, duplicate/source move identity, selected faults, worker state, and target survival: 30 dedicated cases. Team routes, received-email behavior, custom fields, and the privilege matrix remain open.
- [x] Upsert primary-id/key-attribute create/update paths and matching undeclared-key rejection; batch settings/partial commits; transaction response defaults, fault type/code/message/index, nested-batch rejection, and rollback.
- [ ] Positive live declared alternate-key provisioning, collation/uniqueness, and direct key-based CRUD.
- [ ] Successful sparse update, delete, assign, and relationship lookup/intersect effects, beyond the existing fault/payload checks.
- [ ] P1 follow-up: ordinary Create/Update/Retrieve reference isolation, ordering on unselected linked columns, and qualified QueryExpression correctness or explicit rejection. The current 158 live cases do not establish these contracts.

### P2/P4 - Metadata and security depth

- [ ] Required-level behavior through the SDK, runtime types, choice/multiselect values, lookup existence/targets, numeric precision, state/status defaults and partial updates.
- [ ] Complete metadata response properties/filters rather than selected snapshots alone; missing metadata and invalid-name faults.
- [ ] Users in sibling/child business units, owner teams, explicit user/team shares, membership changes, and effective access masks.
- [ ] Exact access-denied faults and operation-specific privilege requirements.
- [ ] Relationship direction, append/append-to, delete/assign cascade cases as each supported rule is added.

Use transient fixtures and supported customization automation, potentially from the companion `dvcli` project, to create the required roles/tables. Preserve the existing decision to avoid a permanent gold dataset. Full access-team template behavior remains deferred; model fixtures only for the supported subset.

## Result comparison contract

Compare the behavior relevant to each scenario:

- Response type/name and output parameter keys/value types.
- Entity attributes, projected primary ids, formatted values, references, and `AliasedValue` metadata.
- `MoreRecords`, cookie continuation semantics, `TotalRecordCount`, and limit flags.
- Fault type/code/message and inner fault/request index where applicable.
- Stored side effects after success/failure and transaction rollback.

Normalize generated ids/timestamps only where their actual value is irrelevant. Do not normalize away ordering, SDK types, identity, error details, or the field being verified. Prefer small explicit snapshots/assertions to large opaque captures.

## Configuration and opt-in behavior

The current connection manager reads **a JSON file path** from `DATAVERSE_APPSETTINGS`, falling back to `appsettings.local.json`, and uses `ConnectionStrings:default` from that file. `DATAVERSE_APPSETTINGS` is not a direct connection-string environment variable.

- [x] Local JSON configuration and an environment-selected file path work as the current configuration mechanism.
- [x] Local settings are excluded from tracked files; no committed example settings file was found.
- [ ] P2: add a documented secret-backed CI configuration path, either a directly supplied connection string or a temporary JSON file.
- [ ] P2: add an example with placeholders and document supported ServiceClient authentication choices.
- [ ] P2: make integration execution explicitly opt-in and define missing-configuration behavior. The general harness currently attempts a connection rather than implementing a common skip/opt-in guard.
- [ ] P2: define connection readiness/timeout diagnostics and avoid sharing mutable live test state across parallel scenarios.

No credentials or live settings need to be copied into the review.

## Cleanup and reproducibility

- [x] The cleanup scope deletes tracked records in reverse order, and existing scenarios use purpose-specific test data.
- [x] Queue deletion tests remove confirmed-deleted items from tracking through `MarkDeleted`; queue cleanup uses ReleaseToQueue/RemoveFromQueue. A bounded second pass handles dependent/transient failures; unresolved failures remain visible in test output.
- [ ] Track every created side effect, including failures partway through arrange/act, and verify cleanup for queue, batch, and security fixtures.
- [ ] Surface cleanup failures in CI results. The current scope logs them to `TestContext.Error` and continues; a green test alone does not prove no data was left behind.
- [ ] Add a narrowly scoped cleanup utility for abandoned records with identifiable run prefixes/ids.
- [ ] Record scenario inputs, environment/build identity, and package/SDK versions in run artifacts without secrets.

## CI and compatibility monitoring

Existing [PR CI](../../../.github/workflows/ci.yml) builds the integration project but deliberately runs only unit tests. It does not execute live parity.

P2 implementation order:

1. Document a reproducible local smoke/core command, explicit opt-in/readiness/missing-settings behavior, and sandbox/restricted-user requirements while closing the three P1 findings.
2. Add manual dispatch using repository secrets, exact category filters, timeouts, and failing cleanup reporting; verify the existing Ubuntu workflow's Windows-style test-project path.
3. Add scheduled runs and require recorded supported-subset parity before release.
4. Retain TRX and focused mismatch reports; make expected behavior changes explicit in reviewed tests/baselines.

Current categories include `Parity`, `ParitySmoke`, `Exceptions`, `QueryBase`, `QueryExpression`, `OrganizationRequests`, `Metadata`, `Security`, `QuerySemantics`, `SecurityIdentity`, `PagingAggregates`, `MutationConsistency`, and `Queues`. They are not yet a uniform `ParityCore`/`ParityQuery`/`ParitySecurity` scheme; `Parity` alone does not include every existing fixture. Define and document the final filters before scheduling subsets. Run credentialed jobs through a trusted workflow path.

A recurring run should detect Microsoft service changes through exact contract differences. Do not automatically bless changed results or require a large baseline framework before useful scheduled tests can run.

## Evidence anchors

- [Dual execution harness](../CloudAwesome.Xrm.Simulate.Integration/ParityTesting/DataverseParityHarness.cs)
- [Scenario contract](../CloudAwesome.Xrm.Simulate.Integration/ParityTesting/DataverseParityScenario.cs)
- [Cleanup implementation](../CloudAwesome.Xrm.Simulate.Integration/ParityTesting/LiveDataverseCleanupScope.cs)
- [Current connection configuration](../CloudAwesome.Xrm.Simulate.Integration/DataverseConnectionManager.cs)
- [Core request parity examples](../CloudAwesome.Xrm.Simulate.Integration/OrganizationRequestTests/CoreOrganizationRequestParityTests.cs)
