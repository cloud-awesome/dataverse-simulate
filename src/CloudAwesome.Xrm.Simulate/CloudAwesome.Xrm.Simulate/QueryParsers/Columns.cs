using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

public static class Columns
{
    public static IQueryable<Entity> Apply(ColumnSet columnSet, IEnumerable<Entity> records)
    {
        return records
            .Select(entity => EntityCloner.Project(entity, columnSet))
            .AsQueryable();
    }
        
}
