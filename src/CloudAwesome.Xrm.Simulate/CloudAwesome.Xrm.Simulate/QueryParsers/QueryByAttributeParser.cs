using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Metadata;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

public static class QueryByAttributeParser
{
    public static IEnumerable<Entity> Parse(QueryByAttribute query, Dictionary<string,
        List<Entity>> data, MockedEntityDataService dataService)
        => Parse(query, data, dataService, null);

    internal static IEnumerable<Entity> Parse(QueryByAttribute query, Dictionary<string, List<Entity>> data,
        MockedEntityDataService dataService, SimulatedMetadata? metadata)
    {
        if (query.Values == null
            || query.Attributes == null
            || data == null
            || !data.ContainsKey(query.EntityName))
        {
            return Enumerable.Empty<Entity>();
        }

        var records = data[query.EntityName].AsQueryable();

        var queryExpression = new QueryExpression
        {
            EntityName = query.EntityName,
            ColumnSet = query.ColumnSet
        };

        queryExpression.TopCount = query.TopCount;
        queryExpression.Orders.AddRange(query.Orders);
        queryExpression.Criteria = ConstructFilters(query.Attributes, query.Values);

        return QueryExpressionParser.Parse(queryExpression, data, dataService, metadata);
    }

    private static FilterExpression ConstructFilters(DataCollection<string> attributes,
        DataCollection<object> values)
    {
        var filter = new FilterExpression();
        for (int i = 0; i < attributes.Count; i++)
        {
            filter.AddCondition(attributes[i], ConditionOperator.Equal,
                values[i]);
        }

        return filter;
    }
}
