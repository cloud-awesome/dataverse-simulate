using CloudAwesome.Xrm.Simulate.DataServices;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

public static class LinkedEntities
{
    public static IQueryable<Entity> Apply(List<LinkEntity>? linkedEntities, List<Entity> records, 
        Dictionary<string, List<Entity>> data, MockedEntityDataService dataService)
    {
        var ordering = new LinkedOrdering(linkedEntities ?? []);
        var joined = ApplyLinks(linkedEntities, records, data, dataService, ordering);
        // Sort using execution-only keys. Remove them before projection/distinct so they cannot
        // leak into output or change distinct identity; top and paging consume this ordered sequence.
        var ordered = Order.Apply(ordering.Orders, joined).ToList();
        foreach (var row in ordered)
            foreach (var key in ordering.HiddenKeys)
                row.Attributes.Remove(key);
        return ordered.AsQueryable();
    }

    private static IQueryable<Entity> ApplyLinks(List<LinkEntity>? linkedEntities, List<Entity> records,
        Dictionary<string, List<Entity>> data, MockedEntityDataService dataService, LinkedOrdering ordering)
    {
        if (linkedEntities == null || !linkedEntities.Any())
        {
            return records.AsQueryable();
        }

        foreach (var linkedEntity in linkedEntities)
        {
            ThrowIfUnsupportedJoinOperator(linkedEntity);

            if (!data.ContainsKey(linkedEntity.LinkToEntityName))
            {
                records = ApplyMissingLinkedEntity(records, linkedEntity).ToList();
                continue;
            }

            var linkedRecords = data[linkedEntity.LinkToEntityName]
                .Select(EntityCloner.Clone)
                .ToList();

            linkedRecords = Filter.Apply(linkedEntity.LinkCriteria, linkedRecords.AsQueryable(), dataService).ToList();
            linkedRecords = ApplyLinks(linkedEntity.LinkEntities.ToList(), linkedRecords, data, dataService, ordering).ToList();
            records = JoinEntities(records, linkedRecords, linkedEntity, ordering).ToList();
        }

        return records.AsQueryable();
    }

    private static void ThrowIfUnsupportedJoinOperator(LinkEntity linkedEntity)
    {
        if (linkedEntity.JoinOperator is JoinOperator.Inner or JoinOperator.LeftOuter)
        {
            return;
        }

        throw new NotSupportedException(
            $"JoinOperator.{linkedEntity.JoinOperator} is not supported by the simulator for QueryExpression LinkEntity joins.");
    }

    private static IEnumerable<Entity> ApplyMissingLinkedEntity(IEnumerable<Entity> primaryEntities, LinkEntity linkedEntity)
    {
        return linkedEntity.JoinOperator == JoinOperator.LeftOuter
            ? primaryEntities.Select(EntityCloner.Clone)
            : Enumerable.Empty<Entity>();
    }

    private static object? GetJoinKeyValue(object value)
    {
        return QueryValueComparer.GetDataverseValue(value);
    }

    private static IEnumerable<Entity> JoinEntities(IEnumerable<Entity> primaryEntities,
        IEnumerable<Entity> linkedEntities, LinkEntity linkedEntity, LinkedOrdering ordering)
    {
        var linkedEntityList = linkedEntities.ToList();

        foreach (var primary in primaryEntities)
        {
            var matches = linkedEntityList
                .Where(linked => JoinKeysMatch(primary, linked, linkedEntity))
                .ToList();

            if (matches.Count == 0)
            {
                if (linkedEntity.JoinOperator == JoinOperator.LeftOuter)
                {
                    yield return EntityCloner.Clone(primary);
                }

                continue;
            }

            foreach (var linked in matches)
            {
                yield return MergeEntities(primary, linked, linkedEntity, ordering);
            }
        }
    }

    private static bool JoinKeysMatch(Entity primaryEntity, Entity linkedEntity, LinkEntity linkEntity)
    {
        if (!primaryEntity.Attributes.TryGetValue(linkEntity.LinkFromAttributeName, out var primaryKey) ||
            !linkedEntity.Attributes.TryGetValue(linkEntity.LinkToAttributeName, out var linkedKey))
        {
            return false;
        }

        return Equals(GetJoinKeyValue(primaryKey), GetJoinKeyValue(linkedKey));
    }

    private static Entity MergeEntities(Entity primaryEntity, Entity linkedEntity, LinkEntity linkEntity,
        LinkedOrdering ordering)
    {
        var result = EntityCloner.Clone(primaryEntity);

        // Carry nested execution keys through the parent join without aliasing them.
        foreach (var key in ordering.HiddenKeys)
            if (linkedEntity.Attributes.TryGetValue(key, out var value)) result[key] = value;
        foreach (var (attribute, key) in ordering.Keys[linkEntity])
            if (linkedEntity.Attributes.TryGetValue(attribute, out var value)) result[key] = value;

        foreach (var attribute in GetProjectedAttributes(linkedEntity, linkEntity.Columns)
                     .Where(attribute => !ordering.HiddenKeys.Contains(attribute.Key)))
        {
            var attributeKey = string.IsNullOrWhiteSpace(linkEntity.EntityAlias)
                ? attribute.Key
                : $"{linkEntity.EntityAlias}.{attribute.Key}";
            result.Attributes[attributeKey] = new AliasedValue(
                linkEntity.LinkToEntityName,
                attribute.Key,
                attribute.Value);
        }

        return result;
    }

    private sealed class LinkedOrdering
    {
        internal readonly Dictionary<LinkEntity, List<(string Attribute, string Key)>> Keys = new();
        internal readonly HashSet<string> HiddenKeys = new(StringComparer.Ordinal);
        internal readonly List<OrderExpression> Orders = [];

        internal LinkedOrdering(IEnumerable<LinkEntity> links)
        {
            foreach (var link in links) Add(link);
        }

        private void Add(LinkEntity link)
        {
            var keys = new List<(string, string)>();
            Keys.Add(link, keys);
            foreach (var order in link.Orders)
            {
                var attribute = !string.IsNullOrWhiteSpace(order.AttributeName) ? order.AttributeName : order.Alias;
                var key = $"__simulator_link_order_{Guid.NewGuid():N}";
                keys.Add((attribute, key));
                HiddenKeys.Add(key);
                Orders.Add(new OrderExpression(key, order.OrderType));
            }
            foreach (var child in link.LinkEntities) Add(child);
        }
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
