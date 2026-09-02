using CloudAwesome.Xrm.Simulate.DataServices;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

public static class LinkedEntities
{
    public static IQueryable<Entity> Apply(List<LinkEntity>? linkedEntities, List<Entity> records, 
        Dictionary<string, List<Entity>> data, MockedEntityDataService dataService)
    {
        if (linkedEntities == null || !linkedEntities.Any())
        {
            return records.AsQueryable();
        }

        foreach (var linkedEntity in linkedEntities)
        {
            if (!data.ContainsKey(linkedEntity.LinkToEntityName))
            {
                continue;
            }

            var linkedRecords = data[linkedEntity.LinkToEntityName];

            linkedRecords = Filter.Apply(linkedEntity.LinkCriteria, linkedRecords.AsQueryable(), dataService).ToList();
            linkedRecords = Apply(linkedEntity.LinkEntities.ToList(), linkedRecords, data, dataService).ToList();
            records = JoinEntities(records, linkedRecords, linkedEntity).ToList();
        }

        return records.AsQueryable();
    }

    private static object GetJoinKeyValue(object value)
    {
        return value is EntityReference entityReference ? entityReference.Id : value;
    }

    private static IEnumerable<Entity> JoinEntities(IEnumerable<Entity> primaryEntities,
        IEnumerable<Entity> linkedEntities, LinkEntity linkedEntity)
    {
        var result = from primary in primaryEntities
            join linked in linkedEntities
                on GetJoinKeyValue(primary.Attributes[linkedEntity.LinkFromAttributeName]) equals
                GetJoinKeyValue(linked.Attributes[linkedEntity.LinkToAttributeName])
            select MergeEntities(primary, linked, linkedEntity);

        return result;
    }

    private static Entity MergeEntities(Entity primaryEntity, Entity linkedEntity, LinkEntity linkEntity)
    {
        foreach (var attribute in GetProjectedAttributes(linkedEntity, linkEntity.Columns))
        {
            var attributeKey = string.IsNullOrWhiteSpace(linkEntity.EntityAlias)
                ? attribute.Key
                : $"{linkEntity.EntityAlias}.{attribute.Key}";
            primaryEntity.Attributes[attributeKey] = new AliasedValue(
                linkEntity.LinkToEntityName,
                attribute.Key,
                attribute.Value);
        }

        return primaryEntity;
    }

    private static IEnumerable<KeyValuePair<string, object>> GetProjectedAttributes(Entity linkedEntity,
        ColumnSet? columnSet)
    {
        if (columnSet == null || columnSet.AllColumns)
        {
            return linkedEntity.Attributes;
        }

        if (columnSet.Columns.Count == 0)
        {
            return Enumerable.Empty<KeyValuePair<string, object>>();
        }

        return linkedEntity.Attributes
            .Where(attribute => columnSet.Columns.Contains(attribute.Key));
    }

}
