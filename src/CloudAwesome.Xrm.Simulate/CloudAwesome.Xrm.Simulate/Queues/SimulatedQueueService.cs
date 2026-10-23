using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.QueryParsers;
using CloudAwesome.Xrm.Simulate.SecurityModel;
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
        RequestFailureHandler.Handle(options, AddToQueueMessage, target.Id);
        if (sourceQueueId == Guid.Empty) sourceQueueId = null;

        var destinationQueue = new EntityReference(QueueLogicalName, destinationQueueId);
        ValidateQueueReference(destinationQueue, "destination queue");
        var destinationQueueRecord = dataService.Get(destinationQueue);
        var targetRecord = dataService.Get(target);
        ValidateTargetIsQueueEnabled(target);
        ValidateQueueItemProperties(queueItemProperties);

        var security = new SimulatedSecurityEnforcer(dataService);
        security.DemandRecordAccess(targetRecord, SecurityPrivilege.Read, options);
        security.DemandRecordAccess(destinationQueueRecord, SecurityPrivilege.Read, options);
        security.DemandTableAccess(QueueItemLogicalName, SecurityPrivilege.Create, options);

        var existing = FindSourceQueueItem(target, sourceQueueId);
        if (sourceQueueId.HasValue && existing is null)
            throw DataverseServiceFaults.MissingQueueSource(target.Id, sourceQueueId.Value);
        if (existing is not null)
        {
            var sourceQueue = dataService.Get(existing.GetAttributeValue<EntityReference>(QueueIdAttribute));
            security.DemandRecordAccess(sourceQueue, SecurityPrivilege.Read, options);
            security.DemandRecordAccess(existing, SecurityPrivilege.Write, options);
        }

        var queueItemId = existing?.Id ?? ResolveQueueItemId(queueItemProperties);
        if (existing is null) ValidateNoExistingQueueItem(queueItemId);
        var queueItem = existing is null ? new Entity(QueueItemLogicalName) : EntityCloner.Clone(existing);
        if (queueItemProperties is not null)
            foreach (var property in EntityCloner.Clone(queueItemProperties).Attributes)
                if (property.Key is not (QueueItemIdAttribute or QueueIdAttribute or "objectid"))
                    queueItem[property.Key] = property.Value;

        ValidateWorker(queueItem.GetAttributeValue<EntityReference>("workerid"));
        ResolvePrimaryId(queueItem, queueItemId);
        queueItem[QueueIdAttribute] = destinationQueue;
        queueItem["objectid"] = new EntityReference(target.LogicalName, target.Id);
        queueItem.SetAttributeIfEmpty("enteredon", dataService.SystemTime);
        ApplyAuditDefaults(queueItem);

        if (existing is null) dataService.Add(queueItem);
        else
        {
            Touch(queueItem);
            dataService.Update(queueItem);
        }
        auditService.Add(AddToQueueMessage, QueueItemLogicalName, queueItem.Id);

        return queueItem.Id;
    }

    public void RemoveFromQueue(Guid queueItemId)
    {
        RequestFailureHandler.Handle(options, RemoveFromQueueMessage, queueItemId);
        var queueItem = GetQueueItem(queueItemId);
        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            queueItem,
            SecurityPrivilege.Delete,
            options);

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
        RequestFailureHandler.Handle(options, PickFromQueueMessage, queueItemId);

        ValidateWorker(worker);
        var queueItem = EntityCloner.Clone(GetQueueItem(queueItemId));
        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            queueItem,
            SecurityPrivilege.Write,
            options);

        if (removeQueueItem)
        {
            new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(queueItem, SecurityPrivilege.Delete, options);
            dataService.Delete(QueueItemLogicalName, queueItemId);
            auditService.Add(PickFromQueueMessage, QueueItemLogicalName, queueItemId);
            return;
        }

        queueItem["workerid"] = new EntityReference(worker.LogicalName, worker.Id);
        Touch(queueItem);
        dataService.Update(queueItem);
        auditService.Add(PickFromQueueMessage, QueueItemLogicalName, queueItemId);
    }

    public void ReleaseToQueue(Guid queueItemId)
    {
        RequestFailureHandler.Handle(options, ReleaseToQueueMessage, queueItemId);
        var queueItem = EntityCloner.Clone(GetQueueItem(queueItemId));
        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            queueItem,
            SecurityPrivilege.Write,
            options);

        queueItem["workerid"] = null!;
        Touch(queueItem);
        dataService.Update(queueItem);
        auditService.Add(ReleaseToQueueMessage, QueueItemLogicalName, queueItemId);
    }

    public void RouteTo(Guid queueItemId, EntityReference target)
    {
        ArgumentNullException.ThrowIfNull(target);
        RequestFailureHandler.Handle(options, RouteToMessage, queueItemId);

        var queueItem = EntityCloner.Clone(GetQueueItem(queueItemId));
        var targetRecord = dataService.Get(target);
        var security = new SimulatedSecurityEnforcer(dataService);
        security.DemandRecordAccess(queueItem, SecurityPrivilege.Write, options);
        security.DemandRecordAccess(targetRecord, SecurityPrivilege.Read, options);

        switch (target.LogicalName)
        {
            case QueueLogicalName:
                security.DemandRecordAccess(targetRecord, SecurityPrivilege.Write, options);
                queueItem["queueid"] = new EntityReference(target.LogicalName, target.Id);
                queueItem["workerid"] = null!;
                break;
            case "systemuser":
            case "team":
                queueItem["workerid"] = new EntityReference(target.LogicalName, target.Id);
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

    private Entity? FindSourceQueueItem(EntityReference target, Guid? sourceQueueId)
    {
        var candidates = dataService
            .Get(QueueItemLogicalName)
            .Where(queueItem => ReferencesMatch(queueItem.GetAttributeValue<EntityReference>("objectid"), target)
                && (!sourceQueueId.HasValue || queueItem.GetAttributeValue<EntityReference>("queueid")?.Id == sourceQueueId.Value))
            .ToList();
        if (candidates.Count > 1)
            throw new SimulatedQueueException("Multiple source queue items match the target. Specify a unique source queue.");
        return candidates.SingleOrDefault();
    }

    private void ValidateWorker(EntityReference? worker)
    {
        if (worker is null) return;
        if (worker.Id == Guid.Empty) throw DataverseServiceFaults.EmptyGuid();
        if (worker.LogicalName is not ("systemuser" or "team"))
            throw new SimulatedQueueException("Queue item worker must reference a systemuser or team.");
        if (!dataService.Get(worker.LogicalName).Any(record => record.Id == worker.Id))
            throw DataverseServiceFaults.InvalidQueueWorker(worker);
    }

    private static Guid ResolveQueueItemId(Entity? queueItemProperties)
    {
        if (queueItemProperties is null)
        {
            return Guid.NewGuid();
        }

        var idFromEntity = queueItemProperties.Id;
        if (queueItemProperties.Attributes.TryGetValue(QueueItemIdAttribute, out var rawId) && rawId is not null && rawId is not Guid)
            throw new SimulatedQueueException("Queue item id must be a Guid.");
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

        if (queueItemProperties.Attributes.TryGetValue("workerid", out var worker) && worker is not null && worker is not EntityReference)
            throw new SimulatedQueueException("Queue item worker must be an EntityReference.");

        if (queueItemProperties.Attributes.TryGetValue("enteredon", out var enteredOn) && enteredOn is not null && enteredOn is not DateTime)
            throw new SimulatedQueueException("Queue item entered-on must be a DateTime.");
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
            throw DataverseServiceFaults.EmptyGuid();
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

    private static bool ReferencesMatch(EntityReference? left, EntityReference? right)
    {
        return left is not null
               && right is not null
               && left.Id == right.Id
               && string.Equals(left.LogicalName, right.LogicalName, StringComparison.OrdinalIgnoreCase);
    }
}
