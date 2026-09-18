using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RevokeAccessRequestHandler : IRequestHandler
{
    private const string RequestMessage = "RevokeAccess";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var revokeRequest = (RevokeAccessRequest)request;

        RequestFailureHandler.Handle(options, RequestMessage, revokeRequest.Target.Id);

        var target = dataService.Get(revokeRequest.Target);
        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            target,
            SecurityPrivilege.Share,
            options);
        SecurityRequestValidator.DemandPrincipalExists(
            dataService,
            revokeRequest.Revokee);

        if (options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel)
        {
            securityModel
                .RevokeAccess(revokeRequest.Target, revokeRequest.Revokee)
                .Validate();
        }

        return new RevokeAccessResponse { ResponseName = RequestMessage };
    }
}
