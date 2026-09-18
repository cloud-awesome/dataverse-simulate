using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate;

public sealed class SimulatedQueueModel
{
    public List<SimulatedQueue> Queues { get; } = [];

    public List<SimulatedQueueItem> QueueItems { get; } = [];

    public static SimulatedQueueModel Create()
    {
        return new SimulatedQueueModel();
    }

    public SimulatedQueueModel WithQueue(
        Guid queueId,
        string? name = null,
        EntityReference? ownerId = null,
        EntityReference? businessUnitId = null,
        bool isDefault = false,
        Entity? entity = null)
    {
        Queues.Add(new SimulatedQueue
        {
            QueueId = queueId,
            Name = name,
            OwnerId = ownerId,
            BusinessUnitId = businessUnitId,
            IsDefault = isDefault,
            Entity = entity
        });

        return this;
    }

    public SimulatedQueueModel WithQueueItem(
        Guid queueItemId,
        Guid queueId,
        EntityReference objectId,
        EntityReference? workerId = null,
        DateTime? enteredOn = null,
        Entity? entity = null)
    {
        QueueItems.Add(new SimulatedQueueItem
        {
            QueueItemId = queueItemId,
            QueueId = new EntityReference("queue", queueId),
            ObjectId = objectId,
            WorkerId = workerId,
            EnteredOn = enteredOn,
            Entity = entity
        });

        return this;
    }

    public SimulatedQueueModel WithQueueItem(
        Guid queueItemId,
        EntityReference queueId,
        EntityReference objectId,
        EntityReference? workerId = null,
        DateTime? enteredOn = null,
        Entity? entity = null)
    {
        QueueItems.Add(new SimulatedQueueItem
        {
            QueueItemId = queueItemId,
            QueueId = queueId,
            ObjectId = objectId,
            WorkerId = workerId,
            EnteredOn = enteredOn,
            Entity = entity
        });

        return this;
    }
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
