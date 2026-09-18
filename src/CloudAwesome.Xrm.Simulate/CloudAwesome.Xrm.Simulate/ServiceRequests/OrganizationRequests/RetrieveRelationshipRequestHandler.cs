using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RetrieveRelationshipRequestHandler : IRequestHandler
{
    private const string RequestMessage = "RetrieveRelationship";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var retrieveRequest = (RetrieveRelationshipRequest)request;
        var metadata = options?.Metadata
                       ?? throw new SimulatedMetadataException("Metadata retrieval requires simulator metadata to be loaded.");
        var schemaName = DemandSchemaName(retrieveRequest.Name);

        RequestFailureHandler.Handle(options, RequestMessage);

        var relationship = metadata.Entities
            .SelectMany(entity => entity.Relationships)
            .FirstOrDefault(candidate => string.Equals(candidate.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase))
            ?? throw new SimulatedMetadataException($"Metadata does not define relationship '{schemaName}'.");

        return new RetrieveRelationshipResponse
        {
            Results = new ParameterCollection
            {
                ["RelationshipMetadata"] = SdkMetadataProjection.ToSdkRelationshipMetadata(relationship)
            },
            ResponseName = RequestMessage
        };
    }

    private static string DemandSchemaName(string? schemaName)
    {
        return string.IsNullOrWhiteSpace(schemaName)
            ? throw new SimulatedMetadataException("RetrieveRelationshipRequest.Name is required.")
            : schemaName;
    }
}
