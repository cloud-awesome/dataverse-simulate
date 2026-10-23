using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class DeleteRequestHandler : IRequestHandler
{
    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var deleteRequest = (DeleteRequest)request;

        new EntityDeleter(dataService, auditService).Delete(
            deleteRequest.Target.LogicalName,
            deleteRequest.Target.Id,
            options);

        return new DeleteResponse { ResponseName = "Delete" };
    }
}
