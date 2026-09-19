using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public sealed class SetStateRequestHandler : IRequestHandler
{
    private const string RequestMessage = "SetState";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var setStateRequest = (SetStateRequest)request;
        var target = setStateRequest.EntityMoniker;
        var existing = dataService.Get(target.LogicalName, target.Id);

        RequestFailureHandler.Handle(options, RequestMessage, target.Id);

        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            existing,
            SecurityPrivilege.Write,
            options);

        var update = new Entity(target.LogicalName, target.Id)
        {
            ["statecode"] = setStateRequest.State,
            ["statuscode"] = setStateRequest.Status
        };

        MetadataValidator.ValidateUpdate(update, options);
        dataService.Update(update);
        auditService.Add(RequestMessage, target.LogicalName, target.Id);

        return new SetStateResponse { ResponseName = RequestMessage };
    }
}
