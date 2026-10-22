# Queue simulation roadmap

Date reviewed: 2026-10-22

## Current position

First-class queue simulation is implemented. The old plan to add models and request handlers is substantially complete; the remaining work is live parity and validation/atomicity hardening.

- [x] `SimulatedQueueModel`, `SimulatedQueue`, and `SimulatedQueueItem` setup models.
- [x] `SimulatorOptions.Queues` and initialization after ordinary/system/security data and clock configuration.
- [x] Typed setup converts to normal `queue` and `queueitem` rows, visible through data access and query APIs.
- [x] `SimulatedQueueService` shared by the runtime facade and SDK request handlers.
- [x] Add/remove/pick/release/route handlers registered by default, with typed SDK responses.
- [x] Target queue capability checked when metadata is supplied.
- [x] Initial security checks and focused unit tests for allowed/denied operations.

No queue-specific live integration fixture currently exists. Do not describe the implemented queue policy as fully live-verified.

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
| `AddToQueueRequest` | Validates destination/target, creates a queueitem, merges supplied queueitem properties, sets audit timestamps, returns `QueueItemId` and response name. If a source queue is supplied, removes the first matching source item. | Duplicate target in same queue; missing/multiple source matches; item identity during moves; property restrictions; exact privilege set. |
| `RemoveFromQueueRequest` | Deletes existing queueitem and audits. | Exact missing-item fault and any target-record effects. |
| `PickFromQueueRequest` | Resolves a systemuser worker from SDK `WorkerId`; sets worker or deletes item when `RemoveQueueItem` is true. Runtime service also has an EntityReference overload. | Accepted worker types, default/empty worker behavior, required delete versus write access, activity/state effects. |
| `ReleaseToQueueRequest` | Clears worker, updates modification fields, and audits. | Faults/side effects for already-unassigned or invalid items. |
| `RouteToRequest` | Queue target changes queue and clears worker; user/team target changes worker, preserving the item id. | Accepted target types, default queue/ownership semantics, exact fields, and whether live Dataverse preserves the id. |

All five request handlers exist. Routing to a principal is currently a local policy, not proof of the exact live contract.

## Metadata and security delivered versus pending

### Metadata

- [x] Queue-enabled capability is represented in DTO/runtime metadata and has loading tests.
- [x] Typed initialization requires queue/queueitem metadata when metadata is configured.
- [x] Seeded/generated target records are rejected when their metadata explicitly disallows queues.
- [ ] Validate generated and supplied queueitem/queue attributes for known columns, runtime types, create/update/read validity, and lookup targets.
- [ ] Apply valid-message capability rules if the external metadata contract supplies enough information.
- [ ] Validate worker/principal/owner types and existence consistently across setup and all operations.
- [ ] Verify `objecttypecode` and other automatically generated fields only where a supported query scenario needs them.

`SimulatedQueueService` and the seeder write directly through `MockedEntityDataService`; they do not currently call the general CRUD `MetadataValidator` or entity processors. Loading metadata alone does not guarantee full queue attribute validation.

### Security

- [x] Add demands read on target and destination queue, plus queueitem create; source moves also check source read and deletion of a matching source item.
- [x] Remove demands queueitem delete.
- [x] Pick/release/route demand queueitem write; route also checks target read and, for queues, destination write.
- [x] No configured concrete security model preserves permissive behavior.
- [ ] Capture append/append-to, create/delete/write, principal/queue access, and exact denial faults from live operations before calling this a complete privilege policy.

Queue visibility also inherits the base-versus-linked security limitations described in [06](06-security-model.md).

## Remaining work in roadmap order

### P1 - Mutation safety and consistent validation

- [ ] Validate the complete destination item before removing a source item. `AddToQueue` currently deletes a matching source item before duplicate-id and conflicting-property checks; a later failure can leave the source deleted.
- [ ] Define operation atomicity and add regressions for failed source moves, invalid targets/properties, and routing/picking errors.
- [ ] Align queue operations with the agreed audit/metadata/processor contract without introducing duplicate audits for composed requests.
- [ ] Document provisional routing, duplicate-target, and worker behavior until live fixtures establish the contract.

### P2 - Dedicated live queue suite

Capture minimal fixtures using transient queue-enabled records:

- [ ] Exact response names, output keys/value types, and queueitem fields.
- [ ] Add to an existing destination, including already-queued targets and custom properties.
- [ ] Move from source to destination, including same queue, missing source, and failure after validation.
- [ ] Pick with/without removal, release, and route to queue/user/team where supported.
- [ ] Missing queue/item/target/worker, invalid target types, and disabled queue capability faults.
- [ ] Timestamp/owner/worker/id changes and any target-record activity effects.
- [ ] Security requirements and cleanup after partial failures or transactions.

Use the existing parity harness and cleanup scope; no new framework is necessary. See [03](03-integration-testing.md).

### P3/P4 - Documentation and metadata completion

- [ ] Publish existing setup/request examples and the supported routing/worker subset with explicit limitations (P3).
- [ ] Extend attribute/type/lookup/capability validation and ownership handling using shared components (P4).
- [ ] Verify typed/entity conflict behavior and initialization invariants more thoroughly; implementation checks exist, but the unit suite does not exhaust every validation branch.

Server-side routing engines, mailbox processing, SLA execution, asynchronous queue workers, and cascading plugin execution remain outside the unit-test scope.

## Test evidence

`QueueSimulationTests` covers typed initialization, metadata-absent permissiveness, queue-enabled loading/rejection, add/source-move/remove/pick/release/route mutations, selected security allow/deny cases, and all five SDK request responses. These passed in the 2026-10-22 unit run (560 passed overall, 4 skipped).

The old implementation steps 1-4 are complete as first implementations. Step 5 security is wired; its live parity portion and full metadata enforcement remain open. The next queue task should therefore be the P1/P2 hardening above, not adding the same models or handlers again.

## Evidence anchors

- [Setup model and builders](../CloudAwesome.Xrm.Simulate/Queues/SimulatedQueueModel.cs)
- [Initialization/seeding](../CloudAwesome.Xrm.Simulate/Queues/SimulatedQueueDataSeeder.cs)
- [Runtime operations and checks](../CloudAwesome.Xrm.Simulate/Queues/SimulatedQueueService.cs)
- [Queue unit tests](../CloudAwesome.Xrm.Simulate.Test/QueueSimulationTests.cs)
- [Metadata follow-up](05-metadata-simulation.md)
