using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RemoveMembersTeamRequestHandler : IRequestHandler
{
    private const string RequestMessage = "RemoveMembersTeam";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var removeRequest = (RemoveMembersTeamRequest)request;

        RequestFailureHandler.Handle(options, RequestMessage, removeRequest.TeamId);

        SecurityRequestValidator.DemandTeamExists(dataService, removeRequest.TeamId);
        foreach (var memberId in removeRequest.MemberIds)
        {
            SecurityRequestValidator.DemandUserExists(dataService, memberId);
        }

        if (options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel)
        {
            foreach (var memberId in removeRequest.MemberIds)
            {
                securityModel.RemoveTeamMember(removeRequest.TeamId, memberId);
                var membershipRows = dataService.Get("teammembership")
                    .Where(x =>
                        x.GetAttributeValue<Guid>("teamid") == removeRequest.TeamId &&
                        x.GetAttributeValue<Guid>("systemuserid") == memberId)
                    .ToList();

                foreach (var membershipRow in membershipRows)
                {
                    dataService.Delete(membershipRow);
                }
            }

            securityModel.Validate();
        }

        return new RemoveMembersTeamResponse { ResponseName = RequestMessage };
    }
}
