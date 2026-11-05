# Fluent API documentation handoff

Updated: 2026-11-04 for the shallower implementation. Supersedes the first implementation's atomic/snapshot descriptions. This is local source behavior, not evidence of a published package.

## Recommended entry points

Use `.Simulate(configure: simulation => ...)` for initial configuration and `.Simulated().Configure(simulation => ...)` for subsequent setup. Both SDK entry points retain their SDK return types. Direct facade `With...` conveniences remain available.

```csharp
IOrganizationService service = null!;
var now = new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);
var account = new Entity("account", Guid.NewGuid()) { ["name"] = "Example" };

service = service.Simulate(configure: simulation => simulation
    .WithData(account)
    .WithClock(now));

service.Simulated().WithClock(now.AddHours(1));
```

Import `CloudAwesome.Xrm.Simulate` and `Microsoft.Xrm.Sdk`, alongside the existing model namespaces needed by each example.

## Explain the shallow design accurately

- Fluent setup fills the **same options object** and invokes the **existing initializer**. There is no separate fluent initialization engine.
- Live configuration delegates to existing helpers and applies **sequentially**, without rollback. An earlier change remains when a later call fails.
- Options, fixtures, metadata, security models, and plugin mocks retain existing reference semantics. There is no blanket automatic graph copying or fluent-only ownership mode. Use fresh fixtures/options for independent tests.
- Individual services may still copy values as part of their existing contracts. In particular, CRUD boundary isolation remains implemented; raw fixture access remains mutable.
- Initial setup follows the existing initializer order. Live relationships and queue items need their referenced records to exist at the time of the call.
- Changing a setting does not reseed or snapshot unrelated state. Configuration does not clear audit/log/telemetry/service-bus captures.

## Concern grouping and options mapping

| Existing option | Fluent path |
| --- | --- |
| `AuthenticatedUser` | `WithUser(user)` |
| `BusinessUnit` | `WithBusinessUnit(businessUnit)` |
| `Organization` | `WithOrganization(organization)` |
| `ClockSimulator` | `WithClock(clock)` or `WithClock(now)` |
| `FiscalYearSettings` | `WithFiscalYearSettings(settings)` |
| `InitialiseData` | `WithData(rows/dictionary)` or the data callback's `WithEntities` |
| `InitialiseRelationships` | `WithData(data => data.WithRelationships(relationships))` |
| `Metadata` | `WithMetadata(path/model)` or the existing metadata service callback |
| `SimulatedSecurityModel` | `WithSecurity(model)` or the existing security service callback |
| `Queues` | `WithData(data => data.WithQueues(model/configure))` |
| `EntityProcessors` | `WithServices(services => services.WithProcessor(key, processor))` |
| `FakeServiceFailureSettings` | `WithServices(services => services.WithFailure(rule))`; provider `WithMissingService(type, missing)` covers all five managed service flags |
| `PluginExecutionContextMock` | Provider `WithPluginContext(mock/configure)` |

`WithServices` has the same shared handler/processor/failure operations on both mocks. Provider-only service availability extends that group. Custom dependency registration/factories and their lifetimes are deferred. The old extra `WithEnvironment` group is replaced by direct root identity/clock/fiscal setters.

Metadata loading replaces the complete model. No merge or removal API is included. Fluent dictionary data appends individual rows; raw `Data().Add(dictionary)` retains its replacement behavior.

## Security uses the existing facade

Both callbacks receive `SimulatedSecurityModelService`. Roles, imports, privileges, and assignments belong under `WithSecurity`; there is no separate root role shortcut or new security builder.

```csharp
var user = new Entity("systemuser", Guid.NewGuid());
var businessUnit = new Entity("businessunit", Guid.NewGuid()) { ["name"] = "Test BU" };

service = service.Simulate(configure: simulation => simulation
    .WithUser(user)
    .WithBusinessUnit(businessUnit)
    .WithSecurity(security => security
        .WithBusinessUnit(businessUnit.Id, "Test BU")
        .WithUser(user.Id, businessUnit.Id)
        .WithMissingEntityPolicy(false)
        .WithRole("Reader", role => role.CanRead("account", PrivilegeDepthEnum.Organization))
        .AssignRoleToUser("Reader", user.Id)));
```

The snippet also requires `CloudAwesome.Xrm.Simulate.SecurityModel`. Define the graph's units/users explicitly, as with existing security setup. The root identity and graph principal are distinct concepts; this layer does not automatically reconcile them. Assigning a role remains explicit. `IgnoreMissingEntities` retains its default of true; `WithMissingEntityPolicy(false)` opts into denial for unlisted permissions.

The facade collects model declarations before initialization and uses its existing live seed/update operations after initialization. The group validates the live graph at the end, without rolling back earlier mutations. `WithoutSecurity()` disables optional enforcement without deleting fixture rows or changing metadata validation.

## Plugin context uses one model

The callback receives `PluginExecutionContextMock`, supporting ordinary property assignment and small fluent extension methods. An existing mock can be supplied by reference. There is no `PluginContextFrame` or separate plugin-context builder.

The existing resolver now wires the supported message/entity/id, stage/mode/isolation/depth, parameters/shared variables, images, initiating user, request/correlation/operation fields, owning extension, parent, and offline/transaction fields. Provider identity remains authoritative for user/BU/organization. Explicit operation time overrides simulated time when supplied.

Parameters and images retain ordinary reference semantics, so plugin output writes are observable. Subsequent service resolution uses the selected mock. Do not describe an immutable execution-frame or previous-context snapshot contract. Context flags do not execute a plugin pipeline or start an SDK transaction.

## Requests and provider state

Initial custom handler registration is deferred until initialization creates the existing request registry. Live registration uses `CustomOrgRequests().Add<TRequest>()` immediately. Duplicate and built-in replacement checks remain in that registry.

Providers and their existing/future factory services share options, data/audits, and custom request registration. Each handler receives the executing caller's view. Connection-default changes do not rebind existing explicit/empty/SYSTEM caller views. Keep the existing authenticated-connection `WhoAmI` description.

Providers expose the existing metadata/security/custom-request facades alongside captures. Their five managed SDK service flags affect subsequent resolution, without revoking earlier references.

## Separate documentation repository

The last inspected rewrite lives at `C:/Source/Cloud Awesome/cloudawesome-docs/docs/dataverse-simulate/` and contains staged/unstaged work. It has not been modified as part of this rework. Coordinate these updates with its current state:

| Page/area | Update |
| --- | --- |
| `configuration/index.md` | Introduce the thin fluent layer and mapping above. Keep legacy options and explain shared reference/sequential semantics. |
| `index.md`, `getting-started.md` | Use a minimal fluent setup while retaining the returned SDK service/provider. |
| `organization-service/index.md`, `plugins/index.md` | Cover both entry points, shared `WithServices`, explicit plugin execution, and provider-only flags/context. |
| Data setup | Distinguish additive fluent conveniences, raw dictionary replacement, and operation-time CRUD isolation. |
| Security setup | Use the existing facade; define units/users and role assignment explicitly. |
| Plugin setup | Show property assignment or fluent setters on the existing mock, complete resolver wiring, and ordinary reference behavior. |
| Request/processor/failure pages | Show common `WithServices` and existing typed registration/option contracts. |
| Support matrix/roadmap | Replace the old claim that no comprehensive configuration entry point exists, without adding snapshot, atomicity, DI, or platform-parity claims. |

## Executable examples and XML docs

- [Executable quick start, security/metadata, shared handlers, legacy options, and plugin examples](../CloudAwesome.Xrm.Simulate.Test/DocumentationCode/FluentConfigurationExamples.cs)
- [Focused fluent/reference/compatibility/propagation/context/XML regressions](../CloudAwesome.Xrm.Simulate.Test/FluentConfigurationTests.cs)
- [Thin configuration helpers](../CloudAwesome.Xrm.Simulate/Configuration)

New fluent members have XML comments and the library emits an XML documentation file for packaging. The focused documentation test checks summaries, parameters, and fluent returns. Reused security/role/queue methods are documented directly rather than maintaining comments on another security DSL.

Rider's documentation API returned empty responses in the prior session. Generated XML/package checks provide distribution evidence; visual tooltip/parameter-help verification remains manual. Local package generation is not a publication/version decision.

## Rework verification

Verification on 2026-11-04:

- Four-project Release build passed with no errors and existing warnings (`fluent-shallow-build.log`).
- Full Release unit suite passed **1,078 tests**, with **0 failures and 0 skips**: the original 1,043 cases plus 35 revised fluent/example cases. Results are in `fluent-shallow-unit.log` and `CloudAwesome.Xrm.Simulate.Test/TestResults/fluent-shallow-unit.trx`.
- A consumer compiled against the original library passed after replacing only its library DLL with the simplified implementation. Source-level compatibility calls are covered by the revised regression fixture.
- Local packaging passed and the package contains both the DLL and XML documentation under `lib/net8.0` (`fluent-shallow-pack.log`). The existing package-readme advisory remains release work; this local artifact is not a publication or version decision.
- XML was regenerated after refining the facade tooltips. No new functional changes were made after the full unit run.
- No live Dataverse tests or separate documentation-site build were run. The separate docs working tree was not modified.

The earlier 1,091-test snapshot belongs to the superseded implementation; its atomic/snapshot/lifetime tests were deliberately replaced to match the simpler agreed contracts. Existing operation regressions remain in the full suite.
