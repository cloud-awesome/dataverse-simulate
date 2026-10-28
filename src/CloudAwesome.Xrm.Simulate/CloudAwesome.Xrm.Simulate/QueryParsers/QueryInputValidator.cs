using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

/// <summary>Reject query flags whose semantics the executor does not implement, even on empty data.</summary>
internal static class QueryInputValidator
{
	internal static void Validate(QueryExpression query)
	{
		ValidateColumns(query.ColumnSet);
		ValidateFilter(query.Criteria);
		ValidateOrders(query.Orders);
		foreach (var link in query.LinkEntities) ValidateLink(link);
	}

	internal static void Validate(QueryByAttribute query)
	{
		ValidateColumns(query.ColumnSet);
		ValidateOrders(query.Orders);
		foreach (var attribute in query.Attributes) RejectQualifiedName(attribute);
	}

	private static void ValidateLink(LinkEntity link)
	{
		if (link.JoinOperator is not (JoinOperator.Inner or JoinOperator.LeftOuter))
			throw new NotSupportedException(
				$"JoinOperator.{link.JoinOperator} is not supported by the simulator for QueryExpression LinkEntity joins.");
		ValidateColumns(link.Columns);
		ValidateFilter(link.LinkCriteria);
		ValidateOrders(link.Orders);
		foreach (var child in link.LinkEntities) ValidateLink(child);
	}

	private static void ValidateFilter(FilterExpression? filter)
	{
		if (filter is null) return;
		if (filter.AnyAllFilterLinkEntity is not null)
			throw new NotSupportedException(
				"FilterExpression.AnyAllFilterLinkEntity is not supported by the simulator.");
		foreach (var condition in filter.Conditions)
		{
			if (!string.IsNullOrEmpty(condition.EntityName))
				throw new NotSupportedException(
					"ConditionExpression.EntityName qualified conditions are not supported by the simulator. Use unqualified LinkCriteria for supported join filters.");
			if (condition.CompareColumns)
				throw new NotSupportedException(
					"ConditionExpression.CompareColumns column comparisons are not supported by the simulator.");
			RejectQualifiedName(condition.AttributeName);
		}

		foreach (var child in filter.Filters) ValidateFilter(child);
	}

	private static void ValidateOrders(IEnumerable<OrderExpression> orders)
	{
		foreach (var order in orders)
		{
			if (!string.IsNullOrEmpty(order.EntityName))
				throw new NotSupportedException(
					"OrderExpression.EntityName qualified ordering is not supported by the simulator. Use unqualified LinkEntity.Orders for supported linked ordering.");
			RejectQualifiedName(order.AttributeName);
			RejectQualifiedName(order.Alias);
		}
	}

	private static void ValidateColumns(ColumnSet? columns)
	{
		if (columns is null) return;
		foreach (var column in columns.Columns) RejectQualifiedName(column);
		foreach (var expression in columns.AttributeExpressions) RejectQualifiedName(expression.AttributeName);
	}

	private static void RejectQualifiedName(string? name)
	{
		if (name?.Contains('.', StringComparison.Ordinal) == true)
			throw new NotSupportedException(
				$"Qualified attribute name '{name}' is not supported by the simulator. Use unqualified attributes within their table's query or LinkEntity.");
	}
}