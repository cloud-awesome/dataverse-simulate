using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture]
public class QueryExpressionAggregateTests
{
	private IOrganizationService _organizationService = null!;
	
	[SetUp]
	public void SetUp()
	{
		_organizationService = _organizationService.Simulate();
	}

	[Test]
	public void Sum_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child

		var query = new QueryExpression
		{
			EntityName = Arthur.Contact().LogicalName,
			ColumnSet = new ColumnSet(false)
			{
				AttributeExpressions =
				{
					new XrmAttributeExpression(
						attributeName: Contact.Fields.NumberOfChildren,
						alias: "SumOfChildren",
						aggregateType: XrmAggregateType.Sum)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["SumOfChildren"].Should().Be(3);
	}
	
	[Test]
	public void Average_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child

		var query = new QueryExpression
		{
			EntityName = Arthur.Contact().LogicalName,
			ColumnSet = new ColumnSet(false)
			{
				AttributeExpressions =
				{
					new XrmAttributeExpression(
						attributeName: Contact.Fields.NumberOfChildren,
						alias: "SumOfChildren",
						aggregateType: XrmAggregateType.Avg)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["SumOfChildren"].Should().Be(1);
	}
	
	[Test]
	public void Min_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child

		var query = new QueryExpression
		{
			EntityName = Arthur.Contact().LogicalName,
			ColumnSet = new ColumnSet(false)
			{
				AttributeExpressions =
				{
					new XrmAttributeExpression(
						attributeName: Contact.Fields.NumberOfChildren,
						alias: "SumOfChildren",
						aggregateType: XrmAggregateType.Min)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["SumOfChildren"].Should().Be(0);
	}
	
	[Test]
	public void Max_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child

		var query = new QueryExpression
		{
			EntityName = Arthur.Contact().LogicalName,
			ColumnSet = new ColumnSet(false)
			{
				AttributeExpressions =
				{
					new XrmAttributeExpression(
						attributeName: Contact.Fields.NumberOfChildren,
						alias: "SumOfChildren",
						aggregateType: XrmAggregateType.Max)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["SumOfChildren"].Should().Be(2);
	}
	
	[Test]
	public void Count_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child

		var query = new QueryExpression
		{
			EntityName = Arthur.Contact().LogicalName,
			ColumnSet = new ColumnSet(false)
			{
				AttributeExpressions =
				{
					new XrmAttributeExpression(
						attributeName: Contact.Fields.NumberOfChildren,
						alias: "SumOfChildren",
						aggregateType: XrmAggregateType.Count)
				}
			},
			Criteria = new FilterExpression
			{
				Conditions =
				{
					new ConditionExpression(Contact.Fields.NumberOfChildren, 
						ConditionOperator.GreaterThan, 0)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["SumOfChildren"].Should().Be(2);
	}
	
	[Test]
	public void ColumnCount_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child
		_organizationService.Simulated().Data().Add(Daniel.Contact()); // NULL Children

		var query = new QueryExpression
		{
			EntityName = Arthur.Contact().LogicalName,
			ColumnSet = new ColumnSet(false)
			{
				AttributeExpressions =
				{
					new XrmAttributeExpression(
						attributeName: Contact.Fields.NumberOfChildren,
						alias: "SumOfChildren",
						aggregateType: XrmAggregateType.CountColumn)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);

		// for avoidance of future doubt:
		//	This is the number of rows with data populated in this column
		//	Value should be three (Arthur, Bruce, Siobhan; Daniel should not be counted as column is NULL)
		aggregate.Entities.FirstOrDefault()!.Attributes["SumOfChildren"].Should().Be(3);
	}
}