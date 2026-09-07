using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

public static class Order
{
    public static IQueryable<Entity> Apply(IList<OrderExpression> orders, IQueryable<Entity> records)
    {
        // TODO - Need more robust tests for the different paths of this method
        if (orders == null || orders.Count == 0)
        {
            return records;
        }

        IOrderedQueryable<Entity> orderedRecords = null;

        for (var i = 0; i < orders.Count; i++)
        {
            var order = orders[i];

            string orderAttributeOrAlias;
            if (!string.IsNullOrWhiteSpace(order.AttributeName))
            {
                orderAttributeOrAlias = order.AttributeName;
            }
            else if (!string.IsNullOrWhiteSpace(order.Alias))
            {
                orderAttributeOrAlias = order.Alias;
            }
            else
            {
                throw new ArgumentException("Either AttributeName or Alias need to populated", nameof(order.AttributeName));
            }

            if (i == 0)
            {
                orderedRecords = order.OrderType == OrderType.Ascending
                    ? records.OrderBy(entity => GetOrderValue(entity, orderAttributeOrAlias))
                    : records.OrderByDescending(entity => GetOrderValue(entity, orderAttributeOrAlias));
            }
            else
            {
                orderedRecords = order.OrderType == OrderType.Ascending
                    ? orderedRecords.ThenBy(entity => GetOrderValue(entity, orderAttributeOrAlias))
                    : orderedRecords.ThenByDescending(entity => GetOrderValue(entity, orderAttributeOrAlias));
            }
        }
            
        return orderedRecords ?? records;
    }

    private static object GetOrderValue(Entity entity, string attributeName)
    {
        var value = entity.GetAttributeValue<object>(attributeName);

        return value is AliasedValue aliasedValue ? aliasedValue.Value : value;
    }
}
