using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

public static class Aggregates
{
    public static IQueryable<Entity> Apply(ColumnSet columnSet, IEnumerable<Entity> records, string entityName)
    {
        // If there are no aggregate expressions, don't touch the pipeline.
        if (columnSet == null ||
            columnSet.AllColumns ||
            columnSet.AttributeExpressions == null ||
            columnSet.AttributeExpressions.Count == 0 ||
            !columnSet.AttributeExpressions.Any(ae => ae.HasGroupBy || ae.AggregateType != XrmAggregateType.None))
        {
            return records.AsQueryable();
        }

        var groupByExpressions = columnSet.AttributeExpressions
            .Where(e => e.HasGroupBy)
            .ToList();

        var aggregateExpressions = columnSet.AttributeExpressions
            .Where(e => !e.HasGroupBy && e.AggregateType != XrmAggregateType.None)
            .ToList();

        // If no GROUP BY, we aggregate across the whole input as a single group.
        var grouped = groupByExpressions.Count == 0
            ? records.GroupBy(_ => GroupKey.Empty, GroupKeyComparer.Instance)
            : records.GroupBy(
                r => new GroupKey(groupByExpressions.Select(g => GetAttributeValue(r, g.AttributeName)).ToArray()),
                GroupKeyComparer.Instance);

        var output = new List<Entity>();

        foreach (var group in grouped)
        {
            var row = new Entity(entityName) { Id = Guid.NewGuid() };

            // Emit group-by columns using their alias if provided
            for (int i = 0; i < groupByExpressions.Count; i++)
            {
                var expr = groupByExpressions[i];
                var alias = SafeAlias(expr);
                row[alias] = group.Key.Values[i];
            }

            // Emit aggregate columns
            foreach (var expr in aggregateExpressions)
            {
                var alias = SafeAlias(expr);
                object value = ComputeAggregate(expr, group);
                row[alias] = value;
            }

            output.Add(row);
        }

        return output.AsQueryable();
    }

    private static object ComputeAggregate(XrmAttributeExpression expr, IGrouping<GroupKey, Entity> group)
    {
        switch (expr.AggregateType)
        {
            case XrmAggregateType.Sum:
                return Sum(group, expr.AttributeName);

            case XrmAggregateType.Avg:
                return Avg(group, expr.AttributeName);

            case XrmAggregateType.Min:
                return Min(group, expr.AttributeName);

            case XrmAggregateType.Max:
                return Max(group, expr.AttributeName);

            case XrmAggregateType.Count:
                // COUNT(*) semantics per Dataverse QueryExpression
                return group.Count();

            case XrmAggregateType.CountColumn:
                // Count of non-null values for that column
                return group.Count(e => GetAttributeValue(e, expr.AttributeName) != null);

            case XrmAggregateType.None:
            default:
                return null;
        }
    }

    private static object Sum(IEnumerable<Entity> group, string attribute)
    {
        decimal sum = 0m;
        bool any = false;
        foreach (var v in Values(group, attribute))
        {
            if (TryToDecimal(v, out var d))
            {
                sum += d;
                any = true;
            }
        }
        return any ? (object)sum : null;
    }

    private static object Avg(IEnumerable<Entity> group, string attribute)
    {
        decimal sum = 0m;
        int count = 0;
        foreach (var v in Values(group, attribute))
        {
            if (TryToDecimal(v, out var d))
            {
                sum += d;
                count++;
            }
        }
        return count > 0 ? (object)(sum / count) : null;
    }

    private static object Min(IEnumerable<Entity> group, string attribute)
    {
        object best = null;
        foreach (var v in Values(group, attribute))
        {
            if (v == null) continue;
            if (best == null) { best = v; continue; }

            if (Compare(best, v) > 0) best = v;
        }
        return best;
    }

    private static object Max(IEnumerable<Entity> group, string attribute)
    {
        object best = null;
        foreach (var v in Values(group, attribute))
        {
            if (v == null) continue;
            if (best == null) { best = v; continue; }

            if (Compare(best, v) < 0) best = v;
        }
        return best;
    }

    private static IEnumerable<object> Values(IEnumerable<Entity> group, string attribute)
        => group.Select(e => GetAttributeValue(e, attribute));

    private static object GetAttributeValue(Entity e, string attribute)
        => e.Attributes != null && e.Attributes.TryGetValue(attribute, out var v) ? v : null;

    private static bool TryToDecimal(object value, out decimal result)
    {
        result = 0m;
        if (value == null) return false;

        // Handle common Dataverse numeric shapes
        switch (value)
        {
            case decimal d: result = d; return true;
            case int i:     result = i; return true;
            case long l:    result = l; return true;
            case double db: result = (decimal)db; return true;
            case float f:   result = (decimal)f; return true;
            // Money wraps decimal .Value in Dataverse
            case Microsoft.Xrm.Sdk.Money m: result = m.Value; return true;
            // fallthrough for strings if you want to be lenient
            case string s when decimal.TryParse(s, out var parsed): result = parsed; return true;
            default: return false;
        }
    }

    // Returns -1, 0, 1 like IComparer
    private static int Compare(object a, object b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        // Normalize Money to underlying decimal for comparability
        if (a is Microsoft.Xrm.Sdk.Money ma) a = ma.Value;
        if (b is Microsoft.Xrm.Sdk.Money mb) b = mb.Value;

        // If both implement IComparable and are the same type, use it
        if (a.GetType() == b.GetType() && a is IComparable cmpSame)
            return cmpSame.CompareTo(b);

        // Try decimal compare if both convertible
        if (TryToDecimal(a, out var da) && TryToDecimal(b, out var db))
            return da.CompareTo(db);

        // Fallback to string compare to keep things deterministic in tests
        return string.CompareOrdinal(a.ToString(), b.ToString());
    }

    private static string SafeAlias(XrmAttributeExpression expr)
        => string.IsNullOrWhiteSpace(expr.Alias) ? expr.AttributeName : expr.Alias;

    // --- Grouping support ---

    private sealed class GroupKey
    {
        public static readonly GroupKey Empty = new GroupKey(Array.Empty<object>());
        public GroupKey(object[] values) { Values = values; }
        public object[] Values { get; }
    }

    private sealed class GroupKeyComparer : IEqualityComparer<GroupKey>
    {
        public static readonly GroupKeyComparer Instance = new GroupKeyComparer();

        public bool Equals(GroupKey x, GroupKey y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x.Values.Length != y.Values.Length) return false;

            for (int i = 0; i < x.Values.Length; i++)
            {
                var a = x.Values[i];
                var b = y.Values[i];
                if (a == null && b == null) continue;
                if (a == null || b == null) return false;

                // Normalize Money as decimal for equality
                if (a is Microsoft.Xrm.Sdk.Money ma) a = ma.Value;
                if (b is Microsoft.Xrm.Sdk.Money mb) b = mb.Value;

                if (!a.Equals(b)) return false;
            }
            return true;
        }

        public int GetHashCode(GroupKey obj)
        {
            unchecked
            {
                int h = 17;
                foreach (var v in obj.Values)
                {
                    var val = v is Microsoft.Xrm.Sdk.Money m ? (object)m.Value : v;
                    h = h * 31 + (val?.GetHashCode() ?? 0);
                }
                return h;
            }
        }
    }
}
