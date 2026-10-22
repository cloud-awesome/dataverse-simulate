using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Metadata;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

/// <summary>
/// 
/// </summary>
public static class QueryExpressionParser
{
    public static IEnumerable<Entity> Parse(QueryExpression query, Dictionary<string,
        List<Entity>> data, MockedEntityDataService dataService)
        => Parse(query, data, dataService, null);

    internal static IEnumerable<Entity> Parse(QueryExpression query, Dictionary<string, List<Entity>> data,
        MockedEntityDataService dataService, SimulatedMetadata? metadata)
    {
        if (query == null || data == null || !data.ContainsKey(query.EntityName))
        {
            return Enumerable.Empty<Entity>();
        }

        var primaryIdAttribute = metadata?.GetEntity(query.EntityName).PrimaryIdAttribute;
        var records = data[query.EntityName]
            .Select(EntityCloner.Clone)
            .ToList()
            .AsQueryable();

        records = Filter.Apply(query.Criteria, records, dataService);
        records = LinkedEntities.Apply(query.LinkEntities.ToList(), records.ToList(), data, dataService);
        records = Aggregates.Apply(query.ColumnSet, records, query.EntityName, dataService.FiscalYearSettings);
        records = Order.Apply(query.Orders, records);
        if (query.Distinct)
        {
            // An empty base ColumnSet defaults to its primary id, even with Distinct or selected linked columns.
            var columns = query.ColumnSet ?? new ColumnSet(false);
            var includePrimaryId = columns.Columns.Count == 0 && columns.AttributeExpressions.Count == 0;
            records = Columns.Apply(columns, records, includePrimaryId, primaryIdAttribute);
        }

        records = Distinct.Apply(query.Distinct, records);
        records = TopCount.Apply(query.TopCount, records.ToList());
        if (!query.Distinct)
        {
            records = Columns.Apply(query.ColumnSet, records, true, primaryIdAttribute);
        }

        return records;
    }
}
