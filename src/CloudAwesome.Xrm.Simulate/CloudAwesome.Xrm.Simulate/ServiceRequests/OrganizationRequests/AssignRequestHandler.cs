using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class AssignRequestHandler : IRequestHandler
{
    private const string RequestMessage = "Assign";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var assignRequest = (AssignRequest)request;

        RequestFailureHandler.Handle(options, RequestMessage, assignRequest.Target.Id);

        var entity = dataService.Get(assignRequest.Target);
        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            entity,
            SecurityPrivilege.Assign,
            options);

        var changes = new Entity(entity.LogicalName, entity.Id) { ["ownerid"] = assignRequest.Assignee };
        new RecordOwnership(dataService, options).PrepareUpdate(changes, entity, assign: true);
        dataService.Update(changes);
        auditService.Add(RequestMessage, entity.LogicalName, entity.Id);

        return new AssignResponse { ResponseName = "Assign" };
    }
}
