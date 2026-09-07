using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

public static class Columns
{
    public static IQueryable<Entity> Apply(ColumnSet columnSet, IEnumerable<Entity> records, bool includePrimaryId = true)
    {
        return records
            .Select(entity => EntityCloner.Project(entity, columnSet, includePrimaryId))
            .AsQueryable();
    }
        
}
