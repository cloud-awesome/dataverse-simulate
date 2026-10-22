# Integration testing roadmap

Date reviewed: 2026-10-22

## Current position

The original connectivity-only stage is complete. A reusable dual-execution harness and several focused live/simulator parity suites now exist. The next priority is reliable execution and deeper coverage of the implemented features, not creating another harness.

**Verification boundary:** 40 integration tests were discovered on 2026-10-22 with `dotnet test CloudAwesome.Xrm.Simulate.Integration/CloudAwesome.Xrm.Simulate.Integration.csproj --no-build --no-restore --configuration Release --list-tests`. The integration project built successfully as part of the solution. No live sandbox tests were executed during this review; fixture presence is not a claim of a current live pass.

## Delivered harness

- [x] `DataverseParityScenario<TResult>` defines live arrangement, simulator options/arrangement, a shared operation, normalization, and equivalence assertions.
- [x] `DataverseParityHarness` executes live and simulated services within one scenario.
- [x] Shared `DataverseScenarioState` carries ids and values between arrangements.
- [x] `AfterLiveAct` supports live result/cleanup bookkeeping.
- [x] `LiveDataverseCleanupScope` tracks records, deduplicates them, and deletes in reverse order on disposal.
- [x] The base fixture disposes the cached Dataverse connection at fixture teardown.
- [x] Tests include focused snapshots/assertions for selected faults, query outputs, response shapes, and side effects.

The harness does not automatically capture/compare every exception or every SDK response field. Fault scenarios capture the contract they need themselves. Extend shared capture helpers only where this reduces repetition without hiding important assertions.

## Existing fixture inventory

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

The first query input/output implementation adds **23 `QuerySemantics` cases** for projection/identity, default/all FetchXML columns, OR/nested filters, metadata rejection, invalid operators, malformed input, and empty base projections with selected/filter-only links. Together with the original 40 cases, the project now contains 63 cases. After refreshing authentication and shortening fixture last names to meet the platform limit, the final focused live run on 2026-10-22 passed **all 23 cases**. It also corrected the empty distinct projection behavior and tightened rejection assertions to exclude unrelated service failures. This result covers this category, not a rerun of all integration fixtures. See the [query implementation notes](08-query-execution-phase-review.md#p1-query-inputoutput-implementation---22-october-2026) for the command and simulator exception limitations.

- [ ] Empty and explicit column sets, primary-id projection, and distinct result identity across query forms.
- [ ] FetchXML OR/nested filters, unsupported operators, multiple values, malformed XML, and metadata-backed unknown entity/attribute behavior.
- [ ] Paging across multiple pages: cookie continuation, count caps, total-count boundaries, deterministic order, and top/paging conflicts.
- [ ] Aggregate SDK output wrappers/types, empty input, distinct count, default/explicit limits, and grouping/timezones.
- [ ] Read security for inner/outer linked rows before aggregates/counts/paging.
- [ ] Service-factory caller identity and supported null/system behavior.
- [ ] Ownership changes, assignee validation, organization-owned tables, and stale owning fields.
- [ ] Queue add/remove/pick/release/route responses, side effects, duplicate targets, source moves, and faults. No queue-specific integration fixture exists yet.
- [ ] Upsert update/key paths and declared-key uniqueness; batch settings and transaction fault details/indexes as well as rollback.
- [ ] Successful sparse update, delete, assign, and relationship lookup/intersect effects, beyond the existing fault/payload checks.

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
- [ ] Track every created side effect, including failures partway through arrange/act, and verify cleanup for queue, batch, and security fixtures.
- [ ] Surface cleanup failures in CI results. The current scope logs them to `TestContext.Error` and continues; a green test alone does not prove no data was left behind.
- [ ] Add a narrowly scoped cleanup utility for abandoned records with identifiable run prefixes/ids.
- [ ] Record scenario inputs, environment/build identity, and package/SDK versions in run artifacts without secrets.

## CI and compatibility monitoring

Existing [PR CI](../../../.github/workflows/ci.yml) builds the integration project but deliberately runs only unit tests. It does not execute live parity.

P2 implementation order:

1. Document a reproducible local smoke/core command and sandbox requirements.
2. Add manual dispatch using repository secrets, exact category filters, timeouts, and cleanup reporting.
3. Add scheduled runs and require recorded supported-subset parity before release.
4. Retain TRX and focused mismatch reports; make expected behavior changes explicit in reviewed tests/baselines.

Current categories include `Parity`, `ParitySmoke`, `Exceptions`, `QueryBase`, `QueryExpression`, `OrganizationRequests`, `Metadata`, and `Security`. They are not yet a uniform `ParityCore`/`ParityQuery`/`ParitySecurity` scheme; `Parity` alone does not include every existing fixture. Define and document the final filters before scheduling subsets. Run credentialed jobs through a trusted workflow path.

A recurring run should detect Microsoft service changes through exact contract differences. Do not automatically bless changed results or require a large baseline framework before useful scheduled tests can run.

## Evidence anchors

- [Dual execution harness](../CloudAwesome.Xrm.Simulate.Integration/ParityTesting/DataverseParityHarness.cs)
- [Scenario contract](../CloudAwesome.Xrm.Simulate.Integration/ParityTesting/DataverseParityScenario.cs)
- [Cleanup implementation](../CloudAwesome.Xrm.Simulate.Integration/ParityTesting/LiveDataverseCleanupScope.cs)
- [Current connection configuration](../CloudAwesome.Xrm.Simulate.Integration/DataverseConnectionManager.cs)
- [Core request parity examples](../CloudAwesome.Xrm.Simulate.Integration/OrganizationRequestTests/CoreOrganizationRequestParityTests.cs)
