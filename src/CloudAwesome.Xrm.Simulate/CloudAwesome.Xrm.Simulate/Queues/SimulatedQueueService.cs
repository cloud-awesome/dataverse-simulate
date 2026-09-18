using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.QueryParsers;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.Queues;

public sealed class SimulatedQueueService(
    MockedEntityDataService dataService,
    SimulatorAuditService auditService,
    ISimulatorOptions? options = null)
{
    internal const string QueueLogicalName = "queue";
    internal const string QueueItemLogicalName = "queueitem";
    internal const string QueueIdAttribute = "queueid";
    internal const string QueueItemIdAttribute = "queueitemid";
    internal const string AddToQueueMessage = "AddToQueue";
    internal const string RemoveFromQueueMessage = "RemoveFromQueue";
    internal const string PickFromQueueMessage = "PickFromQueue";
    internal const string ReleaseToQueueMessage = "ReleaseToQueue";
    internal const string RouteToMessage = "RouteTo";

    public Guid AddToQueue(
        EntityReference target,
        Guid destinationQueueId,
        Guid? sourceQueueId = null,
        Entity? queueItemProperties = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        var destinationQueue = new EntityReference(QueueLogicalName, destinationQueueId);
        ValidateQueueReference(destinationQueue, "destination queue");
        dataService.Get(destinationQueue);
        dataService.Get(target);
        ValidateTargetIsQueueEnabled(target);
        ValidateQueueItemProperties(queueItemProperties);

        if (sourceQueueId is not null)
        {
            var sourceQueue = new EntityReference(QueueLogicalName, sourceQueueId.Value);
            ValidateQueueReference(sourceQueue, "source queue");
            dataService.Get(sourceQueue);
            RemoveExistingSourceQueueItem(target, sourceQueueId.Value);
        }

        var queueItemId = ResolveQueueItemId(queueItemProperties);
        ValidateNoExistingQueueItem(queueItemId);

        var queueItem = queueItemProperties is null
            ? new Entity(QueueItemLogicalName)
            : EntityCloner.Clone(queueItemProperties);

        ResolvePrimaryId(queueItem, queueItemId);
        SetOrValidateAttribute(queueItem, "queueid", destinationQueue, "queue item queue");
        SetOrValidateAttribute(queueItem, "objectid", target, "queue item object");
        queueItem.SetAttributeIfEmpty("enteredon", dataService.SystemTime);
        ApplyAuditDefaults(queueItem);

        dataService.Add(queueItem);
        auditService.Add(AddToQueueMessage, QueueItemLogicalName, queueItem.Id);

        return queueItem.Id;
    }

    public void RemoveFromQueue(Guid queueItemId)
    {
        var queueItem = GetQueueItem(queueItemId);

        dataService.Delete(queueItem);
        auditService.Add(RemoveFromQueueMessage, QueueItemLogicalName, queueItemId);
    }

    public void PickFromQueue(
        Guid queueItemId,
        Guid workerId,
        bool removeQueueItem)
    {
        PickFromQueue(queueItemId, new EntityReference("systemuser", workerId), removeQueueItem);
    }

    public void PickFromQueue(
        Guid queueItemId,
        EntityReference worker,
        bool removeQueueItem)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var queueItem = GetQueueItem(queueItemId);
        dataService.Get(worker);

        if (removeQueueItem)
        {
            dataService.Delete(queueItem);
            auditService.Add(PickFromQueueMessage, QueueItemLogicalName, queueItemId);
            return;
        }

        queueItem["workerid"] = worker;
        Touch(queueItem);
        dataService.Update(queueItem);
        auditService.Add(PickFromQueueMessage, QueueItemLogicalName, queueItemId);
    }

    public void ReleaseToQueue(Guid queueItemId)
    {
        var queueItem = GetQueueItem(queueItemId);

        queueItem["workerid"] = null!;
        Touch(queueItem);
        dataService.Update(queueItem);
        auditService.Add(ReleaseToQueueMessage, QueueItemLogicalName, queueItemId);
    }

    public void RouteTo(Guid queueItemId, EntityReference target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var queueItem = GetQueueItem(queueItemId);
        dataService.Get(target);

        switch (target.LogicalName)
        {
            case QueueLogicalName:
                queueItem["queueid"] = target;
                queueItem["workerid"] = null!;
                break;
            case "systemuser":
            case "team":
                queueItem["workerid"] = target;
                break;
            default:
                throw new SimulatedQueueException(
                    $"RouteTo target must be a queue, systemuser, or team, but got '{target.LogicalName}'.");
        }

        Touch(queueItem);
        dataService.Update(queueItem);
        auditService.Add(RouteToMessage, QueueItemLogicalName, queueItemId);
    }

    private Entity GetQueueItem(Guid queueItemId)
    {
        return dataService.Get(QueueItemLogicalName, queueItemId);
    }

    private void RemoveExistingSourceQueueItem(EntityReference target, Guid sourceQueueId)
    {
        var existing = dataService
            .Get(QueueItemLogicalName)
            .FirstOrDefault(queueItem =>
                ReferencesMatch(queueItem.GetAttributeValue<EntityReference>("queueid"), new EntityReference(QueueLogicalName, sourceQueueId))
                && ReferencesMatch(queueItem.GetAttributeValue<EntityReference>("objectid"), target));

        if (existing is not null)
        {
            dataService.Delete(existing);
        }
    }

    private static Guid ResolveQueueItemId(Entity? queueItemProperties)
    {
        if (queueItemProperties is null)
        {
            return Guid.NewGuid();
        }

        var idFromEntity = queueItemProperties.Id;
        var idFromAttribute = queueItemProperties.Attributes.TryGetValue(QueueItemIdAttribute, out var value) && value is Guid attributeId
            ? attributeId
            : Guid.Empty;

        if (idFromEntity != Guid.Empty
            && idFromAttribute != Guid.Empty
            && idFromEntity != idFromAttribute)
        {
            throw new SimulatedQueueException(
                $"Queue item property '{QueueItemIdAttribute}' value '{idFromAttribute}' conflicts with entity id '{idFromEntity}'.");
        }

        if (idFromEntity != Guid.Empty)
        {
            return idFromEntity;
        }

        return idFromAttribute != Guid.Empty
            ? idFromAttribute
            : Guid.NewGuid();
    }

    private static void ResolvePrimaryId(Entity queueItem, Guid queueItemId)
    {
        queueItem.Id = queueItemId;
        queueItem[QueueItemIdAttribute] = queueItemId;
    }

    private static void ValidateQueueItemProperties(Entity? queueItemProperties)
    {
        if (queueItemProperties is null)
        {
            return;
        }

        if (!string.Equals(queueItemProperties.LogicalName, QueueItemLogicalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new SimulatedQueueException(
                $"Queue item properties must use logical name '{QueueItemLogicalName}', but got '{queueItemProperties.LogicalName}'.");
        }
    }

    private static void ValidateQueueReference(EntityReference queueReference, string description)
    {
        if (!string.Equals(queueReference.LogicalName, QueueLogicalName, StringComparison.OrdinalIgnoreCase))
        {
            throw new SimulatedQueueException(
                $"The {description} must reference logical name '{QueueLogicalName}', but got '{queueReference.LogicalName}'.");
        }

        if (queueReference.Id == Guid.Empty)
        {
            throw new SimulatedQueueException($"The {description} id must not be empty.");
        }
    }

    private void ValidateTargetIsQueueEnabled(EntityReference target)
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

    private void ValidateNoExistingQueueItem(Guid queueItemId)
    {
        if (dataService.Get(QueueItemLogicalName).Any(entity => entity.Id == queueItemId))
        {
            throw DataverseServiceFaults.DuplicateKey();
        }
    }

    private void ApplyAuditDefaults(Entity entity)
    {
        entity.SetAttributeIfEmpty(EntityConstants.CreatedOn, dataService.SystemTime);
        entity.SetAttributeIfEmpty(EntityConstants.ModifiedOn, dataService.SystemTime);
        entity.SetAttributeIfEmpty(EntityConstants.CreatedBy, dataService.AuthenticatedUser);
        entity.SetAttributeIfEmpty(EntityConstants.ModifiedBy, dataService.AuthenticatedUser);
        entity.SetAttributeIfEmpty(EntityConstants.OwnerId, dataService.AuthenticatedUser);
    }

    private void Touch(Entity entity)
    {
        entity[EntityConstants.ModifiedOn] = dataService.SystemTime;
        entity[EntityConstants.ModifiedBy] = dataService.AuthenticatedUser;
    }

    private static void SetOrValidateAttribute(
        Entity entity,
        string attributeName,
        object value,
        string description)
    {
        if (entity.Attributes.TryGetValue(attributeName, out var existing)
            && existing is not null
            && !ValuesMatch(existing, value))
        {
            throw new SimulatedQueueException(
                $"Simulated {description} attribute '{attributeName}' conflicts with configured value.");
        }

        entity[attributeName] = value;
    }

    private static bool ValuesMatch(object existing, object configured)
    {
        if (existing is EntityReference existingReference && configured is EntityReference configuredReference)
        {
            return ReferencesMatch(existingReference, configuredReference);
        }

        return Equals(existing, configured);
    }

    private static bool ReferencesMatch(EntityReference? left, EntityReference? right)
    {
        return left is not null
               && right is not null
               && left.Id == right.Id
               && string.Equals(left.LogicalName, right.LogicalName, StringComparison.OrdinalIgnoreCase);
    }
}
