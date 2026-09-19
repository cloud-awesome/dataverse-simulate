using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public sealed class ExecuteTransactionRequestHandler(RequestHandlerRegistry requestHandlerRegistry) : IRequestHandler
{
    private const string RequestMessage = "ExecuteTransaction";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        RequestFailureHandler.Handle(options, RequestMessage);

        var executeTransactionRequest = (ExecuteTransactionRequest)request;
        var dataSnapshot = dataService.CreateSnapshot();
        var auditSnapshot = auditService.CreateSnapshot();
        var securitySnapshot = (options?.SimulatedSecurityModel as SimulatedSecurityModel)?.CreateSnapshot();
        var responses = new OrganizationResponseCollection();

        try
        {
            foreach (var innerRequest in executeTransactionRequest.Requests)
            {
                var innerResponse = requestHandlerRegistry.GetHandler(innerRequest)
                    .Handle(innerRequest, dataService, auditService, options);

                if (executeTransactionRequest.ReturnResponses == true)
                {
                    responses.Add(innerResponse);
                }
            }
        }
        catch
        {
            dataService.RestoreSnapshot(dataSnapshot);
            auditService.RestoreSnapshot(auditSnapshot);
            if (securitySnapshot is not null && options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel)
            {
                securityModel.RestoreSnapshot(securitySnapshot);
            }

            throw;
        }

        return new ExecuteTransactionResponse
        {
            ResponseName = RequestMessage,
            Results = new ParameterCollection
            {
                ["Responses"] = responses
            }
        };
    }
}
