# Architecture, tests, documentation, CI/CD, and publishing

## Architecture review

### Scope and isolation

`OrganisationServiceSimulator` uses an instance-scoped context stored in a `ConditionalWeakTable`, which is a good direction. `ServiceProviderSimulator`, however, uses static shared data, logging, telemetry, service bus, and audit stores. That makes parallel tests and multiple simulated service providers risky.

Recommendation:

- Move `ServiceProviderSimulator` to the same per-instance context pattern as `OrganisationServiceSimulator`.
- Ensure `.Simulated()` throws when called on an object that was not created by `.Simulate()`.
- Avoid static mutable stores except immutable defaults.
- Add parallel test cases to prove two simulated service providers cannot affect each other.

### Core model

The current data store is a dictionary of logical name to `List<Entity>`. That is fine for early versions, but mature behavior needs more structure.

Recommended internal model:

- `SimulationContext`: current user, organization, clock, options, data store, metadata store, relationship store, security evaluator, request dispatcher, audit store, and plugin pipeline. (Potentially equates to the existing `SimulatorOptions`)
- `EntityStore`: records by logical name and id, with alternate key indexes later.
- `RelationshipStore`: relationship instances separate from `Entity.RelatedEntities`.
- `MetadataStore`: table, attribute, relationship, option set, and state/status metadata.

The above are agreed, the below need more discussion prior to implementation: 
- `RequestPipeline`: validation, security, pre-operation processors/plugins, core operation, post-operation processors/plugins, audit, and response construction.
- `QueryEngine`: shared internal query model for QueryExpression, FetchXML, and QueryByAttribute.

### Request extensibility

Custom organization request handlers are useful and documented. Improve the extension model:

- Allow custom handlers to opt into replacing built-in handlers only through an explicit unsafe/test-only API.
- Add "default unknown request handler" support for teams with many custom APIs.
- Add failure configuration by request name/type, not only direct service messages.
- Provide helper builders for common response shapes.

### Public API ergonomics

The current `SimulatorOptions` object is simple but will become crowded.

Roadmap:

- Keep `SimulatorOptions` for compatibility.
- Add a fluent builder: `.WithUser(...)`, `.WithData(...)`, `.WithMetadata(...)`, `.WithSecurityRole(...)`, `.WithClock(...)`, `.WithPluginContext(...)`, `.WithRequestHandler(...)`.
- Group options by concern: data, metadata, security, services, request behavior, plugin pipeline.
- Add documented presets: permissive, strict, live-parity, and legacy-compatible.

## Unit testing coverage

### Current state

Local test run:

- Unit tests: `382` passed, `5` skipped.
- Integration tests: `1` passed.
- Build warnings: numerous nullable warnings and a few warnings pointing at known incomplete behavior.

The suite is broad for condition handlers and query parser basics, but it has blind spots around exact Dataverse behavior and some currently broken direct methods.

### Priority regression tests

Add tests before or during fixes for:

- Partial-column retrieve filters by id.
- Update merges incoming attributes and preserves omitted attributes.
- Update sets `modifiedon` and `modifiedby`.
- Update missing record behavior.
- Disassociate removes relationship state.
- Unsupported `Execute` requests throw a clear simulator exception.
- Request handlers return the same response parameter names as SDK responses expect.
- Direct methods and equivalent `OrganizationRequest` handlers behave identically.
- Service provider simulations are isolated per instance.
- Query filters and links can reference attributes not in the final column set.
- Query projection returns primary id behavior correctly.
- Aliased link values use `AliasedValue`.
- Paging returns `MoreRecords`, count, and total-count values correctly.
- Security filters retrieve/retrieveMultiple and throws for write/delete/assign when insufficient.

### Live parity tests

Unit tests should prove internal behavior. Integration tests should capture live behavior.

For each supported message or query feature, keep a triad:

- Live capture test: execute against sandbox and record exact response or exception.
- Simulator parity test: execute the same scenario against `.Simulate()`.
- Contract test: compare normalized outputs, error codes, and messages.

Use normalization so irrelevant values do not make tests brittle:

- Normalize generated ids only when the id value is not the behavior under test.
- Normalize timestamps only when system time is not the behavior under test.
- Preserve exact exception message, error code, response parameter names, and output value types.

### Coverage and quality gates

Add:

- Coverage report for the unit test project.
- Mutation testing later for query/security components.
- Nullable warnings as warnings initially, then as errors once cleaned up.
- Analyzer rules for public API, package metadata, and test naming.
- A small set of smoke tests for docs examples.

## Documentation

### Current docs mismatch

The public docs describe the high-level API well, but the supported SDK message page is stale. It lists create, update, delete, retrieve, and retrieveMultiple, while the code also has built-in Execute handlers and custom request registration.

Docs roadmap:

- Add a parity matrix page.
- Add a "known limitations" page.
- Add per-feature examples only for live-verified behavior.
- Mark features as experimental where applicable, especially security and aggregates.
- Add upgrade notes and breaking-change policy.
- Add contribution docs with how to add a new request handler and its live parity tests.
- Add docs for integration tests and sandbox requirements.
- Add docs for benchmark interpretation once benchmarks exist.

## CI/CD

No `.github` workflow was present in the local solution tree.

Recommended GitHub Actions:

- `build.yml`: restore, build, unit test, collect coverage.
- `pack.yml`: validate NuGet package metadata and run `dotnet pack`.
- `integration.yml`: manually triggered or scheduled live sandbox parity tests using repository secrets.
- `release.yml`: tag-driven package publish to NuGet.
- `docs.yml`: build or validate docs examples if docs live in the repository.

Recommended gates:

- PRs must pass build and unit tests.
- Main branch must pass package validation.
- Integration tests should be optional for PRs but required before releases.
- Release should publish only from tags and should use deterministic versioning.

## NuGet publishing

The package currently has minimal metadata in the project file and NuGet shows version `1.0.4`, last updated `2024-06-13`.

Add package metadata:

- `PackageReadmeFile`
- `PackageLicenseExpression` or license file
- `RepositoryUrl`
- `RepositoryType`
- `PackageProjectUrl`
- `PackageTags`
- `Description`
- `PackageIcon`
- `GenerateDocumentationFile`
- `PublishRepositoryUrl`
- `EmbedUntrackedSources`
- Symbol package generation

Consider target frameworks:

- Keep `net8.0`.
- Consider `netstandard2.0` or `netstandard2.1` only if the Dataverse client and SDK dependencies support the desired consumer set.
- Consider `net6.0`/`net8.0` multi-targeting if users still test older plugin projects, but avoid broad targets if it increases SDK incompatibility.

Versioning:

- Adopt semantic versioning.
- Use prerelease versions for experimental parity features.
- Make a clear policy for behavior changes that make simulation more Dataverse-accurate but may break existing tests.

