using System.Xml.Linq;
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
/// <remarks>
/// N.B. Filters and LinkEntity currently only work if you've included the attributes in the ColumnSet.
/// </remarks>
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

        MetadataValidator.ValidateQuery(query, options);
        var data = ApplyReadableFilter(query.EntityName, options);
        var results = QueryExpressionParser.Parse(query, data, dataService);
        var resultCollection = new EntityCollection(results.Take(5000).ToList());

        if (query.PageInfo.ReturnTotalRecordCount)
        {
            resultCollection.TotalRecordCount = results.Take(5000).Count();
            if (results.Count() > 5000)
            {
                resultCollection.TotalRecordCountLimitExceeded = true;
            }
        }
        else
        {
            resultCollection.TotalRecordCount = -1;
        }

        return resultCollection;
    }

    internal EntityCollection RetrieveMultiple(FetchExpression query, ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage);

        var entityName = GetFetchEntityName(query);
        var data = ApplyReadableFilter(entityName, options);
        var results = FetchExpressionParser.Parse(query, data, dataService);

        return new EntityCollection(results.ToList());
    }

    internal EntityCollection RetrieveMultiple(QueryByAttribute query, ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage);

        MetadataValidator.ValidateQuery(query, options);
        var data = ApplyReadableFilter(query.EntityName, options);
        var results = QueryByAttributeParser.Parse(query, data, dataService);

        return new EntityCollection(results.ToList());
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

        if (!data.TryGetValue(entityName, out var records))
            return data;

        data[entityName] = new SimulatedSecurityEnforcer(dataService)
            .FilterReadableRecords(entityName, records, options)
            .ToList();

        return data;
    }

    private static string? GetFetchEntityName(FetchExpression query)
    {
        if (string.IsNullOrWhiteSpace(query.Query))
            return null;

        var document = XDocument.Parse(query.Query);
        return document
            .Descendants("entity")
            .FirstOrDefault()
            ?.Attribute("name")
            ?.Value;
    }
}
