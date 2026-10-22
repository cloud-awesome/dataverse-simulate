# Architecture, tests, documentation, CI/CD, and publishing

Date reviewed: 2026-10-22

## Current assessment

The instance-isolation refactor and several proposed platform components have been delivered. The next architectural work should close inconsistencies between the existing components and make their supported behavior clear. A wholesale context/query/request rewrite is not a prerequisite for the next release.

Use the shared [P1-P7 sequence](00-executive-summary.md#updated-order-of-work): correctness and live parity first, release readiness next, then deeper semantics and ergonomics.

## Architecture delivered

- [x] `OrganisationServiceSimulator` and `ServiceProviderSimulator` use per-instance contexts held in `ConditionalWeakTable` instances.
- [x] `.Simulated()` rejects an object that was not initialized through `.Simulate()`.
- [x] Unit tests prove separate service providers retain their own seeded data, resolved services, telemetry, and audit state.
- [x] Factory-created organization services reuse provider data/audits without reinitializing the store.
- [x] Separate relationship state exists in `MockedRelationshipDataStore`; it is no longer represented by mutating a retrieved target's `RelatedEntities`.
- [x] Metadata DTOs/runtime indexes, a typed security calculator/enforcer, and queue services now exist.
- [x] `ExecuteTransaction` snapshots/restores entity, relationship, audit, and security state on failure.
- [x] QueryExpression is the common execution representation for FetchXML and QueryByAttribute conversions.

The entity store is still a logical-name dictionary of lists. This is acceptable until measurements show a need for id/key indexes. Query cloning protects the tested top-level result mutations, but `EntityCloner.Clone` copies attribute values and related collections shallowly; deep isolation of mutable SDK values is not established.

### Remaining architectural work

- [ ] P1: apply security consistently to linked data and service-factory identities.
- [ ] P1: route converted FetchXML through the same metadata validation contract as QueryExpression.
- [ ] P1: define one audit policy for direct SDK calls, request equivalents, nested batch requests, failures, and rollback. Direct delete/association/retrieve-multiple paths currently do not all write audits; some composed requests produce both inner-operation and request audit entries.
- [ ] P1/P4: keep shared mutation paths consistent for metadata, timestamps, ownership, security, processors, and rollback. Queue services currently mutate the data store directly and enforce only a subset of metadata rules.
- [ ] P4: establish the supported isolation contract for seed data, returned entities, mutable SDK attribute values, and reused options/security models.
- [ ] P5: consider extracting a reusable internal context or request pipeline only where these fixes expose duplication. The existing `SimulationContext` holders and `SimulatorOptions` already serve different purposes: runtime state versus configuration.
- [ ] P7: measure before adding entity/alternate-key indexes, caching XML, or restructuring query execution for speed.

A richer request pipeline or intermediate query model remains a design option requiring discussion, not an agreed prerequisite. Any plugin context component must remain specific to the unit under test; automatic plugin dispatch is excluded.

## Request extensibility and setup API

- [x] Public typed custom handlers and executable documentation examples exist.
- [x] Public custom registration rejects replacement of registered built-ins.
- [x] Request-name/record-id failure settings are consumed by supported request paths; this is no longer limited to direct CRUD alone.
- [x] `.Simulated().Metadata()`, `.SecurityModel()`, and `.Queues()` facades exist; metadata can be loaded after initial simulation.
- [x] Typed security and queue builders, XML role import, and early-bound data retrieval helpers reduce setup ceremony.

Remaining API decisions (P5 unless a consumer needs them sooner):

- [ ] A coherent builder over `SimulatorOptions`, with concern-based grouping where it improves discoverability.
- [ ] Document permissive behavior and explicitly configured fidelity; named permissive/strict/live-parity/legacy presets do not currently exist.
- [ ] Default unknown/custom-API handling by request name and convenient response builders.
- [ ] Whether public built-in replacement is needed at all. Do not add an unsafe override merely because it appeared in the original proposal.
- [ ] Clarify `Queues()` runtime operations versus `SimulatorOptions.Queues` setup; the originally proposed facade `.Queues().WithQueue(...)` is not implemented.

## Unit tests and verification

On 2026-10-22:

- **560 passed, 4 skipped, 0 failed** in the unit project with `dotnet test CloudAwesome.Xrm.Simulate.Test/CloudAwesome.Xrm.Simulate.Test.csproj --no-restore`.
- The full four-project Release build succeeded, reporting **0 warnings and 0 errors** on this incremental build. This does not establish that a clean rebuild is warning-free.
- **40 integration tests** were discovered; no live execution result is claimed.

### Regression work delivered

- [x] Requested-id retrieval, sparse updates, missing/duplicate record faults, disassociation, unsupported requests, and direct request equivalents.
- [x] Service-provider and resolved-service state isolation.
- [x] Unselected filter/join/order attributes, selected linked columns, aliases, distinct, paging metadata, and aggregate count/limit paths.
- [x] Security depth decisions, base-row filtering, shares/team membership, request denial, and security fixture ergonomics.
- [x] Queue setup/operations/responses, metadata loading/validation/retrieval, batch continuation, and transaction rollback including security state.
- [x] Executable examples under `CloudAwesome.Xrm.Simulate.Test/DocumentationCode`.

### Remaining quality work

- [ ] P1: add regression coverage for the concrete findings in [01](01-simulation-parity.md) and [08](08-query-execution-phase-review.md).
- [ ] P1: reconcile the four ignored empty TODO tests against the newer projection/link tests. Do not treat skips as four proven outstanding feature gaps.
- [ ] P2: compare live fault codes/messages, response keys/types, and mutation side effects without normalizing away the property under test.
- [ ] P3: review a fresh coverage report and set useful coverage/quality thresholds. Having coverlet configured is not evidence of a measured coverage percentage or enforced minimum.
- [ ] P3: inventory clean-build nullable/analyzer warnings and adopt incremental gates rather than assuming the historical warning count still applies.
- [ ] Later: use mutation testing selectively for query/security decisions if it adds confidence beyond focused regressions.

## Documentation

The local README still makes broad mock/audit claims, while `docs` contains only a placeholder and the full documentation is hosted separately. Public documentation freshness was not verified in this update.

P3 deliverables:

- [ ] Publish the [current support matrix](01-simulation-parity.md), with partial behavior and live-verification status, in user-facing docs.
- [ ] Document metadata contract/loading, security role/team/share setup, queues, batches, transaction scope, paging limits, and current unsupported behavior.
- [ ] Document factory identity limitations until corrected; distinguish context fixtures from automatic plugin execution.
- [ ] Keep docs examples executable and expand the existing example tests for new supported APIs.
- [ ] Add contribution guidance for a request handler, unit regressions, and the corresponding live parity scenario.
- [ ] Document sandbox configuration/cleanup, opt-in test commands, release checks, and behavior-breaking accuracy fixes.
- [ ] Add benchmark interpretation guidance after actual benchmarks exist (P7).

## CI/CD

The old statement that no workflow exists was incorrect: the workflow is at repository root, outside this solution directory.

[Existing CI](../../../.github/workflows/ci.yml):

- [x] Trigger on pull requests targeting `main`.
- [x] Install .NET 8, restore and build the solution in Release.
- [x] Run only the unit test project, collect XPlat coverage, and upload test/coverage artifacts.
- [x] Keep live integration execution out of the default unit-test run.

Configuration in source does not prove recent successful hosted runs or branch-protection settings. Check Linux path portability and artifact output when validating the next hosted run.

Remaining automation:

- [ ] P2: manual/scheduled live parity workflow with sandbox secrets and explicit categories; retain run results and cleanup failures.
- [ ] P3: validate `dotnet pack` output and package metadata; add main/tag/release checks as appropriate.
- [ ] P3: publish only reviewed versions from a documented release path with source/symbol artifacts and recorded parity results.
- [ ] P3: decide the role of `pipelines/build-and-release.yaml`. It currently restores NuGet and leaves build/test/release as a TODO; it is not a functioning release pipeline.
- [ ] P3: validate user-facing docs/examples in their actual repository/publishing workflow.

## Packaging and versioning

The local library project targets **net8.0**, declares version **1.1.1**, and references Dataverse Client 1.2.10 and NSubstitute 5.3.0. Published package state was not checked. A root README and LICENSE exist, but packaging them is separate work.

P3 checklist:

- [ ] Package README, license expression/file, repository/project URLs, description/tags, and icon if wanted.
- [ ] XML documentation, repository/source information, Source Link or equivalent source support, and symbol packages.
- [ ] Reproducible version selection and package validation; inspect actual `.nupkg`/`.snupkg` contents.
- [ ] Semantic versioning and a policy for changes that improve Dataverse accuracy while breaking existing simulated expectations.
- [ ] Confirm supported consumer frameworks against SDK dependencies and current .NET support; do not carry forward the old suggestion to add `net6.0` without a consumer requirement.

## Evidence anchors

- [Simulator and request registration](../CloudAwesome.Xrm.Simulate/OrganisationServiceSimulator.cs)
- [Provider isolation and shared state](../CloudAwesome.Xrm.Simulate/ServiceProviderSimulator.cs)
- [Options and existing setup surface](../CloudAwesome.Xrm.Simulate/SimulatorOptions.cs)
- [Library package configuration](../CloudAwesome.Xrm.Simulate/CloudAwesome.Xrm.Simulate.csproj)
- [Provider regression tests](../CloudAwesome.Xrm.Simulate.Test/ServiceProviderSimulatorTests.cs)
- [Transaction rollback implementation](../CloudAwesome.Xrm.Simulate/ServiceRequests/OrganizationRequests/ExecuteTransactionRequestHandler.cs)
