using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RetrieveAllEntitiesRequestHandler : IRequestHandler
{
    private const string RequestMessage = "RetrieveAllEntities";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var retrieveRequest = (RetrieveAllEntitiesRequest)request;
        var metadata = options?.Metadata
                       ?? throw new SimulatedMetadataException("Metadata retrieval requires simulator metadata to be loaded.");

        RequestFailureHandler.Handle(options, RequestMessage);

        var entityMetadata = metadata.Entities
            .Select(entity => SdkMetadataProjection.ToSdkEntityMetadata(entity, retrieveRequest.EntityFilters))
            .ToArray();

        return new RetrieveAllEntitiesResponse
        {
            Results = new ParameterCollection
            {
                ["EntityMetadata"] = entityMetadata
            },
            ResponseName = RequestMessage
        };
    }
}
