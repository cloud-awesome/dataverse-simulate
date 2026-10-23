using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.QueryParsers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public sealed class UpsertRequestHandler : IRequestHandler
{
    private const string RequestMessage = "Upsert";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var upsertRequest = (UpsertRequest)request;
        var target = EntityCloner.Clone(upsertRequest.Target);

        var existing = ResolveExisting(dataService, target, options);
        if (existing is not null)
        {
            target.Id = existing.Id;
            foreach (var key in target.KeyAttributes.Keys)
                target.Attributes.Remove(key);
            target.KeyAttributes.Clear();
            RequestFailureHandler.Handle(options, RequestMessage, target.Id);
            new EntityUpdater(dataService, new SimulatorAuditService()).Update(target, options);
            auditService.Add(RequestMessage, target.LogicalName, target.Id);

            return new UpsertResponse
            {
                ResponseName = RequestMessage,
                Results = new ParameterCollection
                {
                    ["RecordCreated"] = false,
                    ["Target"] = new EntityReference(target.LogicalName, target.Id)
                }
            };
        }

        ApplyKeyAttributesToCreateTarget(target);
        var primaryId = options?.Metadata?.GetEntity(target.LogicalName).PrimaryIdAttribute ?? $"{target.LogicalName}id";
        if (target.Id == Guid.Empty && target.Attributes.TryGetValue(primaryId, out var value) && value is Guid keyId && keyId != Guid.Empty)
            target.Id = keyId;
        target.KeyAttributes.Clear();
        RequestFailureHandler.Handle(options, RequestMessage, target.Id);
        var createdId = new EntityCreator(dataService, new SimulatorAuditService()).Create(target, options);
        target.Id = createdId;
        auditService.Add(RequestMessage, target.LogicalName, createdId);

        return new UpsertResponse
        {
            ResponseName = RequestMessage,
            Results = new ParameterCollection
            {
                ["RecordCreated"] = true,
                ["Target"] = new EntityReference(target.LogicalName, createdId)
            }
        };
    }

    private static Entity? ResolveExisting(MockedEntityDataService dataService, Entity target, ISimulatorOptions? options)
    {
        var existingRecords = dataService.Get(target.LogicalName);
        if (target.Id != Guid.Empty)
        {
            return existingRecords.SingleOrDefault(entity => entity.Id == target.Id);
        }

        if (target.KeyAttributes.Count == 0)
        {
            return null;
        }

        var metadata = options?.Metadata?.GetEntity(target.LogicalName);
        if (metadata is not null)
            foreach (var key in target.KeyAttributes.Keys) metadata.GetAttribute(key);

        var primaryId = metadata?.PrimaryIdAttribute ?? $"{target.LogicalName}id";
        if (target.KeyAttributes.Count == 1 && target.KeyAttributes.TryGetValue(primaryId, out var id) && id is Guid guid)
            return existingRecords.SingleOrDefault(entity => entity.Id == guid);

        var matches = existingRecords.Where(entity =>
            target.KeyAttributes.All(keyAttribute =>
                entity.Attributes.TryGetValue(keyAttribute.Key, out var value) &&
                keyAttribute.Value is not null && AttributeValuesEqual(value, keyAttribute.Value))).ToList();

        // Dataverse permits an unmatched key to flow to Create. Declared-key validation
        // is required when a matching row would otherwise be updated.
        if (matches.Count > 0 && metadata is not null && !metadata.AlternateKeys.Any(key =>
                (key.KeyStatus is null || string.Equals(key.KeyStatus, "Active", StringComparison.OrdinalIgnoreCase)) &&
                key.AttributeLogicalNames.Count == target.KeyAttributes.Count &&
                key.AttributeLogicalNames.All(name => target.KeyAttributes.ContainsKey(name))))
            throw DataverseServiceFaults.InvalidUpsertKey(target.LogicalName, target.KeyAttributes.Keys, metadata.AlternateKeys);

        if (matches.Count > 1)
            throw DataverseServiceFaults.DuplicateKey();

        return matches.SingleOrDefault();
    }

    private static void ApplyKeyAttributesToCreateTarget(Entity target)
    {
        foreach (var keyAttribute in target.KeyAttributes)
        {
            if (!target.Attributes.ContainsKey(keyAttribute.Key))
            {
                target.Attributes.Add(keyAttribute.Key, keyAttribute.Value);
            }
        }
    }

    private static bool AttributeValuesEqual(object? left, object? right)
    {
        return (left, right) switch
        {
            (OptionSetValue leftOption, OptionSetValue rightOption) => leftOption.Value == rightOption.Value,
            (EntityReference leftReference, EntityReference rightReference) =>
                string.Equals(leftReference.LogicalName, rightReference.LogicalName, StringComparison.OrdinalIgnoreCase) && leftReference.Id == rightReference.Id,
            (Money leftMoney, Money rightMoney) => leftMoney.Value == rightMoney.Value,
            _ => Equals(left, right)
        };
    }
}
