using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Queues;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class ReleaseToQueueRequestHandler : IRequestHandler
{
    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var releaseRequest = (ReleaseToQueueRequest)request;

        RequestFailureHandler.Handle(
            options,
            SimulatedQueueService.ReleaseToQueueMessage,
            releaseRequest.QueueItemId);

        new SimulatedQueueService(dataService, auditService, options)
            .ReleaseToQueue(releaseRequest.QueueItemId);

        return new ReleaseToQueueResponse
        {
            ResponseName = SimulatedQueueService.ReleaseToQueueMessage
        };
    }
}
