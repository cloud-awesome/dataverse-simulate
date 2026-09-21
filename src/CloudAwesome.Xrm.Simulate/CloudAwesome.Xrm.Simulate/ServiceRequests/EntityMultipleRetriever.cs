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
    private const int DefaultPageSize = 5000;
    private const int TotalRecordCountLimit = 5000;

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
        return ToEntityCollection(results, query.PageInfo);
    }

    internal EntityCollection RetrieveMultiple(FetchExpression query, ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage);

        if (string.IsNullOrWhiteSpace(query.Query))
        {
            return ToEntityCollection(Enumerable.Empty<Entity>(), null);
        }

        var queryExpression = FetchExpressionParser.ConvertFetchXmlToQueryExpression(query.Query);
        var data = ApplyReadableFilter(queryExpression.EntityName, options);
        var results = QueryExpressionParser.Parse(queryExpression, data, dataService);

        return ToEntityCollection(results, queryExpression.PageInfo);
    }

    internal EntityCollection RetrieveMultiple(QueryByAttribute query, ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage);

        MetadataValidator.ValidateQuery(query, options);
        var data = ApplyReadableFilter(query.EntityName, options);
        var results = QueryByAttributeParser.Parse(query, data, dataService);

        return ToEntityCollection(results, query.PageInfo);
    }

    private static EntityCollection ToEntityCollection(IEnumerable<Entity> results, PagingInfo? pageInfo)
    {
        var materialized = results.ToList();
        var pageNumber = pageInfo?.PageNumber > 0 ? pageInfo.PageNumber : 1;
        var pageSize = pageInfo?.Count > 0 ? pageInfo.Count : DefaultPageSize;
        var skip = (pageNumber - 1) * pageSize;
        var page = materialized
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        var collection = new EntityCollection(page)
        {
            MoreRecords = materialized.Count > skip + pageSize,
            TotalRecordCount = -1
        };

        if (collection.MoreRecords && page.Count > 0)
        {
            collection.PagingCookie = BuildPagingCookie(pageNumber, page);
        }

        if (pageInfo?.ReturnTotalRecordCount == true)
        {
            collection.TotalRecordCount = Math.Min(materialized.Count, TotalRecordCountLimit);
            collection.TotalRecordCountLimitExceeded = materialized.Count > TotalRecordCountLimit;
        }

        return collection;
    }

    private static string BuildPagingCookie(int pageNumber, IReadOnlyList<Entity> page)
    {
        var first = page[0].Id.ToString("D");
        var last = page[^1].Id.ToString("D");

        return $"<cookie page=\"{pageNumber}\" first=\"{first}\" last=\"{last}\" />";
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

}
