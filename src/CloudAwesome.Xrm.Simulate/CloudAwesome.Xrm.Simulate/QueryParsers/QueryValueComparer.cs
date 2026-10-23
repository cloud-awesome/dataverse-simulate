using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

internal static class QueryValueComparer
{
    internal static int Compare(object? left, object? right)
    {
        left = GetDataverseValue(left);
        right = GetDataverseValue(right);
        if (left is null) return right is null ? 0 : -1;
        if (right is null) return 1;
        if (left is Guid leftId && right is Guid rightId)
            return new System.Data.SqlTypes.SqlGuid(leftId).CompareTo(new System.Data.SqlTypes.SqlGuid(rightId));
        if (left is string leftText && right is string rightText)
            return StringComparer.OrdinalIgnoreCase.Compare(leftText, rightText);
        if (left.GetType() == right.GetType() && left is IComparable comparable)
            return comparable.CompareTo(right);
        throw new NotSupportedException($"Cannot compare query values of types {left.GetType().Name} and {right.GetType().Name}.");
    }

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
