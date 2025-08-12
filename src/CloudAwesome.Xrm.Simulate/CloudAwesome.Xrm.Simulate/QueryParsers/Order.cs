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
                    ? records.OrderBy(entity => entity.GetAttributeValue<object>(orderAttributeOrAlias))
                    : records.OrderByDescending(entity => entity.GetAttributeValue<object>(orderAttributeOrAlias));
            }
            else
            {
                orderedRecords = order.OrderType == OrderType.Ascending
                    ? orderedRecords.ThenBy(entity => entity.GetAttributeValue<object>(orderAttributeOrAlias))
                    : orderedRecords.ThenByDescending(entity => entity.GetAttributeValue<object>(orderAttributeOrAlias));
            }
        }
            
        return orderedRecords ?? records;
    }
}