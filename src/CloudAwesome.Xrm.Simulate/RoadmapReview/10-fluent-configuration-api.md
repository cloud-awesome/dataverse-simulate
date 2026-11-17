# Fluent configuration API review and implementation plan

Date reviewed: 2026-11-04; initial proposal: 2026-10-30; grouping amendments: 2026-11-03.

Status: **reworked around the existing simulator, following the agreed preference for simpler, shallower configuration**. This document supersedes the first implementation's transactional/snapshot design.

## Decision and scope

Make existing configuration discoverable through `.Simulate()` and `.Simulated()`. Fluent methods should primarily set existing options or invoke existing model/facade methods. Preserve the current operation engines, initialization helpers, option/reference semantics, and public compatibility signatures.

The first implementation added atomic configuration, graph copying, declaration reconciliation, a second plugin-context model, and application dependency lifetimes. Those guarantees exceeded the core setup requirement. They have been removed from this pass rather than supported through another configuration framework.

Keep the agreed public grouping:

- Roles, privileges, imports, and user/team assignments belong together under `WithSecurity`.
- Both mocks use `WithServices` for handlers, processors, and request failures. The provider adds the existing missing-service switches in that same group.
- Metadata supports loading and whole-model replacement. There is no metadata removal or merge API.
- Fluent members and overloads have XML comments suitable for IntelliSense.

This is the configuration/isolated-context slice of [P5](00-executive-summary.md#updated-order-of-work), brought forward for P3 documentation. Deeper platform semantics and release automation remain in their existing roadmap workstreams.

## Architecture: reuse the existing paths

| Area | Implementation |
| --- | --- |
| Initial setup | The callback fills the same `ISimulatorOptions` object used by legacy setup. The existing organization-service/provider initializer runs once. |
| Subsequent setup | `Simulated().Configure(...)` invokes a builder over the existing options, data, and facades. Each call applies directly, in order. |
| Security | `WithSecurity(Action<SimulatedSecurityModelService>)` exposes the existing facade on both paths. Before initialization, its existing methods update the model without seeding rows. On a live simulation, the existing methods seed/update rows normally and the group validates at the end. |
| Plugin context | `PluginExecutionContextMock` is the only configuration model. Formerly fixed fixture fields gain setters; the existing resolver maps the supported properties. Fluent setters are small extension methods on that mock. |
| Metadata | Reuse `SimulatedMetadataService` and `SimulatedMetadata.Load`, without configuration-specific serialization or copying. |
| Data/queues | Small conveniences delegate to `MockedEntityDataService.Add/Upsert/SetRelationship` and the existing queue model/seeder. |
| Requests | Existing `CustomOrgRequests().Add<TRequest>()` handles registration. A short list defers initial registrations until the real service registry exists. Live registration is immediate. |
| Shared provider state | Pass the existing request registry through factory creation, alongside the options/data/audit state already shared. Preserve each service's bound caller identity. |

`SimulationSetup` is only a small bridge: options, optional live data/request facades, an initial fixture-data view, and deferred initial request registration. It does not implement `ISimulatorOptions`, hold a second copy of the options, reconcile a temporary simulation, or commit snapshots.

The first implementation's `ConfigurationDraft`, `ConfigurationCopies`, `SimulationRuntime`, `PluginContextFrame`, `SimulationSecurityBuilder`, and separate plugin builder are removed. The metadata/role configuration-copy helpers are removed too. Existing CRUD input/output cloning remains part of operation correctness and is unaffected by this simplification.

## Public API and examples

Both entry points retain SDK return types. Keep the original CLR signatures as forwarding/legacy overloads, with optional defaults on the extended callback overloads. This preserves positional-null and named-data-service call forms without introducing an ambiguous `Simulate(Action<...>)` overload.

```csharp
IOrganizationService service = null!;
var now = new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);
var account = new Entity("account", Guid.NewGuid()) { ["name"] = "Example" };

service = service.Simulate(configure: simulation => simulation
    .WithData(account)
    .WithClock(now));

service.Simulated().Configure(simulation => simulation
    .WithClock(now.AddHours(1))
    .WithData(data => data.Upsert(
        new Entity("account", account.Id) { ["name"] = "Updated fixture" })));
```

Root `OrganizationSimulationBuilder` and `ServiceProviderSimulationBuilder` retain typed chaining. The common generic root is only shared fluent plumbing; it is not another state engine. Direct `.Simulated().WithData(...)`, `.WithClock(...)`, `.WithMetadata(...)`, and related conveniences call the same builder methods.

### Security uses the existing facade

```csharp
var user = new Entity("systemuser", Guid.NewGuid());
var businessUnit = new Entity("businessunit", Guid.NewGuid()) { ["name"] = "Test BU" };

service = service.Simulate(configure: simulation => simulation
    .WithUser(user)
    .WithBusinessUnit(businessUnit)
    .WithData(account)
    .WithSecurity(security => security
        .WithBusinessUnit(businessUnit.Id, "Test BU")
        .WithUser(user.Id, businessUnit.Id)
        .WithMissingEntityPolicy(false)
        .WithRole("Account reader", role => role
            .CanRead("account", PrivilegeDepthEnum.Organization))
        .AssignRoleToUser("Account reader", user.Id)));
```

`security` is `SimulatedSecurityModelService`, not a new security DSL. The same callback type works for initial and live configuration. Its initial mode simply defers seeding to the existing initializer. The model, role parser, privilege calculation, and seeder continue owning security behavior.

Selecting the authenticated user and defining a security principal remain distinct. Define the graph's units/users and role assignments explicitly, using the existing prerequisites; this pass does not automatically infer or reconcile that graph from identity fixtures. The example intentionally uses the same explicit unit/user ids in both places.

`IgnoreMissingEntities` retains its existing default of `true`; `WithMissingEntityPolicy(false)` opts into denial for unlisted permissions. Defining/importing a role does not assign it. `WithoutSecurity()` clears optional enforcement without deleting fixture rows or disabling metadata validation.

### Services use consistent terminology

```csharp
provider = provider.Simulate(configure: simulation => simulation
    .WithServices(services => services
        .WithRequestHandler<ScoreRequest>(CalculateScore)
        .WithProcessor(processorType, processor)
        .WithMissingService(typeof(ITracingService))));
```

Organization-service mocks use the same `WithServices` name and the same handler/processor/failure operations. Only the provider exposes missing SDK service settings. Their implementation writes the existing five flags; no application dependency factory/container is included.

### Plugin setup uses the existing mock

```csharp
provider = provider.Simulate(configure: simulation => simulation
    .WithPluginContext(plugin => plugin
        .WithMessage("Update")
        .WithStage(20)
        .WithPrimaryEntity("account", account.Id)
        .WithInputParameter("Target", account)
        .WithPreImage("Before", account)));

pluginUnderTest.Execute(provider);
```

The callback receives `PluginExecutionContextMock`. Direct assignments such as `plugin.Stage = 20` work as well as fluent extensions. An overload accepts an existing mock by reference.

Message/entity/id, stage/mode/isolation/depth, parameters/shared variables, images, initiating user, request/correlation/operation details, owning extension, parent context, offline flags, and transaction flags are wired through the existing resolver. User/BU/organization identity derives from the provider as before. Explicit operation time can be set; otherwise resolution uses simulated time.

There is no new execution-frame copying or lifetime guarantee. Existing parameter/image reference behavior is retained; subsequent service resolution reads the selected mock. Previously returned context references follow the existing resolver's captured/live behavior. Parent/transaction settings remain fixtures and do not invoke a pipeline or start a transaction.

## Coverage of the compatibility options

All 13 existing properties remain available through `SimulatorOptions` and `ISimulatorOptions`. No required properties are added to the interface.

| Existing option | Fluent equivalent |
| --- | --- |
| `AuthenticatedUser` | `WithUser(user)` |
| `BusinessUnit` | `WithBusinessUnit(businessUnit)` |
| `Organization` | `WithOrganization(organization)` |
| `ClockSimulator` | `WithClock(clock)` or `WithClock(now)` |
| `FiscalYearSettings` | `WithFiscalYearSettings(settings)` |
| `InitialiseData` | `WithData(rows/dictionary)` or `WithData(data => data.WithEntities(...))` |
| `InitialiseRelationships` | `WithData(data => data.WithRelationships(relationships))` |
| `Metadata` | `WithMetadata(path/model)` or `WithMetadata(metadata => metadata.Load(path))` |
| `SimulatedSecurityModel` | `WithSecurity(model)` or the existing security-facade callback |
| `Queues` | `WithData(data => data.WithQueues(model/configure))` |
| `EntityProcessors` | `WithServices(services => services.WithProcessor(key, processor))` |
| `FakeServiceFailureSettings` | `WithServices(services => services.WithFailure(rule))`; provider-only `WithMissingService(type, missing)` covers the five existing resolution flags |
| `PluginExecutionContextMock` | Provider `WithPluginContext(mock/configure)` |

Existing `Data()`, metadata/security/request/queue facades, and audit/log/telemetry/service-bus captures remain available. Providers gain the missing metadata/security/custom-request facades. Root fiscal settings replace the previous extra `WithEnvironment` wrapper.

## Behavioral trade-offs

1. **Reference semantics are consistent.** A supplied options object remains the same object with or without a callback. Existing copies/seeding performed by individual services remain as implemented; there is no blanket cloning of configuration, metadata, security models, or fixtures. Use fresh options/models/fixtures for independent tests.
2. **Live changes are sequential.** A failure does not undo earlier changes in the callback. `Configure` is an arranging convenience, not a transaction. Initializer errors retain existing setup behavior too; no new rollback contract applies to a supplied data store.
3. **Existing validation/order rules apply.** Initial data/relationships/security/queues follow the original initializer. Live relationships and queue items require referenced records to exist when the helper runs. No general declaration-order reconciliation is added.
4. **Additive data is a convenience, not a new store policy.** Fluent dictionary data is appended record by record. Raw `Data().Add(dictionary)` still replaces the store. Raw fixture ids, duplicate behavior, and mutable references retain their existing contracts; no global uniqueness policy is introduced by a builder.
5. **No unrelated replay occurs.** Changing clock/metadata does not reseed or copy the store, metadata graph, security graph, or queues. Default-user creation stays in `SimulatorOptionsProcessor`, including its existing fields.
6. **Identity and propagation remain deliberate.** Updating connection defaults does not rebind existing impersonated/SYSTEM service views. Providers and factory services share the same options and request registry. Handler invocation uses the executing caller's data view.
7. **Deferred registration is bounded.** Only initial custom request registration waits for initialization. Runtime registrations use the existing registry immediately; duplicate/built-in replacement protection remains there.

The broader snapshot/atomicity tests from the first implementation are replaced with tests for these agreed simpler contracts. Existing CRUD/query/security/transaction regressions remain in the suite.

## Implementation stages and acceptance gates

| Stage | Deliverable | Acceptance gate |
| --- | --- | --- |
| **1. Public contract and compatibility** | Root callbacks, existing concern types, typed returns, and retained extension signatures. | Old/new/null/named call forms compile; a consumer compiled against the original library runs with the replacement DLL. |
| **2. Thin common setup** | Direct option assignment and reuse of data, metadata, clock/calendar, relationships, and queue helpers. | Both initial paths use their original initializer; reference semantics and sequential failures are documented/tested. No second materializer or broad copy layer remains. |
| **3. Security and requests** | Existing security facade usable before/after initialization; existing typed request registration shared across provider services. | Roles/assignments/import and request handlers/processors/failures run through existing engines. Caller identity and batch/transaction dispatch are preserved. |
| **4. Existing provider service controls** | The five missing-service flags under the same `WithServices` name. | Missing/restored resolution works for each managed SDK interface. Custom dependency factories and lifetimes are deferred. |
| **5. One plugin context model** | Settable fields and fluent extensions on `PluginExecutionContextMock`, plus complete resolver mapping. | Resolve and assert all supported fields; exercise a real plugin and inspect output/captures. No parallel context type remains. |
| **6. XML docs, examples, and handoff** | Clear XML comments, executable examples, and documentation mapping in this repo. | Build/unit/package/binary checks pass; XML accompanies the package. Coordinate publication with the separate docs rewrite. |

## XML documentation and verification

Document each new public fluent method/overload with a useful summary, parameters/type parameters, fluent return value, and the important behavioral boundaries. Reused security/queue/role methods exposed by callbacks should also be documented. Include examples at the entry points and keep them aligned with `DocumentationCode`.

Generate library XML documentation and verify it is present next to the assembly in the local package. Check new fluent comment coverage through the focused XML test. Keep the older undocumented surface outside a new blanket CS1591 requirement.

Required implementation checks are the four-project Release build, full unit suite, the baseline-compiled consumer, and local package XML inspection. No fresh live Dataverse evidence is claimed by setup-layer tests. Visual IDE tooltip verification remains a separate manual check when a functioning documentation surface is available.

Verification on 2026-11-04: the four-project Release build passed, the full unit suite passed **1,078 tests with no failures/skips**, the original baseline-compiled consumer passed with the replacement DLL, and local package inspection confirmed the DLL plus XML documentation. See the handoff for commands/artifacts and limitations. No live Dataverse or documentation-site execution is claimed. The prior implementation's 1,091-pass result describes its superseded contracts and is not the result for this version.

## Deferred decisions

Do not add atomic reconfiguration, automatic configuration isolation/copying, automatic security-identity reconciliation, generalized order-independent declarations, separate execution-frame lifetimes, dependency factories/scopes, or a new registry/removal framework without a concrete consumer requirement and a separate cost discussion.

Metadata merge/removal, strict/parity presets, automatic plugin dispatch, deeper platform semantics, release/version choices, and documentation publication remain separate work.

## Evidence and handoff

- [Documentation migration handoff](11-fluent-api-documentation-handoff.md)
- [Executable examples](../CloudAwesome.Xrm.Simulate.Test/DocumentationCode/FluentConfigurationExamples.cs)
- [Fluent regressions](../CloudAwesome.Xrm.Simulate.Test/FluentConfigurationTests.cs)
- [Thin fluent layer](../CloudAwesome.Xrm.Simulate/Configuration)
- [Existing initialization helpers](../CloudAwesome.Xrm.Simulate/SimulatorOptionsProcessor.cs)
- [Reused security facade](../CloudAwesome.Xrm.Simulate/SecurityModel/SimulatedSecurityModelService.cs)
- [Single plugin mock](../CloudAwesome.Xrm.Simulate/ServiceProviders/PluginExecutionContextMock.cs) and [resolver](../CloudAwesome.Xrm.Simulate/ServiceProviders/PluginExecutionContextSimulator.cs)
