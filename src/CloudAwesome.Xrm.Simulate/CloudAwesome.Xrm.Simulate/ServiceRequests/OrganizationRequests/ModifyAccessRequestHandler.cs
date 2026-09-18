using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class ModifyAccessRequestHandler : IRequestHandler
{
    private const string RequestMessage = "ModifyAccess";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var modifyRequest = (ModifyAccessRequest)request;

        RequestFailureHandler.Handle(options, RequestMessage, modifyRequest.Target.Id);

        var target = dataService.Get(modifyRequest.Target);
        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            target,
            SecurityPrivilege.Share,
            options);
        SecurityRequestValidator.DemandPrincipalExists(
            dataService,
            modifyRequest.PrincipalAccess.Principal);

        if (options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel)
        {
            securityModel
                .ModifyAccess(
                    modifyRequest.Target,
                    modifyRequest.PrincipalAccess.Principal,
                    modifyRequest.PrincipalAccess.AccessMask)
                .Validate();
        }

        return new ModifyAccessResponse { ResponseName = RequestMessage };
    }
}
