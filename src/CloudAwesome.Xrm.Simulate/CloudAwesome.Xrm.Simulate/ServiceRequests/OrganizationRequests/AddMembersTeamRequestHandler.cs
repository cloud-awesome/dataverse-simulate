using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class AddMembersTeamRequestHandler : IRequestHandler
{
    private const string RequestMessage = "AddMembersTeam";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var addRequest = (AddMembersTeamRequest)request;

        RequestFailureHandler.Handle(options, RequestMessage, addRequest.TeamId);

        SecurityRequestValidator.DemandTeamExists(dataService, addRequest.TeamId);
        foreach (var memberId in addRequest.MemberIds)
        {
            SecurityRequestValidator.DemandUserExists(dataService, memberId);
        }

        if (options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel)
        {
            foreach (var memberId in addRequest.MemberIds)
            {
                securityModel.AddTeamMember(addRequest.TeamId, memberId);
            }

            securityModel.Validate();
        }

        return new AddMembersTeamResponse { ResponseName = RequestMessage };
    }
}
