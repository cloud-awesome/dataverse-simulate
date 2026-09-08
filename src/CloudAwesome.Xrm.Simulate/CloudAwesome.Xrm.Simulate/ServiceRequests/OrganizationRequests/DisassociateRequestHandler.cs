using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class DisassociateRequestHandler : IRequestHandler
{
    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var disassociateRequest = (DisassociateRequest)request;

        new EntityDisassociator(dataService).Disassociate(
            disassociateRequest.Target.LogicalName,
            disassociateRequest.Target.Id,
            disassociateRequest.Relationship,
            disassociateRequest.RelatedEntities,
            options);

        return new DisassociateResponse { ResponseName = "Disassociate" };
    }
}
