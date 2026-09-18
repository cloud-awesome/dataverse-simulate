using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RetrieveEntityRequestHandler : IRequestHandler
{
    private const string RequestMessage = "RetrieveEntity";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var retrieveRequest = (RetrieveEntityRequest)request;
        var metadata = DemandMetadata(options);
        var entityLogicalName = DemandEntityLogicalName(retrieveRequest.LogicalName);

        RequestFailureHandler.Handle(options, RequestMessage);

        var entityMetadata = SdkMetadataProjection.ToSdkEntityMetadata(
            metadata.GetEntity(entityLogicalName),
            retrieveRequest.EntityFilters);

        return new RetrieveEntityResponse
        {
            Results = new ParameterCollection
            {
                ["EntityMetadata"] = entityMetadata
            },
            ResponseName = RequestMessage
        };
    }

    private static SimulatedMetadata DemandMetadata(ISimulatorOptions? options)
    {
        return options?.Metadata
               ?? throw new SimulatedMetadataException("Metadata retrieval requires simulator metadata to be loaded.");
    }

    private static string DemandEntityLogicalName(string? entityLogicalName)
    {
        return string.IsNullOrWhiteSpace(entityLogicalName)
            ? throw new SimulatedMetadataException("RetrieveEntityRequest.EntityLogicalName is required.")
            : entityLogicalName;
    }
}
