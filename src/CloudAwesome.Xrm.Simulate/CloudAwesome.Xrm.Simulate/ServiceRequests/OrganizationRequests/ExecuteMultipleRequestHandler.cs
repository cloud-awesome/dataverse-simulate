using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public sealed class ExecuteMultipleRequestHandler(RequestHandlerRegistry requestHandlerRegistry) : IRequestHandler
{
    private const string RequestMessage = "ExecuteMultiple";

    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        RequestFailureHandler.Handle(options, RequestMessage);

        var executeMultipleRequest = (ExecuteMultipleRequest)request;
        var settings = executeMultipleRequest.Settings ?? new ExecuteMultipleSettings();
        var response = new ExecuteMultipleResponse
        {
            ResponseName = RequestMessage,
            Results = new ParameterCollection
            {
                ["IsFaulted"] = false,
                ["Responses"] = new ExecuteMultipleResponseItemCollection()
            }
        };

        for (var index = 0; index < executeMultipleRequest.Requests.Count; index++)
        {
            var innerRequest = executeMultipleRequest.Requests[index];

            try
            {
                var innerResponse = requestHandlerRegistry.GetHandler(innerRequest)
                    .Handle(innerRequest, dataService, auditService, options);

                if (settings.ReturnResponses)
                {
                    response.Responses.Add(new ExecuteMultipleResponseItem
                    {
                        RequestIndex = index,
                        Response = innerResponse
                    });
                }
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                response.Results["IsFaulted"] = true;
                response.Responses.Add(new ExecuteMultipleResponseItem
                {
                    RequestIndex = index,
                    Fault = ex.Detail
                });

                if (!settings.ContinueOnError)
                {
                    break;
                }
            }
        }

        return response;
    }
}
