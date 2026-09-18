using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.QueryParsers;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.Queues;

internal static class SimulatedQueueDataSeeder
{
    private const string QueueLogicalName = "queue";
    private const string QueueItemLogicalName = "queueitem";
    private const string QueueIdAttribute = "queueid";
    private const string QueueItemIdAttribute = "queueitemid";

    internal static void Seed(MockedEntityDataService dataService, ISimulatorOptions? options)
    {
        var queueModel = options?.Queues;
        if (queueModel is null)
        {
            return;
        }

        ValidateMetadataScaffold(options);
        ValidateDuplicateSetupIds(queueModel);

        foreach (var queue in queueModel.Queues)
        {
            var entity = CreateQueueEntity(queue, dataService);
            ValidateNoExistingEntity(dataService, QueueLogicalName, entity.Id);
            dataService.Add(entity);
        }

        foreach (var queueItem in queueModel.QueueItems)
        {
            var entity = CreateQueueItemEntity(queueItem, dataService, options);
            ValidateNoExistingEntity(dataService, QueueItemLogicalName, entity.Id);
            dataService.Add(entity);
        }
    }

    private static Entity CreateQueueEntity(
        SimulatedQueue queue,
        MockedEntityDataService dataService)
    {
        if (queue.QueueId == Guid.Empty)
        {
            throw new SimulatedQueueException("Simulated queue QueueId must not be empty.");
        }

        var entity = CloneOrCreate(queue.Entity, QueueLogicalName);
        ResolvePrimaryId(entity, QueueIdAttribute, queue.QueueId, "queue");

        SetOrValidateAttribute(entity, "name", queue.Name, "queue name");
        SetOrValidateAttribute(entity, "ownerid", queue.OwnerId ?? dataService.AuthenticatedUser, "queue owner");
        SetOrValidateAttribute(entity, "businessunitid", queue.BusinessUnitId ?? dataService.BusinessUnit, "queue business unit");
        SetOrValidateAttribute(entity, "isdefault", queue.IsDefault, "queue default flag");
        ApplyAuditDefaults(entity, dataService);

        return entity;
    }

    private static Entity CreateQueueItemEntity(
        SimulatedQueueItem queueItem,
        MockedEntityDataService dataService,
        ISimulatorOptions? options)
    {
        if (queueItem.QueueItemId == Guid.Empty)
        {
            throw new SimulatedQueueException("Simulated queue item QueueItemId must not be empty.");
        }

        ValidateQueueReference(queueItem.QueueId);
        dataService.Get(queueItem.QueueId);
        dataService.Get(queueItem.ObjectId);
        ValidateTargetIsQueueEnabled(queueItem.ObjectId, options);

        var entity = CloneOrCreate(queueItem.Entity, QueueItemLogicalName);
        ResolvePrimaryId(entity, QueueItemIdAttribute, queueItem.QueueItemId, "queue item");

        SetOrValidateAttribute(entity, "queueid", queueItem.QueueId, "queue item queue");
        SetOrValidateAttribute(entity, "objectid", queueItem.ObjectId, "queue item object");
        SetOrValidateAttribute(entity, "workerid", queueItem.WorkerId, "queue item worker");
        SetOrValidateAttribute(entity, "enteredon", queueItem.EnteredOn ?? dataService.SystemTime, "queue item entered-on");
        ApplyAuditDefaults(entity, dataService);

        return entity;
    }

    private static Entity CloneOrCreate(Entity? entity, string logicalName)
    {
        if (entity is null)
        {
            return new Entity(logicalName);
        }

        if (!string.Equals(entity.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new SimulatedQueueException(
                $"Simulated {logicalName} entity must use logical name '{logicalName}', but got '{entity.LogicalName}'.");
        }

        return EntityCloner.Clone(entity);
    }

    private static void ResolvePrimaryId(
        Entity entity,
        string primaryIdAttribute,
        Guid typedId,
        string setupName)
    {
        if (entity.Id != Guid.Empty && entity.Id != typedId)
        {
            throw new SimulatedQueueException(
                $"Simulated {setupName} entity id '{entity.Id}' conflicts with configured id '{typedId}'.");
        }

        if (entity.Attributes.TryGetValue(primaryIdAttribute, out var value)
            && value is Guid attributeId
            && attributeId != Guid.Empty
            && attributeId != typedId)
        {
            throw new SimulatedQueueException(
                $"Simulated {setupName} attribute '{primaryIdAttribute}' value '{attributeId}' conflicts with configured id '{typedId}'.");
        }

        entity.Id = typedId;
        entity[primaryIdAttribute] = typedId;
    }

    private static void SetOrValidateAttribute(
        Entity entity,
        string attributeName,
        object? value,
        string description)
    {
        if (value is null)
        {
            return;
        }

        if (entity.Attributes.TryGetValue(attributeName, out var existing)
            && existing is not null
            && !ValuesMatch(existing, value))
        {
            throw new SimulatedQueueException(
                $"Simulated {description} attribute '{attributeName}' conflicts with configured value.");
        }

        entity.SetAttributeIfEmpty(attributeName, value);
    }

    private static bool ValuesMatch(object existing, object configured)
    {
        if (existing is EntityReference existingReference && configured is EntityReference configuredReference)
        {
            return string.Equals(existingReference.LogicalName, configuredReference.LogicalName, StringComparison.OrdinalIgnoreCase)
                   && existingReference.Id == configuredReference.Id;
        }

        return Equals(existing, configured);
    }

    private static void ApplyAuditDefaults(Entity entity, MockedEntityDataService dataService)
    {
        entity.SetAttributeIfEmpty(EntityConstants.CreatedOn, dataService.SystemTime);
        entity.SetAttributeIfEmpty(EntityConstants.ModifiedOn, dataService.SystemTime);
        entity.SetAttributeIfEmpty(EntityConstants.CreatedBy, dataService.AuthenticatedUser);
        entity.SetAttributeIfEmpty(EntityConstants.ModifiedBy, dataService.AuthenticatedUser);
        entity.SetAttributeIfEmpty(EntityConstants.OwnerId, dataService.AuthenticatedUser);
    }

    private static void ValidateQueueReference(EntityReference queueReference)
    {
        if (!string.Equals(queueReference.LogicalName, QueueLogicalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new SimulatedQueueException(
                $"Simulated queue item QueueId must reference logical name '{QueueLogicalName}', but got '{queueReference.LogicalName}'.");
        }

        if (queueReference.Id == Guid.Empty)
        {
            throw new SimulatedQueueException("Simulated queue item QueueId must not be empty.");
        }
    }

    private static void ValidateTargetIsQueueEnabled(EntityReference target, ISimulatorOptions? options)
    {
        var metadata = options?.Metadata;
        if (metadata is null)
        {
            return;
        }

        var targetMetadata = metadata.GetEntity(target.LogicalName);
        if (targetMetadata.IsValidForQueue == false)
        {
            throw new SimulatedMetadataException(
                $"Entity '{target.LogicalName}' is not enabled for queues.");
        }
    }

    private static void ValidateMetadataScaffold(ISimulatorOptions? options)
    {
        var metadata = options?.Metadata;
        if (metadata is null)
        {
            return;
        }

        metadata.GetEntity(QueueLogicalName);
        metadata.GetEntity(QueueItemLogicalName);
    }

    private static void ValidateDuplicateSetupIds(SimulatedQueueModel queueModel)
    {
        var duplicateQueue = queueModel.Queues
            .GroupBy(queue => queue.QueueId)
            .FirstOrDefault(group => group.Key != Guid.Empty && group.Count() > 1);

        if (duplicateQueue is not null)
        {
            throw new SimulatedQueueException($"Simulated queue '{duplicateQueue.Key}' is configured more than once.");
        }

        var duplicateQueueItem = queueModel.QueueItems
            .GroupBy(queueItem => queueItem.QueueItemId)
            .FirstOrDefault(group => group.Key != Guid.Empty && group.Count() > 1);

        if (duplicateQueueItem is not null)
        {
            throw new SimulatedQueueException($"Simulated queue item '{duplicateQueueItem.Key}' is configured more than once.");
        }
    }

    private static void ValidateNoExistingEntity(
        MockedEntityDataService dataService,
        string logicalName,
        Guid id)
    {
        if (dataService.Get(logicalName).Any(entity => entity.Id == id))
        {
            throw DataverseServiceFaults.DuplicateKey();
        }
    }
}
