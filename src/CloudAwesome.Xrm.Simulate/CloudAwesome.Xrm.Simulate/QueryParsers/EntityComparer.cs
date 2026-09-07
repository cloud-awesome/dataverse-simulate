using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

public class EntityComparer : IEqualityComparer<Entity>
{
	public bool Equals(Entity? x, Entity? y)
	{
		if (x == null || y == null) return false;

		if (x.Attributes.Count != y.Attributes.Count) return false;

		foreach (var attribute in x.Attributes)
		{
			if (!y.Attributes.ContainsKey(attribute.Key)) return false;

			var xValue = attribute.Value;
			var yValue = y.Attributes[attribute.Key];

			if (!ValuesAreEqual(xValue, yValue)) return false;
		}

		return true;
	}

	public int GetHashCode(Entity obj)
	{
		if (obj == null) return 0;

		int hash = 17;
		foreach (var attribute in obj.Attributes)
		{
			hash = hash * 31 + (attribute.Key.GetHashCode());
			if (attribute.Value != null)
			{
				hash = hash * 31 + GetValueHashCode(attribute.Value);
			}
		}
		return hash;
	}

	private static bool ValuesAreEqual(object? xValue, object? yValue)
	{
		if (xValue == null && yValue == null) return true;
		if (xValue == null || yValue == null) return false;

		if (xValue is AliasedValue xAliasedValue && yValue is AliasedValue yAliasedValue)
		{
			return xAliasedValue.EntityLogicalName == yAliasedValue.EntityLogicalName &&
			       xAliasedValue.AttributeLogicalName == yAliasedValue.AttributeLogicalName &&
			       ValuesAreEqual(xAliasedValue.Value, yAliasedValue.Value);
		}

		return xValue.Equals(yValue);
	}

	private static int GetValueHashCode(object value)
	{
		if (value is not AliasedValue aliasedValue)
		{
			return value.GetHashCode();
		}

		var hash = 17;
		hash = hash * 31 + (aliasedValue.EntityLogicalName?.GetHashCode() ?? 0);
		hash = hash * 31 + (aliasedValue.AttributeLogicalName?.GetHashCode() ?? 0);
		hash = hash * 31 + (aliasedValue.Value == null ? 0 : GetValueHashCode(aliasedValue.Value));

		return hash;
	}
}
