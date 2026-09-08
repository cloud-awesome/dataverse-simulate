using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class AssociateRequestHandler : IRequestHandler
{
    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var associateRequest = (AssociateRequest)request;

        new EntityAssociator(dataService).Associate(
            associateRequest.Target.LogicalName,
            associateRequest.Target.Id,
            associateRequest.Relationship,
            associateRequest.RelatedEntities,
            options);

        return new AssociateResponse { ResponseName = "Associate" };
    }
}
