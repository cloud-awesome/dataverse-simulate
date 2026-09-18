# Queue simulation roadmap

## Goal

Add first-class simulation for Dataverse queues and queue items so plugin and service tests can exercise realistic queue workflows without hand-building `queueitem` records for every scenario.

The first implementation should cover:

- queue and queue-item seeding through `SimulatorOptions`;
- a simulator-owned `SimulatedQueue` model and queue data service facade;
- metadata-aware validation when `SimulatorOptions.Metadata` is supplied;
- organization request handlers for common queue messages;
- predictable mutation of the existing in-memory entity store so tests can still query `queue` and `queueitem` directly.

Queue simulation must remain opt-in for setup ergonomics and backwards compatibility. If no queue model is configured, direct CRUD over `queue` and `queueitem` should continue to work through the normal entity store. If queue requests are executed without pre-seeded queues, the simulator should create only the request side effects that Dataverse would create, and should validate referenced rows that are required by the request.

## Current position

The project already has the necessary extension points:

- `SimulatorOptions.InitialiseData` can seed raw `queue` and `queueitem` rows today.
- `SimulatorOptions.Metadata` can validate entities, attributes, lookups, valid messages, and relationships when supplied.
- `MockedEntityDataService` stores ordinary Dataverse rows in memory and already exposes `Add`, `Upsert`, `Get`, `Update`, and `Delete`.
- `RequestHandlerRegistry` registers first-class `OrganizationRequest` handlers for CRUD, association, team membership, and security access requests.
- Security and metadata planning already establish the pattern that configured fidelity should be used when available and permissive behavior should remain the default otherwise.

The gap is that queue-specific behavior is currently implicit. Tests must manually know the `queue` and `queueitem` schema, and SDK requests such as `AddToQueueRequest` are unsupported unless a test registers a custom handler.

## Design principles

- Keep queue state in Dataverse-shaped `queue` and `queueitem` entities so existing retrieve/query tests see the same data that request handlers mutate.
- Add typed setup models only to reduce test ceremony and centralize queue-specific validation.
- Use metadata when supplied. Without metadata, accept any target entity type and any queue-enabled shape that has enough references for the request to proceed.
- Do not model server-side routing engines, mailbox processing, SLA behavior, or asynchronous queue workers in the first implementation.
- Preserve the current direct `IOrganizationService` and `Execute` handler split: organization requests should call shared queue services rather than duplicate logic.
- Capture live Dataverse parity for fault types, response shapes, and edge cases before locking behavior.

## Proposed model

Introduce queue-specific runtime types:

```csharp
public sealed class SimulatedQueueModel
{
    public List<SimulatedQueue> Queues { get; } = [];
    public List<SimulatedQueueItem> QueueItems { get; } = [];
}

public sealed class SimulatedQueue
{
    public required Guid QueueId { get; init; }
    public string? Name { get; init; }
    public EntityReference? OwnerId { get; init; }
    public EntityReference? BusinessUnitId { get; init; }
    public bool IsDefault { get; init; }
    public Entity? Entity { get; init; }
}

public sealed class SimulatedQueueItem
{
    public required Guid QueueItemId { get; init; }
    public required EntityReference QueueId { get; init; }
    public required EntityReference ObjectId { get; init; }
    public EntityReference? WorkerId { get; init; }
    public DateTime? EnteredOn { get; init; }
    public Entity? Entity { get; init; }
}
```

`SimulatedQueue` and `SimulatedQueueItem` should be setup models, not parallel persistence. During simulator initialization, they should be converted into normal `Entity("queue")` and `Entity("queueitem")` rows and inserted into `MockedEntityDataService`.

If a caller supplies `Entity`, the simulator should preserve custom attributes and fill missing required queue attributes from the strongly typed properties. If both are supplied and conflict, fail at initialization with a clear simulator exception.

## SimulatorOptions and setup API

Add queue setup to `SimulatorOptions`:

```csharp
var options = new SimulatorOptions
{
    Metadata = SimulatedMetadata.Load("dataverse-metadata.json"),
    Queues = SimulatedQueueModel.Create()
        .WithQueue(defaultQueueId, "Default support queue", isDefault: true)
        .WithQueueItem(queueItemId, defaultQueueId, caseReference)
};
```

Recommended options surface:

```csharp
public SimulatedQueueModel? Queues { get; set; }
```

Recommended fluent setup surface:

```csharp
_organizationService.Simulated()
    .Queues()
    .WithQueue(defaultQueueId, "Default support queue")
    .WithQueueItem(queueItemId, defaultQueueId, targetReference);
```

The fluent API should mutate the same `SimulatorOptions.Queues` object that initialization uses, matching the metadata and security setup style.

## Data-store integration

Add a queue service over `MockedEntityDataService` rather than a separate store:

```csharp
internal sealed class SimulatedQueueService
{
    Guid AddToQueue(EntityReference target, Guid destinationQueueId, Guid? sourceQueueId, Entity? queueItemProperties, ISimulatorOptions? options);
    void RemoveFromQueue(Guid queueItemId, ISimulatorOptions? options);
    void PickFromQueue(Guid queueItemId, Guid workerId, bool removeQueueItem, ISimulatorOptions? options);
    void ReleaseToQueue(Guid queueItemId, ISimulatorOptions? options);
    void RouteTo(Guid queueItemId, EntityReference target, ISimulatorOptions? options);
}
```

The service should:

- read and write `queue` and `queueitem` through the existing data service;
- use `dataService.SystemTime` for queue timestamps;
- use `dataService.AuthenticatedUser` as the default worker where the request does not supply one and live parity confirms that behavior;
- keep duplicate detection and lookup validation in one place;
- create queue items with deterministic Dataverse-shaped attributes.

Suggested minimum `queue` attributes:

- `queueid`;
- `name`;
- `ownerid`;
- `businessunitid`;
- `createdon`, `createdby`, `modifiedon`, `modifiedby` where normal create behavior supplies them.

Suggested minimum `queueitem` attributes:

- `queueitemid`;
- `queueid`;
- `objectid`;
- `objecttypecode` if live parity or metadata fixtures show it is required in common query paths;
- `workerid`;
- `enteredon`;
- `createdon`, `createdby`, `modifiedon`, `modifiedby`;
- any request-supplied `QueueItemProperties` attributes that are valid for create.

## Metadata-aware validation

When `SimulatorOptions.Metadata` is supplied, queue simulation should validate:

- `queue` and `queueitem` exist in metadata before typed seeding creates rows.
- Seeded and generated attributes are known and valid for create or update as appropriate.
- `AddToQueueRequest.Target` references an existing row.
- `DestinationQueueId` references an existing `queue`.
- `SourceQueueId`, when supplied, references an existing `queue`.
- `queueitem.objectid` can target the requested table only if the target table is queue-enabled in metadata.
- request messages are valid for the participating tables when metadata exposes valid message information.

The metadata model likely needs one additional table capability:

```csharp
public bool? IsValidForQueue { get; init; }
```

If metadata does not expose a direct queue-enabled flag from the generated document, the generator can derive it from Dataverse metadata where possible and leave it `null` when unknown. Validation should treat `null` as unknown, not denied, unless a later strict mode says otherwise.

When metadata is not supplied, queue requests should:

- accept any target entity logical name;
- still require referenced queue and queue-item rows to exist where the request depends on them;
- still prevent impossible duplicate queue-item ids and missing target records.

## Organization request coverage

Register handlers in `OrganisationServiceSimulator.RegisterServiceRequests()` for the SDK queue messages:

- `AddToQueueRequest`
- `RemoveFromQueueRequest`
- `PickFromQueueRequest`
- `ReleaseToQueueRequest`
- `RouteToRequest`

### `AddToQueueRequest`

Expected behavior:

- validate `Target`;
- validate `DestinationQueueId`;
- optionally validate and remove or update an existing source queue item when `SourceQueueId` is supplied, based on live parity;
- create a new `queueitem`;
- merge `QueueItemProperties` into the generated queue item after validating that the properties entity is `queueitem`;
- set `ResponseName = "AddToQueue"`;
- return `AddToQueueResponse.QueueItemId`.

Parity questions for live fixtures:

- Does adding an already queued target to the same queue fail, create a duplicate, or return the existing queue item?
- Does `SourceQueueId` move an existing queue item or only annotate routing?
- Which timestamps change on target record and queue item?

### `RemoveFromQueueRequest`

Expected behavior:

- validate `QueueItemId`;
- delete the `queueitem` row;
- set `ResponseName = "RemoveFromQueue"`.

Parity questions:

- Does removal mutate the target record in any common table types?
- What exact fault is thrown for a missing queue item?

### `PickFromQueueRequest`

Expected behavior:

- validate `QueueItemId`;
- validate `WorkerId` as a `systemuser` or `team` principal when metadata/security model can do so;
- if `RemoveQueueItem` is `true`, delete the queue item;
- otherwise set `workerid` and update modified metadata on the queue item;
- set `ResponseName = "PickFromQueue"`.

Parity questions:

- Does `WorkerId` accept teams or only users for this SDK message?
- Are there additional state/status changes for activity-backed queue items?

### `ReleaseToQueueRequest`

Expected behavior:

- validate `QueueItemId`;
- clear `workerid`;
- update modified metadata on the queue item;
- set `ResponseName = "ReleaseToQueue"`.

### `RouteToRequest`

Expected behavior:

- validate `QueueItemId`;
- validate `Target`;
- route based on the target type:
  - if the target is a `queue`, move the queue item to that queue;
  - if the target is a `systemuser` or `team`, update ownership/worker fields according to live parity;
- set `ResponseName = "RouteTo"`.

Parity questions:

- Which target logical names are accepted by the SDK message in current Dataverse?
- Does routing to a principal create or select that principal's default queue?
- Does routing preserve the existing `queueitemid` or create a new queue item?

## Security integration

Queue requests should use the same security enforcer described in the security roadmap.

Minimum first-pass checks:

- `AddToQueueRequest`: read target record, read destination queue, append/append-to or create `queueitem` privilege based on live parity.
- `RemoveFromQueueRequest`: delete/write access to the queue item.
- `PickFromQueueRequest`: write access to the queue item and valid acting worker.
- `ReleaseToQueueRequest`: write access to the queue item.
- `RouteToRequest`: write access to the queue item and read/write access to the destination queue or target principal as parity requires.

If no security model is configured, allow existing permissive behavior.

## Test strategy

Start with unit tests around the shared queue service before wiring request handlers.

Minimum tests:

- simulator initializes typed queues into the `queue` table;
- simulator initializes typed queue items into the `queueitem` table;
- seeded queue item references must point at existing queue and target rows;
- metadata absent accepts queue items for any existing target table;
- metadata present rejects queue items for tables not enabled for queues;
- `AddToQueueRequest` creates a queue item and returns its id;
- `AddToQueueRequest.QueueItemProperties` merges valid custom attributes;
- `RemoveFromQueueRequest` deletes the queue item;
- `PickFromQueueRequest` sets `workerid` when `RemoveQueueItem` is false;
- `PickFromQueueRequest` deletes the queue item when `RemoveQueueItem` is true;
- `ReleaseToQueueRequest` clears `workerid`;
- `RouteToRequest` moves the queue item or otherwise follows live-observed behavior;
- missing queue, missing queue item, invalid worker, and invalid target failures match live Dataverse faults where parity fixtures exist.

Integration parity tests should capture:

- exact response names and output parameter keys;
- queue item fields created by Dataverse for a simple case;
- behavior when a target is already in the destination queue;
- behavior when source and destination queues are both supplied;
- missing queue item and missing queue faults;
- queue-enabled versus non-queue-enabled target faults.

## Phased implementation plan

### Step 1 - Model and initialization

- Add `SimulatedQueueModel`, `SimulatedQueue`, and `SimulatedQueueItem`.
- Add `SimulatorOptions.Queues` to `SimulatorOptions` and `ISimulatorOptions`.
- Add a `SimulatorOptionsProcessor.InitialiseQueues` step after ordinary data, users, business units, organization, security seed data, and system time are available.
- Convert typed queue setup into ordinary entities in `MockedEntityDataService`.
- Add tests for initialization and direct retrieval/query visibility.

### Step 2 - Metadata validation

- Extend metadata JSON/runtime models with queue-enabled table capability if the generator can supply it.
- Add queue-specific validation helpers that consume `SimulatedMetadata` without making metadata mandatory.
- Add tests for permissive mode and metadata-backed strictness.

### Step 3 - Queue service

- Implement shared queue mutations over `MockedEntityDataService`.
- Reuse existing entity create/update/delete helpers where practical so auditing, timestamps, processors, metadata, and security remain consistent.
- Add tests for add, remove, pick, release, and route behavior without SDK request handlers.

### Step 4 - Organization request handlers

- Add handlers for `AddToQueueRequest`, `RemoveFromQueueRequest`, `PickFromQueueRequest`, `ReleaseToQueueRequest`, and `RouteToRequest`.
- Register them in the existing request registry.
- Route all handlers through `SimulatedQueueService`.
- Add response shape tests and failure tests.

### Step 5 - Security and parity hardening

- Wire queue service operations through the security enforcer.
- Capture live faults and queue item snapshots from an integration environment.
- Replace provisional simulator exceptions with Dataverse-shaped faults where enough evidence exists.

## Dependencies and sequencing

This work intersects with:

- [01-simulation-parity](01-simulation-parity.md), because queue requests are part of the core organization request backlog.
- [05-metadata-simulation](05-metadata-simulation.md), because queue-enabled table validation belongs in metadata.
- [06-security-model](06-security-model.md), because queue operations read and mutate records on behalf of users and teams.

Recommended immediate next step:

1. add typed queue/queue-item setup models and initialization into the data store;
2. implement `AddToQueueRequest` and `RemoveFromQueueRequest` over a shared queue service;
3. add metadata validation for queue-enabled target tables;
4. add pick, release, and route handlers after live parity fixtures clarify edge behavior.
