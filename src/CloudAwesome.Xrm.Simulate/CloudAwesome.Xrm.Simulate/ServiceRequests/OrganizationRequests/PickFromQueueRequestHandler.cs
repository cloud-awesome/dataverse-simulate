using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Queues;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class PickFromQueueRequestHandler : IRequestHandler
{
    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var pickRequest = (PickFromQueueRequest)request;

        RequestFailureHandler.Handle(
            options,
            SimulatedQueueService.PickFromQueueMessage,
            pickRequest.QueueItemId);

        new SimulatedQueueService(dataService, auditService, options)
            .PickFromQueue(
                pickRequest.QueueItemId,
                pickRequest.WorkerId,
                pickRequest.RemoveQueueItem);

        return new PickFromQueueResponse
        {
            ResponseName = SimulatedQueueService.PickFromQueueMessage
        };
    }
}
