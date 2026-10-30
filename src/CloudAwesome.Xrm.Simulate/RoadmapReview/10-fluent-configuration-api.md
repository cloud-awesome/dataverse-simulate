# Fluent configuration API review and implementation plan

Date reviewed: 2026-10-30

Status: **proposed design; no library implementation in this review**.

## Recommendation and roadmap placement

Deliver a complete, concern-based configuration API through `.Simulate()` and `.Simulated()` before finalizing the documentation rewrite. Keep `SimulatorOptions`, `ISimulatorOptions`, existing extension signatures, and existing inspection/setup facades available for compatibility.

The public contract should have two forms:

- **Initial setup:** `.Simulate(configure: simulation => simulation.With...(...))`, returning the same SDK interface as today.
- **Subsequent setup:** `.Simulated().Configure(simulation => simulation.With...(...))`, with direct `.Simulated().With...(...)` conveniences for common changes.

Both forms use the same configuration engine. Shortcuts and grouped callbacks configure the same settings; consumers should never need a different setup mechanism to reach an existing option.

This brings the configuration portion of [P5](00-executive-summary.md#updated-order-of-work) forward as a dependency of P3 documentation. Include the plugin-context wiring necessary to make the advertised configuration work. P2 live-parity automation and P4/P6 semantic expansion retain their own scope. This proposal does not require finishing every deeper simulator behavior before updating the documentation.

The separate documentation repository has not been inspected here. Its reported rewrite is the reason to settle the API now, not evidence that particular published pages or samples are current. No package/version commitment is made.

## Current implementation and gaps

The current code already supplies useful pieces of the proposed API. Reuse them rather than introducing parallel metadata, security, queue, or request engines.

| Area             | Current implementation                                                                                                                                      | Work required for the fluent API                                                                                                                   |
|------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| Entry points     | Organization service and provider `.Simulate(options)` return SDK interfaces; `.Simulated()` resolves instance state.                                       | Add configuration callbacks without changing return types or losing existing call shapes.                                                          |
| Options          | `SimulatorOptions` and `ISimulatorOptions` expose 13 flat properties.                                                                                       | Cover every property, group new configuration by concern, and retain the compatibility surface.                                                    |
| Data             | Initial dictionaries/relationships and mutable `.Data().Add/Get` fixture access. Dictionary `Add` replaces the store rather than appending.                 | Add explicit, additive fluent seeding with an ownership/duplicate policy; preserve raw access semantics.                                           |
| Metadata         | Initial `Metadata`, plus organization-service `.Metadata().Load(path)` mutating the options object.                                                         | Add path/model shortcuts and provider access; preserve optional contract 1.0 loading and split-file behavior.                                      |
| Security         | Typed model and organization-service `.SecurityModel()` support BUs, users, teams, roles, XML import, memberships, assignments, and shares.                 | Integrate these capabilities into grouped setup and provider access; distinguish selecting the authenticated user from adding security principals. |
| Queues           | `SimulatedQueueModel.WithQueue/WithQueueItem` seeds through options; both facades expose runtime queue operations.                                          | Make queue fixture configuration discoverable under data; retain `.Queues()` as runtime operations.                                                |
| Requests         | Typed `.CustomOrgRequests().Add<TRequest>()`, processors, and request failure settings.                                                                     | Initial and runtime registration, grouped behavior configuration, and consistent provider/factory propagation.                                     |
| Services         | Provider resolves five fixed SDK service types; failure settings can make individual resolutions return null.                                               | Group missing-service behavior and provide registration of additional service types.                                                               |
| Plugin execution | `PluginExecutionContextMock` exists, but several values are read-only/generated; the resolver copies only a subset.                                         | Make the complete supported context configurable and wire it into resolved contexts.                                                               |
| State sharing    | Factory services share data/audits and retain their own caller identities. Each factory-created service currently creates its own request-handler registry. | Share configured handlers across the provider's service family, while retaining per-service identity.                                              |
| Clock            | `IClockSimulator.Now` is sampled at initialization into `SystemTime`.                                                                                       | Expose it fluently and define explicit subsequent changes; do not silently promise a ticking clock.                                                |

Important compatibility details found in source:

- Operations read some options live. Existing tests change metadata/failure settings after initialization. Replacing every legacy options object with an immutable snapshot would change observable behavior.
- Reading `.SecurityModel()` currently creates a model when none exists. New configuration/inspection entry points should avoid enabling security merely through discovery; retain the existing method's behavior.
- The provider stores context/failure settings in its data service as well as retaining options. Changing a property alone does not establish consistent propagation.
- Existing `.Simulate()` creates and returns a substitute; callers must retain the returned SDK interface. Keep the existing receiver-registration behavior, including provider lookup through its registered receiver.

## Public API shape

All examples below are **proposed API**, not currently executable documentation. Final names and overloads must pass the first implementation stage before they are copied into public docs.

### Initial setup and compatibility options

```csharp
IOrganizationService service = null!;

service = service.Simulate(configure: simulation => simulation
    .WithUser(user)
    .WithData(account, contact)
    .WithMetadata("TestMetadata/entity-metadata.json")
    .WithClock(new MockSystemTime(
        new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc))));

// Existing options form the baseline; explicitly configured fluent values win.
service = service.Simulate(
    options: existingOptions,
    configure: simulation => simulation.WithUser(otherUser));
```

Keep the existing SDK return types. Do not change parameterless `.Simulate()` into a builder that requires `.Build()`, or add implicit conversions between a builder and SDK services.

Proposed types:

- `SimulationBuilder`: shared identity, data, metadata, security, clock, and request configuration.
- `OrganizationSimulationBuilder`: shared configuration for a standalone organization service.
- `ServiceProviderSimulationBuilder`: shared configuration plus service resolution and plugin context.
- Dedicated data, metadata, security, service, request, and plugin builders under these roots.

Use concrete builder classes with strongly typed fluent returns. Shared implementation must not make `.WithUser(...).WithPluginContext(...)` lose the provider builder type. Avoid adding a public builder interface for every class unless an actual extension/test requirement needs it.

### Callback overloads without positional-null ambiguity

Adding `Simulate(Action<...>)` beside `Simulate(ISimulatorOptions?)` makes existing `.Simulate(null)` ambiguous. Prefer a trailing optional **named** `configure` parameter, retaining the old CLR signatures as forwarding methods.

Illustrative organization-service declarations:

```csharp
// Existing CLR signature retained; optional defaults move to the new overload.
IOrganizationService Simulate(
    this IOrganizationService service,
    ISimulatorOptions? options,
    MockedEntityDataService? dataService);

IOrganizationService Simulate(
    this IOrganizationService service,
    ISimulatorOptions? options = null,
    MockedEntityDataService? dataService = null,
    Action<OrganizationSimulationBuilder>? configure = null);
```

Use the equivalent two-/three-parameter pattern for providers: retain `(provider, options)` and add `(provider, options = null, configure = null)`. Both old and new overloads delegate to one implementation.

This keeps existing compiled calls resolvable and lets ordinary source calls bind to either the forwarding or extended overload. A small in-memory C# compiler probe during this review accepted parameterless, positional-null, positional options/data-service, named options/data-service, and named callback forms with this pattern. It was not a build against the actual library or a binary compatibility test.

Stage 1 must verify the real extension methods in the solution's C# 12/net8.0 configuration and against an already compiled consumer. Optional-parameter reflection metadata changes on the retained overloads; inspect that explicitly. Do not publish the callback API until these checks pass.

### Grouped configuration

Keep a small root set for common tasks. Use callbacks for larger fixtures and advanced settings:

```csharp
service = service.Simulate(configure: simulation => simulation
    .WithUser(user)
    .WithData(data => data
        .WithEntities(account, contact)
        .WithRelationships(relationships)
        .WithQueues(queues => queues
            .WithQueue(queueId, name: "Support")))
    .WithMetadata(metadata => metadata
        .Load("TestMetadata/entity-metadata.json"))
    .WithSecurityRole("Account reader", role => role
        .CanRead("account", PrivilegeDepthEnum.Organization))
    .WithSecurity(security => security
        .AssignRoleToUser("Account reader", user.Id))
    .WithRequests(requests => requests
        .WithProcessor(processorType, processor)
        .WithFailure(failureSetting)));
```

`WithSecurityRole` defines a role and enables configured security; it does **not** silently assign that role. Assignment identifies the user/team explicitly. Root `.WithUser(user)` selects the authenticated identity. `.WithSecurity(security => security.WithUser(...))` adds a graph principal without switching identity.

### Provider and isolated plugin setup

```csharp
IServiceProvider provider = null!;

provider = provider.Simulate(configure: simulation => simulation
    .WithUser(user)
    .WithData(account)
    .WithClock(new MockSystemTime(nowUtc))
    .WithPluginContext(plugin => plugin
        .WithMessage("Update")
        .WithStage(20)
        .WithPrimaryEntity("account", account.Id)
        .WithInputParameter("Target", update)
        .WithPreImage("Before", before)
        .WithInitiatingUser(initiatingUserId))
    .WithRequestHandler<CalculateScoreRequest>((request, context) =>
        new OrganizationResponse
        {
            Results = new ParameterCollection { ["Score"] = 100 }
        })
    .WithServices(services => services
        .WithService<IMyDependency>(dependency)));

pluginUnderTest.Execute(provider);
var created = provider.Simulated().Data().Get("task");
var traces = provider.Simulated().Logs().Get();
```

Plugin execution remains explicit through `IPlugin.Execute(provider)`. Entity processors remain a separate extension point. Context configuration does not register plugins or dispatch a pipeline.

### Subsequent configuration through `.Simulated()`

```csharp
service.Simulated()
    .WithClock(new MockSystemTime(laterUtc))
    .WithData(nextAccount)
    .WithRequestHandler<CalculateScoreRequest>(CalculateScore);

provider.Simulated().Configure(simulation => simulation
    .WithData(nextAccount)
    .WithPluginContext(plugin => plugin
        .WithMessage("Create")
        .WithPrimaryEntity("account", nextAccount.Id)
        .WithInputParameter("Target", nextAccount)));
```

`Configure` applies one validated change set. Each direct `With...` call applies a separate change set and returns the same facade, so chained direct calls are not atomic as a group. Neither form clears data, audit, logs, telemetry, or service-bus captures.

Preserve existing `.Data()`, `.Audit()`/`.Audits()`, `.Logs()`, `.Telemetry()`, `.ServiceBus()`, `.Metadata()`, `.SecurityModel()`, `.Queues()`, and `.CustomOrgRequests()` methods and return types. Add the missing metadata/security/request facades on providers. Fluent methods return the simulation facade or builder; inspection methods continue returning their existing services.

## Configuration coverage and concern groups

This inventory is the definition of “full fluent API” for this implementation. It covers the existing configuration contract and the agreed setup gaps, rather than promising all future Dataverse features.

| Concern                  | Proposed root shortcuts                                                                                                         | Grouped capabilities / existing source                                                                                                                                                                                                                                            |
|--------------------------|---------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Identity and environment | `WithUser(Entity)`, `WithBusinessUnit(Entity)`, `WithOrganization(Entity)`                                                      | Covers `AuthenticatedUser`, `BusinessUnit`, and `Organization`; explicit `systemuser`/`businessunit`/`organization` validation. Keep distinct from adding graph principals.                                                                                                       |
| Data                     | `WithData(params Entity[])`, `WithData(IEnumerable<Entity>)`, `WithData(dictionary)`, `WithData(Action<SimulationDataBuilder>)` | Entities including early-bound types; relationship fixtures; `WithQueues(model/configure)`; explicit upsert for replacing fixture rows. Covers `InitialiseData`, `InitialiseRelationships`, `Queues`.                                                                             |
| Metadata                 | `WithMetadata(path)`, `WithMetadata(model)`, `WithMetadata(configure)`                                                          | Use existing loader/model; inline/split files and contract checks. Whole-model replacement, not implicit merging. Explicit `WithoutMetadata()` for removal.                                                                                                                       |
| Security                 | `WithSecurity(model/configure)`, `WithSecurityRole(name, configure)`                                                            | BUs, users, owner/access teams, roles/privileges, XML file/list/directory import, assignments/removals, memberships, shares/revocations. Reuse the typed model; reject unsupported custom `ISecurityModel` implementations. Explicit `WithoutSecurity()` for permissive behavior. |
| Clock and calendar       | `WithClock(IClockSimulator)`, `WithClock(DateTime)`, `WithEnvironment(configure)`                                               | Fixed simulated time and `WithEnvironment(environment => environment.WithFiscalYearSettings(settings))`, covering `ClockSimulator` and `FiscalYearSettings`. Use UTC examples; preserve the existing date-value interpretation.                                                   |
| Request behavior         | `WithRequestHandler<TRequest>(handler)`, `WithRequests(configure)`                                                              | Typed handlers; entity processors by `ProcessorType`; request-name/optional-record-id failure settings; explicit removal of custom registrations/processors/failures. Covers `EntityProcessors` and request entries in `FakeServiceFailureSettings`.                              |
| Provider services        | Provider-only `WithServices(configure)`                                                                                         | Additional service registrations, plus missing tracing, telemetry, endpoint notification, plugin context, and organization-service factory behavior. Covers all five service flags in `FakeServiceFailureSettings`.                                                               |
| Plugin execution         | Provider-only `WithPluginContext(mock/configure)`                                                                               | Complete supported `IPluginExecutionContext` fixture, including parameters, images, identities, correlation/operation/request details, parent and transaction context. Covers `PluginExecutionContextMock`.                                                                       |

The root should not expose every security graph operation. IntelliSense at `.WithSecurity(...)`, `.WithData(...)`, `.WithRequests(...)`, `.WithServices(...)`, and `.WithPluginContext(...)` supplies the detail. Use a small `WithEnvironment(...)` group for identity/clock/calendar details, including fiscal settings; the root identity and clock shortcuts forward into that group.

Every member of both `SimulatorOptions` and `ISimulatorOptions` must appear in the final mapping and have an executable fluent example. Do not add these new groups as required interface properties: that would break third-party `ISimulatorOptions` implementations. Stop growing the flat compatibility bag for new features.

## Behavioral contracts

### Precedence, ordering, and failure

1. Current defaults apply first, supplied legacy options provide the baseline, and the callback overrides only what it explicitly touches.
2. Repeated scalar settings use the last explicit value. Repeated data/relationship/security/queue additions accumulate; duplicate record/definition conflicts fail with useful identities. Existing idempotent membership/assignment behavior stays idempotent. Typed handler duplicates, including built-ins, fail rather than overwrite.
3. Configuration order should not determine dependency resolution. Collect declarations, then validate the complete fixture and initialize it in dependency order. Selecting a user before defining its BU/role is valid if the finished fixture is consistent.
4. Validate/load into temporary configuration and simulator-owned fixture state before committing. Invalid paths, graph references, duplicates, or unsupported service registrations leave an existing simulation unchanged. No partially seeded store or registered provider should escape initial setup failure.
5. Callback exceptions discard the pending simulator changes. External effects performed by user callbacks/delegates cannot be rolled back. Configuration is an arrange-phase operation; concurrent execution/configuration is outside the contract.
6. Reject null arguments explicitly. Use named `Without...`/removal methods to disable optional capabilities rather than overloading null with reset semantics. Keep group callback and value overloads to the useful minimum.

### Data and ownership

New `.WithData(...)` is additive fixture seeding. It bypasses CRUD permissions, metadata-required-field validation, processors, timestamps, and auditing, as fixture setup does today. Structural fixture checks still apply. Queue/security seeders retain their supported specialized setup rules.

- Do not implement fluent dictionary seeding by calling the existing store-replacing `Data().Add(dictionary)` on an initialized simulation.
- Require non-empty fixture ids and valid logical names on the new fluent path. Duplicate logical-name/id pairs fail; explicit grouped upsert handles intentional replacement. Configuration is not an alternate CRUD API.
- Clone supported mutable SDK entity values and fixture containers on the new path using the existing cloning contract. Preserve early-bound entity types so `.Data().Get<T>()` remains useful. Arbitrary application objects, delegates, processor instances, and custom service instances are not promised deep copies.
- Copy mutable security/queue/context configuration into simulation-owned structures. Reusing a new fluent fixture across independent simulations must not share memberships, role assignment state, queues, or parameter/image collections. Treat metadata as shareable only where its model is demonstrably immutable; otherwise copy mutable parts.
- Reconcile the selected user/BU/organization with security seeders once. Identical fixture identities are consolidated; conflicting attributes/BU definitions fail rather than being silently overwritten. Adding security also registers the selected user and BU in the graph without granting privileges.
- Existing raw `.Data().Add/Get` remains mutable, with its existing dictionary-replacement behavior. Legacy options-only seeding retains existing reference behavior; do not silently impose the new fluent ownership contract on it.

### Identity and security

`WithUser` sets the authenticated connection/default plugin identity; it does not enable security. Supplying a security model/role or making an explicit security declaration enables security. Merely accessing the new fluent configuration or reading a current-configuration view must not enable it.

Preserve the scoped P1 identity contract: factory null selects simulator-local SYSTEM, `Guid.Empty` selects the provider/plugin user, explicit ids select seeded callers, and `WhoAmI` reports the authenticated connection. SYSTEM's bypass flag remains separate from its id. No automatic privilege grants or unknown-user bypass.

Subsequent identity changes update the default for subsequent work and factory creation. Previously created factory services retain their bound caller, including services created with `Guid.Empty`; explicit caller/SYSTEM services must never be rebound as a side effect. Changes to a provider's data/security/handlers remain visible across its service family.

Keep connection identity, effective service caller, and `InitiatingUserId` distinct. For this release, plugin `UserId` follows the configured provider/plugin user; do not introduce an unrelated context-only `UserId` override that disagrees with factory `Guid.Empty`. If independent plugin/connection identity becomes necessary, design it with dedicated factory/identity fixtures before adding it.

Security configuration delegates to the existing enforcement engine. It does not imply field-level security, modernized BU ownership, new cascade behavior, or exact platform fault parity.

### Metadata and clock

Metadata stays optional and uses the existing contract 1.0 consumer. `.WithMetadata(path)` and legacy `.Metadata().Load(path)` must reach the same configuration used by direct operations, queries, requests, and factory services. Loading/replacing metadata affects subsequent operations; it does not rewrite or retroactively validate every seeded row. Removing metadata restores the existing permissive behavior, including the existing failure contract for metadata retrieval without a model.

`WithClock(IClockSimulator)` samples `Now` once per application, matching current initialization. The `DateTime` convenience has the same fixed-time effect. Subsequent changes affect future timestamps, relative-date queries, and newly resolved context operation time; they do not rewrite existing records. Continuously advancing clocks and timezone/fiscal parity expansion are separate work.

### Request handlers and provider services

Use a provider-owned custom-handler registry shared by its factory services. Registration through initial setup, provider `.Simulated()`, or a factory service must be visible to existing and subsequently created services in that family. Standalone simulations retain separate registries. Preserve the registered caller's data/security context when invoking a shared handler, including nested batches/transactions.

Keep typed registration and the existing public delegate. Preserve built-in replacement protection and unsupported-request exceptions. Request-name Custom API dispatch, catch-all success handlers, and response builders remain separate extensions; “full fluent” does not require inventing those behaviors.

Provider `.WithServices(...)` supports custom application service types and explicit missing-service settings. Initially reserve the five SDK service types already managed by the simulator; reject attempts to replace them with arbitrary instances. This preserves shared organization-service state and log/telemetry/service-bus inspection. A later explicit replacement policy can address capture semantics if a real consumer requires it.

For additional services, begin with instance and factory registration with a documented lifetime (one lazy instance per simulation). Reject duplicate registrations and recursive resolution with clear errors. Missing-service behavior and registrations affect subsequent `GetService` calls; they cannot revoke references already handed to plugin code. Unknown unregistered types retain the current exception behavior.

### Plugin context completeness and lifetime

The context builder must cover message/entity/id, stage/mode/isolation/depth, input/output/shared parameters, pre/post images, initiating user, organization name, request/correlation/operation ids, operation time, owning extension, offline/playback flags, transaction state, and parent context. Identity fields derived from the provider are configurable through the root identity methods, rather than duplicated conflicting setters.

Wire every advertised setting into `PluginExecutionContextSimulator`; a fluent setter that never reaches `GetService(typeof(IPluginExecutionContext))` is incomplete. Preserve legacy defaults when no new configuration is supplied. Support explicit ids/flags currently generated or read-only in `PluginExecutionContextMock` through an internal fixture representation; retain the old mock overload as an adapter.

Each applied plugin-context fixture starts a new execution frame. Repeated context resolution within that frame shares live parameter/image collections so plugin writes remain observable. A subsequent `WithPluginContext` starts a new frame and does not clear database/captures. Previously obtained context references retain their old frame. Parent/transaction flags describe the isolated test fixture; setting them does not start an SDK transaction or dispatch parent/child plugins. Include a cycle guard in any parent-context copying.

## Internal implementation approach

Introduce an internal concern-based configuration representation and an application engine, separate from mutable runtime stores. Public builders collect intent; they do not execute CRUD or instantiate a second simulator.

The internal configuration should group identity/environment, data seeds, metadata, security, service registrations/availability, request behavior, and plugin execution. Keep a small runtime state owner for a standalone service or provider family, with shared data/audits/configuration/custom registrations and per-service caller views. Extract only what these requirements need; a general DI container or wholesale request/query rewrite is unnecessary.

Two compatibility paths must be explicit:

- **Legacy options without a callback:** retain today's live options/reference behavior, including existing facade updates. Adapt access internally without detaching fields that operations currently read live. Do not reinterpret unsupported legacy options as new functionality.
- **New fluent setup, optionally based on options:** snapshot the supplied baseline into owned configuration, then apply the callback. Later changes go through `.Simulated()`; mutating the original baseline does not change the new fluent simulation. Document this boundary and test it.

Route old facades into their simulation's configuration adapter so `.Metadata().Load(...)`, `.SecurityModel().WithRole(...)`, and `.CustomOrgRequests().Add(...)` remain coherent with the new fluent path. Factory-created services must observe the family configuration rather than stale captured copies. Reuse the existing built-in request engine; shared custom registration must not accidentally bind a handler to the first caller's view.

Unify dependency ordering where practical: materialize/load configuration, resolve identity and security declarations, set clock/calendar, seed ordinary/specialized fixtures and relationships in the required order, then publish services/context. The current organization-service and provider initialization sequences differ; prove equivalent results for both paths rather than adding a third sequence.

## Implementation stages and acceptance gates

Implement and review these stages separately, but treat all six as the fluent milestone before publishing the recommended patterns.

| Stage                                                | Deliverable                                                                                                                                  | Acceptance gate                                                                                                                                                                                                    |
|------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **1. Lock the public contract**                      | Real extension overloads, root/group builder skeletons, typed chaining, complete options coverage, and declared capability boundaries.       | Compile legacy/new call forms against the actual library; run an old compiled consumer; verify `ISimulatorOptions` implementers need no changes. Resolve reflection/overload issues before documenting signatures. |
| **2. Shared configuration and common setup**         | Internal adapter/application path; identity, data/relationships/queues, metadata, clock/calendar; both initial and subsequent configuration. | Initial/runtime setup yields equivalent supported behavior; invalid changes do not partially apply; baseline/fixture ownership and early-bound behavior are proven. Existing legacy setup tests remain green.      |
| **3. Security and request behavior**                 | Full existing security setup/import surface, processors/failures, shared custom registry, provider facade gaps.                              | Direct/provider/factory tests cover roles/teams/shares, isolated callers, handler propagation before/after service creation, duplicate/built-in protection, and batch/transaction invocation.                      |
| **4. Provider service configuration**                | Grouped service availability and additional-service registrations with explicit lifetime.                                                    | All five legacy service flags map correctly; custom resolutions/lifetimes/isolation work; reserved types and invalid registrations fail clearly; captures remain available.                                        |
| **5. Complete plugin fixtures**                      | Full context mapping, compatibility mock adapter, new execution frames, parent/transaction fixture support.                                  | Assert values returned through the resolved SDK context for every setting; exercise an actual plugin using parameters/images/services and inspect its writes/captures. Preserve identity/factory contracts.        |
| **6. Executable examples and documentation handoff** | New `DocumentationCode` examples, XML docs, migration/coverage tables, and coordinated changes in the separate docs repository.              | All shown samples compile/run; legacy examples remain valid; new recommended patterns and limitations are consistent across both repositories. Record Release build/unit results and any targeted live evidence.   |

### Verification required during implementation

- Compile old parameterless/options/null/named/custom-data-service calls, new callback calls, mixed options-plus-callback calls, and provider chains ending in provider-only methods. Keep the exact old extension/constructor/member signatures resolvable from an already built consumer.
- Exercise every compatibility option through its fluent equivalent. Compare meaningful behavior (stored records, resolved context, fault behavior, captures), rather than only testing builder property assignment.
- Test repeated group calls, scalar precedence, additions/removals, declaration order, no-op `Configure`, invalid change atomicity, and direct-call versus grouped-call boundaries.
- Reuse a baseline/model/entity/context fixture across independent simulations and verify the documented isolation differences between legacy and fluent paths. Include mutable SDK values, early-bound entities, parameters/images, role memberships, and service lifetimes.
- Verify existing/future factory services see metadata, security, failure, processor, clock, and handler changes without altering bound caller identities. Confirm independent provider families remain isolated.
- Keep existing CRUD/query/transaction regressions green. Setup API changes should not alter supported operation behavior, seed auditing, or raw fixture access.
- Extend `CloudAwesome.Xrm.Simulate.Test/DocumentationCode` with minimal service setup, grouped data/metadata/security, request handlers, options migration, and a complete isolated plugin example.
- Run the four-project Release build and unit suite after implementation. Use targeted live comparisons if identity/context or operation behavior changes; do not require a full live run for a purely additive setup wrapper or treat builder tests as new Dataverse parity evidence.

No solution build/unit/live suite was run for this markdown-only review. Historical roadmap counts are not fresh verification of this proposal.

## Documentation handoff and completion criteria

The documentation rewrite should use the initial callback for complete fixtures, root shortcuts for quick starts, and grouped callbacks for advanced setup. Explain `.Simulated()` as both subsequent configuration and inspection. Keep an options migration page and the old examples as valid alternatives.

The migration mapping must show all 13 existing properties, the difference between selecting a user and defining a security principal, explicit role assignment, additive fluent data versus raw dictionary replacement, fixed clock semantics, provider-only capabilities, and legacy-live versus fluent-owned configuration.

Publish only after these conditions are met:

- [ ] Both entry points expose all applicable existing settings with discoverable grouped builders and common shortcuts.
- [ ] Existing public signatures and existing consumer call forms remain supported; compatibility verification is recorded.
- [ ] Initial and subsequent configuration share one supported application path with the documented ownership/propagation rules.
- [ ] Security/metadata/request setup is reachable from providers, and configured handlers reach their factory services.
- [ ] Every advertised plugin setting is wired into the resolved execution context; plugin execution remains explicit.
- [ ] Executable samples and the support/limitations matrix agree with implementation.
- [ ] The separate documentation repository receives the reviewed API/examples and migration guidance before publication.
- [ ] Release/version choice is made against the actual published baseline, with any deliberate behavior changes called out.

Do not bundle strict/live-parity presets, automatic plugin dispatch, new metadata semantics, advanced queries, public built-in handler replacement, or package automation into this implementation simply because configuration now has a convenient home. The existing roadmap continues to track those decisions independently.

## Evidence anchors

- [Executive roadmap and priorities](00-executive-summary.md)
- [Existing setup/API architecture review](02-architecture-tests-docs-ci.md#request-extensibility-and-setup-api)
- [Metadata consumer contract and remaining scope](05-metadata-simulation.md)
- [Security and identity contract](06-security-model.md)
- [Options](../CloudAwesome.Xrm.Simulate/SimulatorOptions.cs), [options interface](../CloudAwesome.Xrm.Simulate/Interfaces/ISimulatorOptions.cs), and [initialization helpers](../CloudAwesome.Xrm.Simulate/SimulatorOptionsProcessor.cs)
- [Organization-service entry points](../CloudAwesome.Xrm.Simulate/OrganisationServiceSimulator.cs) and [provider entry points](../CloudAwesome.Xrm.Simulate/ServiceProviderSimulator.cs)
- [Organization facade](../CloudAwesome.Xrm.Simulate/DataServices/OrganisationServiceSimulated.cs) and [provider facade](../CloudAwesome.Xrm.Simulate/DataServices/ServiceProviderSimulated.cs)
- [Factory identity/shared data](../CloudAwesome.Xrm.Simulate/ServiceProviders/OrganisationServiceFactorySimulator.cs) and [data/fixture behavior](../CloudAwesome.Xrm.Simulate/DataServices/MockedEntityDataService.cs)
- [Plugin fixture](../CloudAwesome.Xrm.Simulate/ServiceProviders/PluginExecutionContextMock.cs) and [context wiring](../CloudAwesome.Xrm.Simulate/ServiceProviders/PluginExecutionContextSimulator.cs)
- [Custom registration](../CloudAwesome.Xrm.Simulate/ServiceRequests/CustomOrganizationRequestRegistry.cs), [handler dispatch/protection](../CloudAwesome.Xrm.Simulate/ServiceRequests/RequestHandlerRegistry.cs), and [failure settings](../CloudAwesome.Xrm.Simulate/DataStores/FakeServiceFailureSettings.cs)
- [Security model](../CloudAwesome.Xrm.Simulate/SecurityModel/SimulatedSecurityModel.cs), [runtime security facade](../CloudAwesome.Xrm.Simulate/SecurityModel/SimulatedSecurityModelService.cs), and [queue fixtures](../CloudAwesome.Xrm.Simulate/Queues/SimulatedQueueModel.cs)
- [Mutable SDK value cloning](../CloudAwesome.Xrm.Simulate/QueryParsers/EntityCloner.cs)
- [Executable documentation examples](../CloudAwesome.Xrm.Simulate.Test/DocumentationCode)
