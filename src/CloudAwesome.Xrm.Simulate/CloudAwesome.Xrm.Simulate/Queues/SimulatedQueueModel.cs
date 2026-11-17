using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.Queues;

public sealed class SimulatedQueueModel
{
    public List<SimulatedQueue> Queues { get; } = [];

    public List<SimulatedQueueItem> QueueItems { get; } = [];

    public static SimulatedQueueModel Create()
    {
        return new SimulatedQueueModel();
    }

	/// <summary>Declares a queue fixture without executing queue requests.</summary>
	/// <param name="queueId">The non-empty queue id.</param>
	/// <param name="name">The optional queue name.</param>
	/// <param name="ownerId">The owner, defaulting to the configured user.</param>
	/// <param name="businessUnitId">The business unit, defaulting to the configured unit.</param>
	/// <param name="isDefault">Whether this is a default queue.</param>
	/// <param name="entity">Optional additional queue attributes to copy during seeding.</param>
	/// <returns>This model for further queue fixture declarations.</returns>
	/// <remarks>References and duplicate ids are validated when the complete fixture is applied.</remarks>
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

	/// <summary>Declares a queue-item fixture with an existing queue and target.</summary>
	/// <param name="queueItemId">The non-empty queue-item id.</param>
	/// <param name="queueId">The queue id or reference, which must exist in the completed fixture.</param>
	/// <param name="objectId">The target record reference.</param>
	/// <param name="workerId">The optional worker reference.</param>
	/// <param name="enteredOn">The entry time, defaulting to simulated time.</param>
	/// <param name="entity">Optional additional queue-item attributes to copy during seeding.</param>
	/// <returns>This model for further queue fixture declarations.</returns>
	/// <remarks>Metadata queue capability and references are checked by the existing queue seeder.</remarks>
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

	/// <summary>Declares a queue-item fixture with an existing queue and target.</summary>
	/// <param name="queueItemId">The non-empty queue-item id.</param>
	/// <param name="queueId">The queue id or reference, which must exist in the completed fixture.</param>
	/// <param name="objectId">The target record reference.</param>
	/// <param name="workerId">The optional worker reference.</param>
	/// <param name="enteredOn">The entry time, defaulting to simulated time.</param>
	/// <param name="entity">Optional additional queue-item attributes to copy during seeding.</param>
	/// <returns>This model for further queue fixture declarations.</returns>
	/// <remarks>Metadata queue capability and references are checked by the existing queue seeder.</remarks>
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
