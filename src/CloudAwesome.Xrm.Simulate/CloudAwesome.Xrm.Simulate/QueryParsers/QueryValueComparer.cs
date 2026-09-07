using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

internal static class QueryValueComparer
{
    public static bool EqualsByDataverseValue(object? left, object? right)
    {
        return Equals(GetDataverseValue(left), GetDataverseValue(right));
    }

    public static object? GetDataverseValue(object? value)
    {
        return value switch
        {
            AliasedValue aliasedValue => GetDataverseValue(aliasedValue.Value),
            EntityReference entityReference => entityReference.Id,
            OptionSetValue optionSetValue => optionSetValue.Value,
            Money money => money.Value,
            _ => value
        };
    }
}
