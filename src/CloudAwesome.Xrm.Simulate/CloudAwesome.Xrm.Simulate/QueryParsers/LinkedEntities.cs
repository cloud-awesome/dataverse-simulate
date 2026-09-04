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
            linkedRecords = Apply(linkedEntity.LinkEntities.ToList(), linkedRecords, data, dataService).ToList();
            records = JoinEntities(records, linkedRecords, linkedEntity).ToList();
            records = ApplyLinkedOrders(linkedEntity, records).ToList();
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
        IEnumerable<Entity> linkedEntities, LinkEntity linkedEntity)
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
                yield return MergeEntities(primary, linked, linkedEntity);
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

    private static Entity MergeEntities(Entity primaryEntity, Entity linkedEntity, LinkEntity linkEntity)
    {
        var result = EntityCloner.Clone(primaryEntity);

        foreach (var attribute in GetProjectedAttributes(linkedEntity, linkEntity.Columns))
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

    private static IQueryable<Entity> ApplyLinkedOrders(LinkEntity linkedEntity, IEnumerable<Entity> records)
    {
        if (linkedEntity.Orders == null || linkedEntity.Orders.Count == 0)
        {
            return records.AsQueryable();
        }

        var projectedOrders = linkedEntity.Orders
            .Select(order => new OrderExpression(GetProjectedOrderAttributeName(linkedEntity, order), order.OrderType))
            .ToList();

        return Order.Apply(projectedOrders, records.AsQueryable());
    }

    private static string GetProjectedOrderAttributeName(LinkEntity linkedEntity, OrderExpression order)
    {
        var attributeName = !string.IsNullOrWhiteSpace(order.AttributeName)
            ? order.AttributeName
            : order.Alias;

        if (string.IsNullOrWhiteSpace(linkedEntity.EntityAlias) ||
            attributeName.Contains(".", StringComparison.Ordinal))
        {
            return attributeName;
        }

        return $"{linkedEntity.EntityAlias}.{attributeName}";
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
