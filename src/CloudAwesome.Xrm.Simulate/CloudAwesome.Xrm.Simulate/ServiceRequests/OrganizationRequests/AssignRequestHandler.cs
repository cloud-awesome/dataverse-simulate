using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class AssignRequestHandler: IRequestHandler
{
	private const string RequestMessage = "Assign";
	
	public OrganizationResponse Handle(OrganizationRequest request, MockedEntityDataService dataService,
		SimulatorAuditService auditService, ISimulatorOptions? options = null)
	{
		var assignRequest = (AssignRequest) request;

		RequestFailureHandler.Handle(options, RequestMessage, assignRequest.Target.Id);
		
		var entity = dataService.Get(assignRequest.Target);

		switch (assignRequest.Assignee.LogicalName)
		{
			case "systemuser":
				entity["owninguser"] = assignRequest.Assignee;
				break;
			case "team":
				entity["owningteam"] = assignRequest.Assignee;
				break;
		}

		entity["ownerid"] = assignRequest.Assignee;
		dataService.Update(entity);
		
		return new AssignResponse { ResponseName = "Assign" };
	}
}