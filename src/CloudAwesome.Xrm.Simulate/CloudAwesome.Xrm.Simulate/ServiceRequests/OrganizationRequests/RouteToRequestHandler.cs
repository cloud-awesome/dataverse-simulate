using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Queues;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RouteToRequestHandler : IRequestHandler
{
    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var routeRequest = (RouteToRequest)request;

        RequestFailureHandler.Handle(
            options,
            SimulatedQueueService.RouteToMessage,
            routeRequest.QueueItemId);

        new SimulatedQueueService(dataService, auditService, options)
            .RouteTo(routeRequest.QueueItemId, routeRequest.Target);

        return new RouteToResponse
        {
            ResponseName = SimulatedQueueService.RouteToMessage
        };
    }
}
