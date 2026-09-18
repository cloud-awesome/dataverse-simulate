using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Queues;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class AddToQueueRequestHandler : IRequestHandler
{
    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var addRequest = (AddToQueueRequest)request;

        ArgumentNullException.ThrowIfNull(addRequest.Target);

        RequestFailureHandler.Handle(
            options,
            SimulatedQueueService.AddToQueueMessage,
            addRequest.Target.Id);

        var sourceQueueId = addRequest.SourceQueueId == Guid.Empty
            ? (Guid?)null
            : addRequest.SourceQueueId;

        var queueItemId = new SimulatedQueueService(dataService, auditService, options)
            .AddToQueue(
                addRequest.Target,
                addRequest.DestinationQueueId,
                sourceQueueId,
                addRequest.QueueItemProperties);

        return new AddToQueueResponse
        {
            Results = new ParameterCollection { new("QueueItemId", queueItemId) },
            ResponseName = SimulatedQueueService.AddToQueueMessage
        };
    }
}
