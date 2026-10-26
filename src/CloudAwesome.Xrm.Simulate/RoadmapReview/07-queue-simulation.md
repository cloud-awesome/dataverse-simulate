# Queue simulation roadmap

Date reviewed: 2026-10-26; implementation/live update: 2026-10-23

## Current position

First-class queue simulation is implemented. The 23 October P1/P2 pass hardens mutation safety and establishes live evidence for the ordinary task-based subset. Full queue metadata, privileges, and special activity behavior remain open.

- [x] `SimulatedQueueModel`, `SimulatedQueue`, and `SimulatedQueueItem` setup models.
- [x] `SimulatorOptions.Queues` and initialization after ordinary/system/security data and clock configuration.
- [x] Typed setup converts to normal `queue` and `queueitem` rows, visible through data access and query APIs.
- [x] `SimulatedQueueService` shared by the runtime facade and SDK request handlers.
- [x] Add/remove/pick/release/route handlers registered by default, with typed SDK responses.
- [x] Target queue capability checked when metadata is supplied.
- [x] Initial security checks and focused unit tests for allowed/denied operations.

`QueueMutationParityTests` now supplies 30 live/simulator comparisons. They establish the supported subset below, rather than complete queue parity.

The 26 October review confirms the scoped queue P1 work remains delivered; [ordinary CRUD isolation and query correctness](09-p1-closeout-review.md) are the outstanding P1 gates. No new live run was performed today; the passing queue/full-run evidence below is dated 23 October.

## Implemented setup API

```csharp
var options = new SimulatorOptions
{
    InitialiseData = new Dictionary<string, List<Entity>>
    {
        ["incident"] = [caseEntity]
    },
    Queues = SimulatedQueueModel.Create()
        .WithQueue(queueId, "Support queue", isDefault: true)
        .WithQueueItem(queueItemId, queueId, caseEntity.ToEntityReference())
};
```

`caseEntity` must already have a nonempty id. Typed initialization requires referenced queue and target rows to exist. Without metadata, any existing target table is accepted. With metadata, queue/queueitem scaffolding must be present and `IsValidForQueue == false` rejects the target; `null` is treated as unknown and allowed.

`.Simulated().Queues()` returns the runtime operations facade. The originally proposed `.Queues().WithQueue(...).WithQueueItem(...)` facade builders are not implemented; use `SimulatorOptions.Queues` for typed setup. Raw `InitialiseData`/data-store setup remains available.

### Initialization behavior delivered

- [x] Queue ids/primary-id attributes and required references are supplied from typed values.
- [x] Custom entity attributes are preserved by cloning an optional entity payload.
- [x] Conflicting typed/entity values and duplicate setup/existing ids are rejected.
- [x] Name, owner, business unit, default flag, entered-on, and audit/system fields are populated as appropriate.
- [x] Seeded queue-item queue/target references are resolved against the data store.

This is fixture setup over the existing entity store, not a second queue database. Full metadata validity for every seeded/generated attribute and worker/principal type is still open.

## Request/operation matrix

| Request | Current mutation and response | Remaining live questions |
| --- | --- | --- |
| `AddToQueueRequest` | Validates a cloned candidate before writing. Creates a new item or updates the uniquely matching source item, preserving its id even with omitted source, repeated adds, or same-queue moves. Returns `QueueItemId`. Request queue/target override conflicting property-bag references; a move ignores a supplied replacement id. | Received-email multiple queues; ambiguous source policy; full property/metadata and exact privilege rules. |
| `RemoveFromQueueRequest` | Deletes the queueitem, leaves its task target intact, returns an empty typed response, and audits once. Missing-item fault code is compared live. | Broader target/activity effects and exact missing-item envelope. |
| `PickFromQueueRequest` | Sets a valid systemuser worker or deletes the item when `RemoveQueueItem` is true; the task remains intact. Empty and unknown-worker validation faults match live. The facade also supports a team reference. | Team worker parity; exact delete/write privilege rules; broader activity/state effects. |
| `ReleaseToQueueRequest` | Clears the worker and audits once, including an already-unassigned item. Missing-item fault code is compared live. | Broader side effects and full fault envelopes. |
| `RouteToRequest` | Queue target changes queue and clears worker; systemuser target changes worker. Both preserve identity in live comparisons. Team routing remains a local supported policy. | Live team routing, default queues, ownership, exact timestamp/field effects, and privilege requirements. |

All five request handlers use the shared queue service for failure injection, validation, mutation, and auditing. Each successful operation emits its request-named audit entry; failed operations emit none.

## Metadata and security delivered versus pending

### Metadata

- [x] Queue-enabled capability is represented in DTO/runtime metadata and has loading tests.
- [x] Typed initialization requires queue/queueitem metadata when metadata is configured.
- [x] Seeded/generated target records are rejected when their metadata explicitly disallows queues.
- [ ] Validate generated and supplied queueitem/queue attributes for known columns, runtime types, create/update/read validity, and lookup targets.
- [ ] Apply valid-message capability rules if the external metadata contract supplies enough information.
- [x] Runtime add/pick validates worker reference shape, nonempty id, supported principal type, and existence before mutation. Runtime route resolves the destination before mutation.
- [ ] Extend these checks to all typed setup/owner paths and full metadata lookup-target rules.
- [ ] Verify `objecttypecode` and other automatically generated fields only where a supported query scenario needs them.

`SimulatedQueueService` and the seeder write directly through `MockedEntityDataService`; they do not invoke general CRUD entity processors. Queue operations maintain their own timestamps and one operation audit. The runtime validates core identity/worker/entered-on property shapes, references, and target queue capability; arbitrary custom properties remain permissive. Loading metadata alone does not guarantee full queue attribute validation. General CRUD processor dispatch and full metadata validation remain explicit P4 decisions, not an implied plugin pipeline.

### Security

- [x] Add demands read on target and destination queue, plus queueitem create; source moves check source read and queueitem write while preserving identity.
- [x] Remove demands queueitem delete.
- [x] Pick/release/route demand queueitem write; removing through Pick additionally requires delete (unit evidence). Route also checks target read and, for queues, destination write.
- [x] No configured concrete security model preserves permissive behavior.
- [ ] Capture append/append-to, create/delete/write, principal/queue access, and exact denial faults from live operations before calling this a complete privilege policy.

Queue queries use the shared base/linked row security filtering described in [06](06-security-model.md); the complete queue-specific privilege matrix remains unverified.

## Remaining work in roadmap order

### P1 - Mutation safety and consistent validation

- [x] Validate the supported candidate before writing; source moves update the existing item instead of deleting/recreating it.
- [x] Add failure regressions for source/destination/target errors, malformed properties, worker/routing errors, duplicate new ids, access denial, injected failures, and transaction rollback.
- [x] Share success-only operation auditing and failure injection between facade and SDK handlers; document the explicit metadata/processor boundary above.
- [x] Capture ordinary-task duplicate/source/id and worker/routing behavior live; retain received-email/team/privilege qualifications.

### P2 - Dedicated live queue suite

Capture minimal fixtures using transient queue-enabled records:

- [x] Exact response names/output keys, typed queue-item ids/references, and presence of created/modified/entered timestamps for new ordinary items.
- [x] Existing/repeated destinations, implicit/explicit/same-queue moves, conflicting property-bag queue/object references, ignored move ids, and worker properties.
- [x] Wrong/missing source, empty/missing destination, missing task/item/worker, and empty worker. Selected validation fault messages/codes are exact; other missing-record comparisons retain codes only.
- [x] Pick with/without removal, release before/after pick, queue/user routing, stable item ids, worker preservation/clearing, and task survival after removal.
- [ ] Live team routing, disabled queue capability/invalid target types, broad custom fields, exact timestamps/owners, and other activity effects.
- [x] Track transient queue/task/item fixtures, remove confirmed-deleted items from tracking, and verify the lifecycle cleanup/retry follow-up: 30 live passes with no reported cleanup errors.
- [ ] Expand live queue privilege-denial requirements and queue transaction rollback evidence. Rollback/security unit coverage is present; stricter cleanup failure reporting remains P2 harness work.

Use the existing parity harness and cleanup scope; no new framework is necessary. See [03](03-integration-testing.md).

### P3/P4 - Documentation and metadata completion

- [ ] Publish existing setup/request examples and the supported routing/worker subset with explicit limitations (P3).
- [ ] Extend attribute/type/lookup/capability validation and ownership handling using shared components (P4).
- [ ] Verify typed/entity conflict behavior and initialization invariants more thoroughly; implementation checks exist, but the unit suite does not exhaust every validation branch.

Server-side routing engines, mailbox processing, SLA execution, asynchronous queue workers, and cascading plugin execution remain outside the unit-test scope.

## Test evidence

`QueueSimulationTests` covers typed initialization, metadata-absent permissiveness, queue-enabled loading/rejection, add/source-move/remove/pick/release/route mutations, selected security allow/deny cases, and all five SDK request responses. These passed in the 2026-10-22 unit run (560 passed overall, 4 skipped).

The initial implementation remains the foundation; the following pass supplies mutation safety and scoped live evidence. The full security matrix and metadata enforcement remain open.

## P1/P2 implementation and verification - 23 October 2026

`QueueMutationConsistencyTests` adds **59 regression cases** for shared facade/request behavior, move identity, failure atomicity, validation, input reference isolation, required deletion access, and transaction rollback. Existing setup/security/request tests remain passing. The full Release unit suite passes **913 tests, 0 skipped**.

`QueueMutationParityTests` adds **30 live cases** using temporary queues and tasks, covering the matrix above. Records are tracked as soon as they are created; successful deletion scenarios call `MarkDeleted` after confirming the item is absent, avoiding redundant cleanup deletion. Cleanup releases/removes queue items through their SDK messages and retries failures once after dependents; unresolved failures remain visible. The supported implicit-move policy follows the [SDK AddToQueue notes](https://learn.microsoft.com/en-us/dotnet/api/microsoft.crm.sdk.messages.addtoqueuerequest?view=dataverse-sdk-latest), with retained identity established by these live tests.

This is an ordinary queue-enabled task contract. Received email may occupy multiple queues in Dataverse and is not implemented by this single-source policy; ambiguous seeded source items raise a simulator exception. Arbitrary property create/read validity, generated owner/object-type fields, team routes, default queue behavior, and detailed denied-privilege envelopes remain P2/P4 work. Simulator metadata/setup exceptions and arbitrary custom-handler side effects are outside exact Dataverse fault/rollback claims.

Focused command: `dotnet test CloudAwesome.Xrm.Simulate.Integration/CloudAwesome.Xrm.Simulate.Integration.csproj --no-restore --configuration Release --filter "TestCategory=MutationConsistency"`. See [03](03-integration-testing.md) for full-run results and configuration.

The full Release integration run passed all **158 cases**. After correcting cleanup, the final **30-case queue rerun** passed with **no cleanup errors reported**. The initially stranded fixture queue/task were explicitly removed; local TRX paths and the verification boundary are recorded in [03](03-integration-testing.md).

## Evidence anchors

- [Setup model and builders](../CloudAwesome.Xrm.Simulate/Queues/SimulatedQueueModel.cs)
- [Initialization/seeding](../CloudAwesome.Xrm.Simulate/Queues/SimulatedQueueDataSeeder.cs)
- [Runtime operations and checks](../CloudAwesome.Xrm.Simulate/Queues/SimulatedQueueService.cs)
- [Queue unit tests](../CloudAwesome.Xrm.Simulate.Test/QueueSimulationTests.cs)
- [Metadata follow-up](05-metadata-simulation.md)
