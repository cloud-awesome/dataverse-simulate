using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

public class RetrieveMultipleHandler : IRequestHandler
{
    public OrganizationResponse Handle(
        OrganizationRequest request,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options = null)
    {
        var retrieveMultipleRequest = (RetrieveMultipleRequest)request;
        var retriever = new EntityMultipleRetriever(dataService);
        var entityCollection = retrieveMultipleRequest.Query switch
        {
            QueryExpression query => retriever.RetrieveMultiple(query, options),
            FetchExpression query => retriever.RetrieveMultiple(query, options),
            QueryByAttribute query => retriever.RetrieveMultiple(query, options),
            _ => throw new NotSupportedException(
                $"RetrieveMultiple query type '{retrieveMultipleRequest.Query.GetType().Name}' is not supported by CloudAwesome.Xrm.Simulate.")
        };

        return new RetrieveMultipleResponse
        {
            Results = new ParameterCollection
            {
                ["EntityCollection"] = entityCollection
            },
            ResponseName = "RetrieveMultiple"
        };
    }
}
