using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RetrieveAttributeRequestHandler : IRequestHandler
{
    private const string RequestMessage = "RetrieveAttribute";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var retrieveRequest = (RetrieveAttributeRequest)request;
        var metadata = options?.Metadata
                       ?? throw new SimulatedMetadataException("Metadata retrieval requires simulator metadata to be loaded.");
        var entityLogicalName = DemandEntityLogicalName(retrieveRequest.EntityLogicalName);
        var attributeLogicalName = DemandAttributeLogicalName(retrieveRequest.LogicalName);

        RequestFailureHandler.Handle(options, RequestMessage);

        var entityMetadata = metadata.GetEntity(entityLogicalName);
        var attributeMetadata = SdkMetadataProjection.ToSdkAttributeMetadata(
            entityMetadata.GetAttribute(attributeLogicalName));

        return new RetrieveAttributeResponse
        {
            Results = new ParameterCollection
            {
                ["AttributeMetadata"] = attributeMetadata
            },
            ResponseName = RequestMessage
        };
    }

    private static string DemandEntityLogicalName(string? entityLogicalName)
    {
        return string.IsNullOrWhiteSpace(entityLogicalName)
            ? throw new SimulatedMetadataException("RetrieveAttributeRequest.EntityLogicalName is required.")
            : entityLogicalName;
    }

    private static string DemandAttributeLogicalName(string? attributeLogicalName)
    {
        return string.IsNullOrWhiteSpace(attributeLogicalName)
            ? throw new SimulatedMetadataException("RetrieveAttributeRequest.LogicalName is required.")
            : attributeLogicalName;
    }
}
