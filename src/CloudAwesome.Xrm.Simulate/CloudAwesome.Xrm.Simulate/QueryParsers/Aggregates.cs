using CloudAwesome.Xrm.Simulate.DataStores;
using Microsoft.Xrm.Sdk;
using CloudAwesome.Xrm.Simulate.Metadata;
using Microsoft.Xrm.Sdk.Query;
using CloudAwesome.Xrm.Simulate.ServiceRequests;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

public static class Aggregates
{
    public const int DefaultAggregateRecordLimit = 50000;

    public static IQueryable<Entity> Apply(ColumnSet columnSet, IEnumerable<Entity> records, string entityName,
        FiscalYearSettings fiscalYearSettings)
        => Apply(columnSet, records, entityName, fiscalYearSettings, null, null);

    internal static IQueryable<Entity> Apply(ColumnSet columnSet, IEnumerable<Entity> records, string entityName,
        FiscalYearSettings fiscalYearSettings, SimulatedEntityMetadata? metadata, IEnumerable<Entity>? sourceRecords)
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

        var input = ApplyAggregateLimit(columnSet, records).ToList();
        var source = sourceRecords?.ToList() ?? input;
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var expression in columnSet.AttributeExpressions)
        {
            if (!aliases.Add(GetAliasOrAttributeName(expression)))
                throw new ArgumentException("Aggregate and group aliases must be unique.");
        }

        var groupByExpressions = columnSet.AttributeExpressions
            .Where(e => e.HasGroupBy)
            .ToList();

        var aggregateExpressions = columnSet.AttributeExpressions
            .Where(e => !e.HasGroupBy && e.AggregateType != XrmAggregateType.None)
            .ToList();

        // If no GROUP BY, we aggregate across the whole input as a single group.
        IEnumerable<List<Entity>> grouped = groupByExpressions.Count == 0
            ? new[] { input }
            : input.GroupBy(
                r => new GroupKey(groupByExpressions
                    .Select(g => NormalizeGroupByValue(
                        GetAttributeValue(r, g.AttributeName), g, fiscalYearSettings))
                    .ToArray()),
                GroupKeyComparer.Instance).Select(group => group.ToList());

        var output = new List<Entity>();

        foreach (var group in grouped)
        {
            var row = new Entity(entityName);

            // Emit group-by columns using their alias if provided
            for (int i = 0; i < groupByExpressions.Count; i++)
            {
                var expr = groupByExpressions[i];
                var alias = GetAliasOrAttributeName(expr);
                var value = NormalizeGroupByValue(GetAttributeValue(group[0], expr.AttributeName), expr, fiscalYearSettings);
                if (value is not null)
                    row[alias] = new AliasedValue(entityName, expr.AttributeName, value);
            }

            // Emit aggregate columns
            foreach (var expr in aggregateExpressions)
            {
                var alias = GetAliasOrAttributeName(expr);
                var value = ComputeAggregateValue(expr, group, InferAttributeType(expr.AttributeName, metadata, source));
                row[alias] = new AliasedValue(entityName, expr.AttributeName, value);
            }

            output.Add(row);
        }

        return output.AsQueryable();
    }

    private static Type InferAttributeType(string attribute, SimulatedEntityMetadata? metadata, IEnumerable<Entity> source)
    {
        if (metadata?.TryGetAttribute(attribute, out var attributeMetadata) == true)
        {
            var type = attributeMetadata.AttributeType.ToLowerInvariant() switch
            {
                "integer" => typeof(int), "bigint" => typeof(long), "decimal" => typeof(decimal),
                "double" => typeof(double), "money" => typeof(Money), _ => null
            };
            if (type is not null) return type;
        }
        return source.Select(row => GetAttributeValue(row, attribute)).FirstOrDefault(value => value is not null)?.GetType() ?? typeof(int);
    }

    private static object? ComputeAggregateValue(XrmAttributeExpression expression, IReadOnlyList<Entity> group, Type type)
    {
        if (expression.AggregateType == XrmAggregateType.Count) return group.Count;
        var values = group.Select(row => GetAttributeValue(row, expression.AttributeName)).OfType<object>().ToList();
        if (expression.AggregateType == XrmAggregateType.CountColumn)
            return FetchXmlAggregateOptions.IsDistinct(expression) ? values.Distinct(AggregateValueComparer.Instance).Count() : values.Count;

        if (values.Count == 0 && type != typeof(int) && type != typeof(long))
            return null;

        if (expression.AggregateType is XrmAggregateType.Min or XrmAggregateType.Max && values.Count > 0)
            return values.Aggregate((best, value) =>
                (Compare(best, value) > 0) == (expression.AggregateType == XrmAggregateType.Min) ? value : best);

        if (type == typeof(double))
        {
            var numbers = values.Select(value => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            return numbers.Count == 0 ? 0d : expression.AggregateType == XrmAggregateType.Avg ? numbers.Average() : numbers.Sum();
        }
        if (type != typeof(int) && type != typeof(long) && type != typeof(decimal) && type != typeof(Money))
            return values.Count == 0 ? null : throw new NotSupportedException($"Aggregate {expression.AggregateType} requires a numeric attribute.");

        var total = values.Sum(value => value is Money money ? money.Value : Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture));
        var result = expression.AggregateType == XrmAggregateType.Avg && values.Count > 0 ? total / values.Count : total;
        if (type == typeof(Money)) return new Money(result);
        if (type == typeof(int)) return checked((int)decimal.Truncate(result));
        if (type == typeof(long)) return checked((long)decimal.Truncate(result));
        return result;
    }

    private static object? GetAttributeValue(Entity entity, string attribute)
        => entity.GetAttributeValue<object>(attribute);

    private static IEnumerable<Entity> ApplyAggregateLimit(ColumnSet columnSet, IEnumerable<Entity> records)
    {
        var explicitLimit = columnSet.AttributeExpressions
            .Select(FetchXmlAggregateOptions.GetAggregateLimit)
            .FirstOrDefault(limit => limit.HasValue);

        if (explicitLimit.HasValue)
        {
            if (explicitLimit.Value <= 0 || explicitLimit.Value > DefaultAggregateRecordLimit)
                throw DataverseServiceFaults.InvalidAggregateLimit();
            return records.Take(explicitLimit.Value + 1).ToList();
        }

        var materialized = records.Take(DefaultAggregateRecordLimit + 1).ToList();
        if (materialized.Count > DefaultAggregateRecordLimit)
        {
            throw DataverseServiceFaults.AggregateQueryRecordLimitExceeded();
        }

        return materialized;
    }

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

    private static string GetAliasOrAttributeName(XrmAttributeExpression expr)
        => string.IsNullOrWhiteSpace(expr.Alias) ? expr.AttributeName : expr.Alias;

    // --- Grouping support ---

    private sealed class GroupKey
    {
        public static readonly GroupKey Empty = new GroupKey(Array.Empty<object>());
        public GroupKey(object?[] values) { Values = values; }
        public object?[] Values { get; }
    }

    private sealed class GroupKeyComparer : IEqualityComparer<GroupKey>
    {
        public static readonly GroupKeyComparer Instance = new GroupKeyComparer();

        public bool Equals(GroupKey? x, GroupKey? y)
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

    private sealed class AggregateValueComparer : IEqualityComparer<object>
    {
        public static readonly AggregateValueComparer Instance = new AggregateValueComparer();

        public new bool Equals(object? x, object? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;

            return Normalize(x).Equals(Normalize(y));
        }

        public int GetHashCode(object obj)
        {
            return Normalize(obj).GetHashCode();
        }

        private static object Normalize(object value)
        {
            if (value is Microsoft.Xrm.Sdk.Money money) return money.Value;
            if (value is OptionSetValue optionSetValue) return optionSetValue.Value;
            if (value is EntityReference entityReference) return entityReference.Id;
            if (value is AliasedValue aliasedValue) return Normalize(aliasedValue.Value);

            return value;
        }
    }
    
    private static object? NormalizeGroupByValue(object? value, XrmAttributeExpression expr, FiscalYearSettings options)
    {
        if (value == null) return null;

        // Accept DateTimeOffset too (Dataverse sometimes)
        if (value is DateTimeOffset dto) value = dto.UtcDateTime;

        if (value is DateTime dt)
        {
            // You can choose to treat 'Unspecified' as UTC/local; for tests UTC is fine.
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            switch (expr.DateTimeGrouping)
            {
                case XrmDateTimeGrouping.Day:
                    // Day of month 1..31 (common for reporting; pairs with Year/Month)
                    return dt.Day;

                case XrmDateTimeGrouping.Week:
                    // ISO-8601 week number (1..53)
                    return System.Globalization.ISOWeek.GetWeekOfYear(dt);
                
                case XrmDateTimeGrouping.Month:
                    // 1..12
                    return dt.Month;

                case XrmDateTimeGrouping.Quarter:
                    return ((dt.Month - 1) / 3) + 1; // 1..4

                case XrmDateTimeGrouping.Year:
                    return dt.Year;

                case XrmDateTimeGrouping.FiscalPeriod:
                    return GetFiscalPeriod(dt, options.FiscalYearStartMonth);

                case XrmDateTimeGrouping.FiscalYear:
                    return GetFiscalYearLabel(dt, options.FiscalYearStartMonth, 
                        options.FiscalYearNameBasedOnStartDate, options.FiscalYearPrefix);

                case XrmDateTimeGrouping.None:
                default:
                    // If grouping is requested but no specific DateTimeGrouping, use date-only
                    return dt.Date;
            }
        }

        // Non-date group-by: return as-is
        return value;
    }

    private static int GetFiscalPeriod(DateTime dt, int startMonth)
    {
        // startMonth: 1..12
        int zeroBased = ((dt.Month - startMonth) + 12) % 12; // 0..11
        return zeroBased + 1; // 1..12
    }

    private static string GetFiscalYearLabel(DateTime dt, int startMonth, bool namedByStartYear,
        string prefix)
    {
        // If fiscal year starts in, say, April (4), then:
        // - Jan–Mar 2025 belong to FY that STARTED in 2024 and ENDS in 2025.
        int startYear = dt.Month >= startMonth ? dt.Year : dt.Year - 1;
        var tester = namedByStartYear ? startYear : (startYear + 1);
        return $"{prefix}{tester}";
    }

}
