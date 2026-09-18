using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Queues;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RemoveFromQueueRequestHandler : IRequestHandler
{
    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var removeRequest = (RemoveFromQueueRequest)request;

        RequestFailureHandler.Handle(
            options,
            SimulatedQueueService.RemoveFromQueueMessage,
            removeRequest.QueueItemId);

        new SimulatedQueueService(dataService, auditService, options)
            .RemoveFromQueue(removeRequest.QueueItemId);

        return new RemoveFromQueueResponse
        {
            ResponseName = SimulatedQueueService.RemoveFromQueueMessage
        };
    }
}
