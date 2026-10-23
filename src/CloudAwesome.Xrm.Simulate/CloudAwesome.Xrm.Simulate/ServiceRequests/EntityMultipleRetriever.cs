using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.QueryParsers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

/// <summary>
///
/// </summary>
public class EntityMultipleRetriever(MockedEntityDataService dataService) : IEntityMultipleRetriever
{
    private const string RequestMessage = "RetrieveMultiple";

    public void MockRequest(
        IOrganizationService organizationService,
        ISimulatorOptions? options = null)
    {
        organizationService.RetrieveMultiple(Arg.Is<QueryExpression>(x => x != null))
            .Returns(x =>
            {
                var query = x.Arg<QueryExpression>();
                return this.RetrieveMultiple(query, options);
            });

        organizationService.RetrieveMultiple(Arg.Is<FetchExpression>(x => x != null))
            .Returns(x =>
            {
                var query = x.Arg<FetchExpression>();
                return this.RetrieveMultiple(query, options);
            });

        organizationService.RetrieveMultiple(Arg.Is<QueryByAttribute>(x => x != null))
            .Returns(x =>
            {
                var query = x.Arg<QueryByAttribute>();
                return this.RetrieveMultiple(query, options);
            });
    }

    internal EntityCollection RetrieveMultiple(QueryExpression query, ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage);

        var paging = new QueryPaging(query.PageInfo, query.TopCount);
        MetadataValidator.ValidateQuery(query, options);
        var data = ApplyReadableFilter(query.EntityName, options);
        var results = QueryExpressionParser.Parse(query, data, dataService, options?.Metadata, paging);
        return paging.ToCollection(results);
    }

    internal EntityCollection RetrieveMultiple(FetchExpression query, ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage);

        var queryExpression = FetchExpressionParser.ConvertFetchXmlToQueryExpression(query.Query);
        var paging = new QueryPaging(queryExpression.PageInfo, queryExpression.TopCount);
        MetadataValidator.ValidateQuery(queryExpression, options);
        var data = ApplyReadableFilter(queryExpression.EntityName, options);
        var results = QueryExpressionParser.Parse(queryExpression, data, dataService, options?.Metadata, paging);

        return paging.ToCollection(results);
    }

    internal EntityCollection RetrieveMultiple(QueryByAttribute query, ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage);

        var paging = new QueryPaging(query.PageInfo, query.TopCount);
        MetadataValidator.ValidateQuery(query, options);
        var data = ApplyReadableFilter(query.EntityName, options);
        var results = QueryByAttributeParser.Parse(query, data, dataService, options?.Metadata, paging);

        return paging.ToCollection(results);
    }

    private Dictionary<string, List<Entity>> ApplyReadableFilter(
        string? entityName,
        ISimulatorOptions? options)
    {
        var data = dataService.Get()
            .ToDictionary(
                x => x.Key,
                x => x.Value.ToList(),
                StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(entityName))
            return data;

        var enforcer = new SimulatedSecurityEnforcer(dataService);
        foreach (var table in data.Keys.ToList())
            data[table] = enforcer.FilterReadableRecords(table, data[table], options).ToList();

        return data;
    }

}
