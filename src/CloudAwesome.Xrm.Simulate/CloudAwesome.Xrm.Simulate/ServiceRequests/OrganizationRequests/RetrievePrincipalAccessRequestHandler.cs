using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RetrievePrincipalAccessRequestHandler : IRequestHandler
{
    private const string RequestMessage = "RetrievePrincipalAccess";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var retrieveRequest = (RetrievePrincipalAccessRequest)request;

        RequestFailureHandler.Handle(options, RequestMessage, retrieveRequest.Target.Id);

        var target = dataService.Get(retrieveRequest.Target);
        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            target,
            SecurityPrivilege.Share,
            options);
        SecurityRequestValidator.DemandPrincipalExists(
            dataService,
            retrieveRequest.Principal);

        var rights = options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel
            ? CalculateEffectiveAccess(securityModel, retrieveRequest.Principal, target)
            : default;

        return new RetrievePrincipalAccessResponse
        {
            Results = new ParameterCollection { new("AccessRights", rights) },
            ResponseName = RequestMessage
        };
    }

    private static AccessRights CalculateEffectiveAccess(
        SimulatedSecurityModel securityModel,
        EntityReference principal,
        Entity target)
    {
        var context = new SecurityEvaluationContext(securityModel);
        var rights = default(AccessRights);

        foreach (var mapping in PrivilegeMappings)
        {
            if (PermissionsCalculator.CanAccessRecord(context, principal, target, mapping.Privilege).Allowed)
            {
                rights |= mapping.AccessRight;
            }
        }

        return rights;
    }

    private static readonly (SecurityPrivilege Privilege, AccessRights AccessRight)[] PrivilegeMappings =
    [
        (SecurityPrivilege.Read, AccessRights.ReadAccess),
        (SecurityPrivilege.Write, AccessRights.WriteAccess),
        (SecurityPrivilege.Delete, AccessRights.DeleteAccess),
        (SecurityPrivilege.Append, AccessRights.AppendAccess),
        (SecurityPrivilege.AppendTo, AccessRights.AppendToAccess),
        (SecurityPrivilege.Assign, AccessRights.AssignAccess),
        (SecurityPrivilege.Share, AccessRights.ShareAccess)
    ];
}
