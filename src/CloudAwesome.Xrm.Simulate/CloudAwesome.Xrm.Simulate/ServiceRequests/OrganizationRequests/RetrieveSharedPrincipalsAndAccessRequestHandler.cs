using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RetrieveSharedPrincipalsAndAccessRequestHandler : IRequestHandler
{
    private const string RequestMessage = "RetrieveSharedPrincipalsAndAccess";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var retrieveRequest = (RetrieveSharedPrincipalsAndAccessRequest)request;

        RequestFailureHandler.Handle(options, RequestMessage, retrieveRequest.Target.Id);

        var target = dataService.Get(retrieveRequest.Target);
        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            target,
            SecurityPrivilege.Share,
            options);

        var principalAccesses = options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel
            ? securityModel
                .GetSharedPrincipalsAndAccess(retrieveRequest.Target)
                .Select(access => new PrincipalAccess
                {
                    Principal = access.Principal,
                    AccessMask = access.AccessRights
                })
                .ToArray()
            : [];

        return new RetrieveSharedPrincipalsAndAccessResponse
        {
            Results = new ParameterCollection { new("PrincipalAccesses", principalAccesses) },
            ResponseName = RequestMessage
        };
    }
}
