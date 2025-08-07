using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.DataStores;
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
						alias: "AverageNumberOfChildren",
						aggregateType: XrmAggregateType.Avg)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["AverageNumberOfChildren"].Should().Be(1);
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
						alias: "MinNumberOfChildren",
						aggregateType: XrmAggregateType.Min)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["MinNumberOfChildren"].Should().Be(0);
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
						alias: "MaxNumberOfChildren",
						aggregateType: XrmAggregateType.Max)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["MaxNumberOfChildren"].Should().Be(2);
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
						alias: "ContactsWithChildren",
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
		aggregate.Entities.FirstOrDefault()!.Attributes["ContactsWithChildren"].Should().Be(2);
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
						alias: "RecordsWithChildrenDataPopulated",
						aggregateType: XrmAggregateType.CountColumn)
				}
			}
		};

		var aggregate = _organizationService.RetrieveMultiple(query);

		// for avoidance of future doubt:
		//	This is the number of rows with data populated in this column
		//	Value should be three (Arthur, Bruce, Siobhan; Daniel should not be counted as column is NULL)
		aggregate.Entities.FirstOrDefault()!.Attributes["RecordsWithChildrenDataPopulated"].Should().Be(3);
	}

	/// <summary>
	/// Validates the results from https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/queryexpression/aggregate-data#example
	/// </summary>
	[Test]
	public void Verify_Microsoft_Documentation_Aggregate_Sample()
	{
		AddMicrosoftSampleData();
		_organizationService.Simulated().Data().Add(new Account { Name = "Example Account", NumberOfEmployees = null, Address1_City = null, CreatedOn = new DateTime(2023, 8, 27)});
		
		QueryExpression query = new()
		{
			EntityName = "account",
			ColumnSet = new ColumnSet(false)
			{
				AttributeExpressions = {
					{
						new XrmAttributeExpression(
							attributeName: "numberofemployees",
							alias: "Average",
							aggregateType: XrmAggregateType.Avg)
					},
					{
						new XrmAttributeExpression(
							attributeName: "numberofemployees",
							alias: "Count",
							aggregateType: XrmAggregateType.Count)
					},
					{
						new XrmAttributeExpression(
							attributeName: "numberofemployees",
							alias: "ColumnCount",
							aggregateType: XrmAggregateType.CountColumn)
					},
					{
						new XrmAttributeExpression(
							attributeName: "numberofemployees",
							alias: "Maximum",
							aggregateType: XrmAggregateType.Max)
					},
					{
						new XrmAttributeExpression(
							attributeName: "numberofemployees",
							alias: "Minimum",
							aggregateType: XrmAggregateType.Min)
					},
					{
						new XrmAttributeExpression(
							attributeName: "numberofemployees",
							alias: "Sum",
							aggregateType: XrmAggregateType.Sum)
					}
				}
			}
		};

		var sut = _organizationService.RetrieveMultiple(query);
		var result = sut.Entities.FirstOrDefault()!;

		var average = (decimal)result.Attributes["Average"];
		Math.Floor(average).Should().Be(3911);
		
		result.Attributes["Count"].Should().Be(10);
		result.Attributes["ColumnCount"].Should().Be(9);
		result.Attributes["Maximum"].Should().Be(6200);
		result.Attributes["Minimum"].Should().Be(1500);
		result.Attributes["Sum"].Should().Be(35200);
	}
	
	/// <summary>
	/// Validates the results from https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/queryexpression/aggregate-data#grouping
	/// </summary>
	[Test]
	public void Verify_Microsoft_Documentation_GroupBy_Aggregate_Sample()
	{
		AddMicrosoftSampleData();
		_organizationService.Simulated().Data().Add(new Account { Name = "Example Account", NumberOfEmployees = null, Address1_City = null, CreatedOn = new DateTime(2023, 8, 27)});
		
		QueryExpression query = new()
		{
			EntityName = "account",
			ColumnSet = new ColumnSet(false)
			{
				AttributeExpressions = {
					{
						new XrmAttributeExpression(
							attributeName: "numberofemployees",
							alias: "Total",
							aggregateType: XrmAggregateType.Sum)
					},
					{
						new XrmAttributeExpression(
							attributeName: "address1_city",
							alias: "Count",
							aggregateType: XrmAggregateType.Count)
					},
					{
						new XrmAttributeExpression(
							attributeName: "address1_city",
							alias: "City",
							aggregateType: XrmAggregateType.None){
							HasGroupBy = true
						}
					}
				}
			}
		};
		query.Orders.Add(new OrderExpression(
			attributeName: "address1_city",
			alias: "City",
			orderType: OrderType.Ascending));

		var sut = _organizationService.RetrieveMultiple(query).Entities;

		// Row Count, distinct City plus NULL
		sut.Count.Should().Be(8);
		
		// NULL
		sut.First(x => (string)x.Attributes["City"] is null).Attributes["Total"].Should().Be(0);
		
		// Redmond (multiple rows, summed)
		var redmond = sut.First(x => (string)x.Attributes["City"] == "Redmond"); 
		redmond.Attributes["Total"].Should().Be(10600);
		redmond.Attributes["Count"].Should().Be(3);

		// Los Angeles (single row, still returned)
		sut.First(x => (string)x.Attributes["City"] == "Los Angeles").Attributes["Total"].Should().Be(2900);
	}
	
	/// <summary>
	/// Validates the results from
	///		https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/queryexpression/aggregate-data#grouping-by-parts-of-a-date
	/// </summary>
	[Test]
	public void Verify_Microsoft_Documentation_GroupBy_Date_Aggregate_Sample()
	{
		AddMicrosoftSampleData();
		_organizationService.Simulated().Data().Add(new Account { Name = "Example Account", NumberOfEmployees = null, Address1_City = null, CreatedOn = new DateTime(2023, 8, 27)});
		
		var query = _dateTimeGroupingQuery;
		query.Orders.Add(new OrderExpression(
		            attributeName: "createdon",
		            alias: "Month",
		            orderType: OrderType.Ascending));

		var sut = _organizationService.RetrieveMultiple(query).Entities;

		// Row Count
		sut.Count.Should().Be(2);
		
		// Totals by fiscal grouping
		sut.First(x => (int)x.Attributes["Month"] == 3).Attributes["Total"].Should().Be(35200);
		sut.First(x => (int)x.Attributes["Month"] == 8).Attributes["Total"].Should().Be(0);
		
	}
	
	[Test]
	public void Date_Grouping_And_Fiscal_Settings_For_Aggregates_Are_Valid()
	{
		AddMicrosoftSampleData();
		
		var query = _dateTimeGroupingQuery;
		query.Orders.Add(new OrderExpression(
		            attributeName: "createdon",
		            alias: "Month",
		            orderType: OrderType.Ascending));

		var sut = _organizationService.RetrieveMultiple(query).Entities;

		// Row Count
		sut.Count.Should().Be(1);
		
		sut.FirstOrDefault()!.Attributes["Total"].Should().Be(35200);
		
		// Totals by fiscal grouping
		sut.FirstOrDefault()!.Attributes["Day"].Should().Be(25);
		sut.FirstOrDefault()!.Attributes["Week"].Should().Be(12);
		sut.FirstOrDefault()!.Attributes["Month"].Should().Be(3);
		sut.FirstOrDefault()!.Attributes["Year"].Should().Be(2023);
		sut.FirstOrDefault()!.Attributes["FiscalYear"].Should().Be("FY2023");
	}
	
	[Test]
	public void Passing_In_Fiscal_Year_Settings_Applies_Correct_FY_Prefix()
	{
		var simulatorOptions = new SimulatorOptions
		{
			FiscalYearSettings = new FiscalYearSettings
			{
				FiscalYearPrefix = "AF",
				FiscalYearNameBasedOnStartDate = false
			}
		};

		_organizationService.Simulate(simulatorOptions);
		
		AddMicrosoftSampleData();
		
		var query = _dateTimeGroupingQuery;
		query.Orders.Add(new OrderExpression(
		            attributeName: "createdon",
		            alias: "Month",
		            orderType: OrderType.Ascending));

		var sut = _organizationService.RetrieveMultiple(query).Entities;
		
		sut.FirstOrDefault()!.Attributes["Year"].Should().Be(2023);
		
		// Uses 'AF' as prefix and 2024 as ending year
		sut.FirstOrDefault()!.Attributes["FiscalYear"].Should().Be("AF2024");
	}

	private void AddMicrosoftSampleData()
	{
		_organizationService.Simulated().Data().Add(new Account { Name = "Contoso Pharmaceuticals", NumberOfEmployees = 1500, Address1_City = "Redmond", CreatedOn = new DateTime(2023, 3, 25)});
		_organizationService.Simulated().Data().Add(new Account { Name = "Fabrikam, Inc", NumberOfEmployees = 2700, Address1_City = "Lynnwood", CreatedOn = new DateTime(2023, 3, 25)});
		_organizationService.Simulated().Data().Add(new Account { Name = "Blue Yonder Airlines", NumberOfEmployees = 2900, Address1_City = "Los Angeles", CreatedOn = new DateTime(2023, 3, 25)});
		_organizationService.Simulated().Data().Add(new Account { Name = "City Power & Light", NumberOfEmployees = 2900, Address1_City = "Redmond", CreatedOn = new DateTime(2023, 3, 25)});
		_organizationService.Simulated().Data().Add(new Account { Name = "Coho Winery", NumberOfEmployees = 3900, Address1_City = "Phoenix", CreatedOn = new DateTime(2023, 3, 25)});
		_organizationService.Simulated().Data().Add(new Account { Name = "Adventure Works", NumberOfEmployees = 4300, Address1_City = "Santa Cruz", CreatedOn = new DateTime(2023, 3, 25)});
		_organizationService.Simulated().Data().Add(new Account { Name = "Alpine Ski House", NumberOfEmployees = 4800, Address1_City = "Missoula", CreatedOn = new DateTime(2023, 3, 25)});
		_organizationService.Simulated().Data().Add(new Account { Name = "Litware, Inc", NumberOfEmployees = 6000, Address1_City = "Dallas", CreatedOn = new DateTime(2023, 3, 25)});
		_organizationService.Simulated().Data().Add(new Account { Name = "A. Datum Corporation", NumberOfEmployees = 6200, Address1_City = "Redmond", CreatedOn = new DateTime(2023, 3, 25)});

	}
	
	private readonly QueryExpression _dateTimeGroupingQuery = new()
		{
		    EntityName = "account",
		    ColumnSet = new ColumnSet(false)
		    {
		        AttributeExpressions = {
		            {
		                new XrmAttributeExpression(
		                    attributeName: "numberofemployees",
		                    alias: "Total",
		                    aggregateType: XrmAggregateType.Sum)
		            },
		            {
		                new XrmAttributeExpression(
		                    attributeName: "createdon",
		                    alias: "Day",
		                    aggregateType: XrmAggregateType.None){
		                    HasGroupBy = true,
		                    DateTimeGrouping = XrmDateTimeGrouping.Day
		                }
		            },
		            {
		                new XrmAttributeExpression(
		                    attributeName: "createdon",
		                    alias: "Week",
		                    aggregateType: XrmAggregateType.None){
		                    HasGroupBy = true,
		                    DateTimeGrouping = XrmDateTimeGrouping.Week
		                }
		            },
		                                    {
		                new XrmAttributeExpression(
		                    attributeName: "createdon",
		                    alias: "Month",
		                    aggregateType: XrmAggregateType.None){
		                    HasGroupBy = true,
		                    DateTimeGrouping = XrmDateTimeGrouping.Month
		                }
		            },
		            {
		                new XrmAttributeExpression(
		                    attributeName: "createdon",
		                    alias: "Year",
		                    aggregateType: XrmAggregateType.None){
		                    HasGroupBy = true,
		                    DateTimeGrouping = XrmDateTimeGrouping.Year
		                }
		            },
		            {
		                new XrmAttributeExpression(
		                    attributeName: "createdon",
		                    alias: "FiscalPeriod",
		                    aggregateType: XrmAggregateType.None){
		                    HasGroupBy = true,
		                    DateTimeGrouping = XrmDateTimeGrouping.FiscalPeriod
		                }
		            },
		            {
		                new XrmAttributeExpression(
		                    attributeName: "createdon",
		                    alias: "FiscalYear",
		                    aggregateType: XrmAggregateType.None){
		                    HasGroupBy = true,
		                    DateTimeGrouping = XrmDateTimeGrouping.FiscalYear
		                }
		            }
		        }
		    }
		};
}