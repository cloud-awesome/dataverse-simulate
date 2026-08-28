# CloudAwesome.Xrm.Simulate maturity roadmap

Date reviewed: 2026-06-02

## Scope

This review covers the current local solution, the public documentation at https://docs.cloudawesome.uk/dataverse-simulate, the NuGet package page at https://www.nuget.org/packages/CloudAwesome.Xrm.Simulate, and the public GitHub issue list at https://github.com/cloud-awesome/dataverse-simulate/issues.

The roadmap is organized around the four areas requested:

1. Simulation parity
2. Architecture, unit testing coverage, documentation, CI/CD, and publishing
3. Integration testing against a live sandbox
4. Benchmarking

## Current position

The project already has a strong base for an open-source Dataverse test double:

- A fluent `.Simulate()` and `.Simulated()` API for `IOrganizationService` and `IServiceProvider`.
- In-memory entity, trace, telemetry, service bus, and audit stores.
- CRUD method simulation, `RetrieveMultiple`, `QueryExpression`, `FetchExpression`, and `QueryByAttribute` support.
- A useful number of query condition handlers, including many date-relative operators.
- Early support for aggregation, fiscal-year grouping, security role XML parsing, custom organization request handlers, service failure simulation, and plugin-service mocks.
- A passing test suite: `382` unit tests passed, `5` skipped, and `1` integration test passed when run locally with `dotnet test --no-restore`.

The main maturity gap is not just "more messages". The core maturity gap is exactness: the library needs reliable parity for common Dataverse behavior, exception types/messages, output shapes, security filtering, query semantics, and request side effects.

## Priority roadmap

### Phase 0 - Correctness foundations

Before adding large new features, fix known incorrect behavior in existing surfaces:

- Correct `Update` to merge incoming attributes into the existing entity, set `modifiedon` and `modifiedby`, run the update processor rather than the create processor, and audit the request.
- Correct partial-column `Retrieve`, which currently returns the first entity in the table rather than filtering by the requested id when `ColumnSet.AllColumns` is false.
- Implement `Disassociate`, which currently only handles configured failure simulation.
- Replace generic `Exception`, `InvalidOperationException`, and placeholder messages with Dataverse-shaped exceptions collected from live integration tests.
- Make unsupported `Execute` requests fail with a clear simulator error or configurable behavior rather than an internal `KeyNotFoundException`.
- Make service-provider simulation instance-scoped rather than backed by static stores.
- Add regression tests for each corrected behavior before expanding the surface area.

### Phase 1 - Core parity

Focus on the features most likely to unblock users moving from FakeXrmEasy or writing plugin/integration unit tests:

- Complete common `IOrganizationService` method parity: `Associate`, `Disassociate`, CRUD, retrieve, `RetrieveMultiple`, and audit behavior.
- Add first-class handlers for high-value `OrganizationRequest` types: `RetrieveRequest`, `UpdateRequest`, `DeleteRequest`, `AssociateRequest`, `DisassociateRequest`, `UpsertRequest`, `ExecuteMultipleRequest`, `ExecuteTransactionRequest`, `SetStateRequest`, `GrantAccessRequest`, `ModifyAccessRequest`, `RevokeAccessRequest`, `RetrievePrincipalAccessRequest`, `RetrieveSharedPrincipalsAndAccessRequest`, `AddMembersTeamRequest`, `RemoveMembersTeamRequest`, queue requests, and metadata retrieval requests.
- Rework query execution so filtering, joining, ordering, distinct, paging, projection, and aggregation match Dataverse behavior instead of LINQ convenience behavior.
- Treat security as a query and request pipeline concern, not just a create-time permission guard.
- Establish live parity fixtures for exceptions, response shapes, and edge cases.

### Phase 2 - Mature platform behavior

Add higher-value Dataverse behavior that advanced test suites need:

- Plugin pipeline simulation: registered steps, stage/mode/depth, pre/post images, shared variables, parent context, transaction behavior, and cascading plugin execution.
- Metadata-aware validation: required fields, primary id/name behavior, state/status, option set validation, lookup target validation, alternate keys, formatted values, and attribute type handling.
- Relationship metadata and cascade behavior for associate/disassociate/delete/assign.
- Row version and optimistic concurrency behavior.
- Realistic record ownership, teams, business units, sharing, and access-team behavior.
- Better data seeding ergonomics with metadata and early-bound type support.

### Phase 3 - Project maturity

Make the project easier to adopt, contribute to, and trust:

- Add GitHub Actions for build, unit tests, integration test gating, package validation, code coverage, and release publishing.
- Add package metadata, README, license, repository URL, symbol/source package generation, and deterministic versioning.
- Update docs so supported SDK messages and known limitations are always current.
- Add a parity matrix that maps SDK messages, query features, service-provider features, security features, and known gaps.
- Convert the issue backlog into milestones such as `v1.1 correctness`, `v1.2 org requests`, `v1.3 query parity`, `v1.4 security`, `v2.0 plugin pipeline`.

## Recommended immediate sequence

1. Create a parity test harness pattern in the integration project.
2. Fix existing incorrect CRUD behavior and exceptions.
3. Stabilize query pipeline semantics around selected-columns, links, paging, and alias behavior.
4. Implement `ExecuteMultiple` and direct request equivalents for existing direct SDK methods.
5. Introduce metadata and security as explicit architecture components.
6. Add CI and packaging automation once the correctness baseline is green.

## Evidence notes

- Public docs describe the library as using NSubstitute to mock `IOrganizationService` and plugin services, and list entity data, logs, telemetry, and simulator audits as the main data stores.
- Public docs currently list only create, update, delete, retrieve, and retrieveMultiple under supported SDK messages, while the local code also includes `AssignRequest`, `CreateRequest`, `RetrieveMultipleRequest`, `WhoAmIRequest`, and custom request registration.
- NuGet currently shows version `1.0.4`, last updated `2024-06-13`, targeting `.NET 8.0`.
- GitHub issues currently show `60` open issues, with recent items covering request extensibility, simulator options, failure configuration, telemetry alignment, metadata helpers, security fidelity, advanced aggregates, packaging/CI, ExecuteMultiple, and link-entity semantics.

