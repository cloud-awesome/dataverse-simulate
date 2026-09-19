using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
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
        var target = upsertRequest.Target;

        var existing = ResolveExisting(dataService, target);
        if (existing is not null)
        {
            target.Id = existing.Id;
            RequestFailureHandler.Handle(options, RequestMessage, target.Id);
            new EntityUpdater(dataService, auditService).Update(target, options);
            auditService.Add(RequestMessage, target.LogicalName, target.Id);

            return new UpsertResponse
            {
                ResponseName = RequestMessage,
                Results = new ParameterCollection
                {
                    ["RecordCreated"] = false,
                    ["Target"] = target.ToEntityReference()
                }
            };
        }

        ApplyKeyAttributesToCreateTarget(target);
        RequestFailureHandler.Handle(options, RequestMessage, target.Id);
        var createdId = new EntityCreator(dataService, auditService).Create(target, options);
        target.Id = createdId;
        auditService.Add(RequestMessage, target.LogicalName, createdId);

        return new UpsertResponse
        {
            ResponseName = RequestMessage,
            Results = new ParameterCollection
            {
                ["RecordCreated"] = true,
                ["Target"] = target.ToEntityReference()
            }
        };
    }

    private static Entity? ResolveExisting(MockedEntityDataService dataService, Entity target)
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

        return existingRecords.SingleOrDefault(entity =>
            target.KeyAttributes.All(keyAttribute =>
                entity.Attributes.TryGetValue(keyAttribute.Key, out var value) &&
                AttributeValuesEqual(value, keyAttribute.Value)));
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
                leftReference.LogicalName == rightReference.LogicalName && leftReference.Id == rightReference.Id,
            (Money leftMoney, Money rightMoney) => leftMoney.Value == rightMoney.Value,
            _ => Equals(left, right)
        };
    }
}
