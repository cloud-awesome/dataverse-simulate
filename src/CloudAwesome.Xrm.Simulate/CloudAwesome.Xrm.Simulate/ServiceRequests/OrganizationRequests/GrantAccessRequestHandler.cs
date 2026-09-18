using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class GrantAccessRequestHandler : IRequestHandler
{
    private const string RequestMessage = "GrantAccess";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var grantRequest = (GrantAccessRequest)request;

        RequestFailureHandler.Handle(options, RequestMessage, grantRequest.Target.Id);

        var target = dataService.Get(grantRequest.Target);
        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            target,
            SecurityPrivilege.Share,
            options);
        SecurityRequestValidator.DemandPrincipalExists(
            dataService,
            grantRequest.PrincipalAccess.Principal);

        if (options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel)
        {
            securityModel
                .GrantAccess(
                    grantRequest.Target,
                    grantRequest.PrincipalAccess.Principal,
                    grantRequest.PrincipalAccess.AccessMask)
                .Validate();
        }

        return new GrantAccessResponse { ResponseName = RequestMessage };
    }
}
