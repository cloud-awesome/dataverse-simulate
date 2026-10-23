using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using System.ServiceModel;
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
        if (executeTransactionRequest.Requests.Any(inner => inner is ExecuteMultipleRequest or ExecuteTransactionRequest))
        {
            throw new FaultException<OrganizationServiceFault>(new OrganizationServiceFault
            {
                ErrorCode = -2147220715,
                Message = "ExecuteMultiple or ExecuteTransaction Requests cannot be executed inside another ExecuteTransaction request!"
            });
        }

        var dataSnapshot = dataService.CreateSnapshot();
        var auditSnapshot = auditService.CreateSnapshot();
        var securitySnapshot = (options?.SimulatedSecurityModel as SimulatedSecurityModel)?.CreateSnapshot();
        var responses = new OrganizationResponseCollection();
        var requestIndex = 0;

        try
        {
            for (; requestIndex < executeTransactionRequest.Requests.Count; requestIndex++)
            {
                var innerRequest = executeTransactionRequest.Requests[requestIndex];
                var innerResponse = requestHandlerRegistry.GetHandler(innerRequest)
                    .Handle(innerRequest, dataService, auditService, options);

                if (!request.Parameters.Contains("ReturnResponses") || executeTransactionRequest.ReturnResponses == true)
                {
                    responses.Add(innerResponse);
                }
            }
        }
        catch (Exception exception)
        {
            dataService.RestoreSnapshot(dataSnapshot);
            auditService.RestoreSnapshot(auditSnapshot);
            if (securitySnapshot is not null && options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel)
            {
                securityModel.RestoreSnapshot(securitySnapshot);
            }

            if (exception is FaultException<OrganizationServiceFault> serviceFault)
            {
                var fault = serviceFault.Detail;
                throw new FaultException<OrganizationServiceFault>(new ExecuteTransactionFault
                {
                    FaultedRequestIndex = requestIndex,
                    ErrorCode = fault.ErrorCode,
                    Message = fault.Message,
                    ActivityId = fault.ActivityId,
                    Timestamp = fault.Timestamp,
                    TraceText = fault.TraceText,
                    HelpLink = fault.HelpLink,
                    ErrorDetails = fault.ErrorDetails,
                    InnerFault = fault.InnerFault
                }, serviceFault.Reason);
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
