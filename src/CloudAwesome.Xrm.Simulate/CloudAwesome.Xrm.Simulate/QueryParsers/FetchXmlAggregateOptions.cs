using System.Runtime.CompilerServices;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

internal sealed class FetchXmlAggregateOptions
{
    private static readonly ConditionalWeakTable<XrmAttributeExpression, FetchXmlAggregateOptions> OptionsByExpression = new();

    private FetchXmlAggregateOptions(bool distinct, int? aggregateLimit)
    {
        Distinct = distinct;
        AggregateLimit = aggregateLimit;
    }

    public bool Distinct { get; }
    public int? AggregateLimit { get; }

    public static XrmAttributeExpression Attach(
        XrmAttributeExpression expression,
        bool distinct,
        int? aggregateLimit)
    {
        OptionsByExpression.Remove(expression);
        OptionsByExpression.Add(expression, new FetchXmlAggregateOptions(distinct, aggregateLimit));

        return expression;
    }

    public static bool IsDistinct(XrmAttributeExpression expression)
    {
        return OptionsByExpression.TryGetValue(expression, out var options) && options.Distinct;
    }

    public static int? GetAggregateLimit(XrmAttributeExpression expression)
    {
        return OptionsByExpression.TryGetValue(expression, out var options)
            ? options.AggregateLimit
            : null;
    }
}
