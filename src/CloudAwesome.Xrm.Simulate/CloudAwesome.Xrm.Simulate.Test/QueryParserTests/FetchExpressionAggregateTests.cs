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

public class FetchExpressionAggregateTests
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

		var fetch = @"<fetch aggregate=""true"">
                        <entity name=""contact"">
                          <attribute name=""numberofchildren"" alias=""SumOfChildren"" aggregate=""sum"" />
                        </entity>
                      </fetch>";

		var query = new FetchExpression(fetch);
		var aggregate = _organizationService.RetrieveMultiple(query);
		
		aggregate.Entities.FirstOrDefault()!.Attributes["SumOfChildren"].Should().Be(3);
	}
	
	[Test]
	public void Average_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child

		var fetch = @"<fetch aggregate=""true"">
                        <entity name=""contact"">
                          <attribute name=""numberofchildren"" alias=""AverageNumberOfChildren"" aggregate=""avg"" />
                        </entity>
                      </fetch>";

		var query = new FetchExpression(fetch);

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["AverageNumberOfChildren"].Should().Be(1);
	}
	
	[Test]
	public void Min_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child

		var fetch = @"<fetch aggregate=""true"">
                        <entity name=""contact"">
                          <attribute name=""numberofchildren"" alias=""MinNumberOfChildren"" aggregate=""min"" />
                        </entity>
                      </fetch>";

		var query = new FetchExpression(fetch);

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["MinNumberOfChildren"].Should().Be(0);
	}
	
	[Test]
	public void Max_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child

		var fetch = @"<fetch aggregate=""true"">
                        <entity name=""contact"">
                          <attribute name=""numberofchildren"" alias=""MaxNumberOfChildren"" aggregate=""max"" />
                        </entity>
                      </fetch>";

		var query = new FetchExpression(fetch);

		var aggregate = _organizationService.RetrieveMultiple(query);
		aggregate.Entities.FirstOrDefault()!.Attributes["MaxNumberOfChildren"].Should().Be(2);
	}
	
	[Test]
	public void Count_Aggregate_Returns_Valid_Result()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact()); // 0 Children
		_organizationService.Simulated().Data().Add(Bruce.Contact()); // 2 Children
		_organizationService.Simulated().Data().Add(Siobhan.Contact()); // 1 Child

		var fetch = @"<fetch aggregate=""true"">
                        <entity name=""contact"">
                          <attribute name=""numberofchildren"" alias=""ContactsWithChildren"" aggregate=""count"" />
                          <filter type='and'>
							<condition attribute='numberofchildren' operator='gt' value='0' />
						  </filter>
                        </entity>
                      </fetch>";

		var query = new FetchExpression(fetch);

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

		var fetch = @"<fetch aggregate=""true"">
                        <entity name=""contact"">
                          <attribute name=""numberofchildren"" alias=""RecordsWithChildrenDataPopulated"" aggregate=""countcolumn"" />
                        </entity>
                      </fetch>";

		var query = new FetchExpression(fetch);

		var aggregate = _organizationService.RetrieveMultiple(query);

		// for avoidance of future doubt:
		//	This is the number of rows with data populated in this column
		//	Value should be three (Arthur, Bruce, Siobhan; Daniel should not be counted as column is NULL)
		aggregate.Entities.FirstOrDefault()!.Attributes["RecordsWithChildrenDataPopulated"].Should().Be(3);
	}

	/// <summary>
	/// Validates the results from https://learn.microsoft.com/en-us/power-apps/developer/data-platform/fetchxml/aggregate-data#example
	/// </summary>
	[Test]
	public void Verify_Microsoft_Documentation_Aggregate_Sample()
	{
		AddMicrosoftSampleData();
		_organizationService.Simulated().Data().Add(new Account { Name = "Example Account", NumberOfEmployees = null, Address1_City = null, CreatedOn = new DateTime(2023, 8, 27)});

		const string fetch = 
			"""
             <fetch aggregate='true'>
               <entity name='account'>
                 <attribute name='numberofemployees'
                   alias='Average'
                   aggregate='avg' />
                 <attribute name='numberofemployees'
                   alias='Count'
                   aggregate='count' />
                 <attribute name='numberofemployees'
                   alias='ColumnCount'
                   aggregate='countcolumn' />
                 <attribute name='numberofemployees'
                   alias='Maximum'
                   aggregate='max' />
                 <attribute name='numberofemployees'
                   alias='Minimum'
                   aggregate='min' />
                 <attribute name='numberofemployees'
                   alias='Sum'
                   aggregate='sum' />
               </entity>
             </fetch>
             """;
		var query = new FetchExpression(fetch);
		
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
	/// Validates the results from https://learn.microsoft.com/en-us/power-apps/developer/data-platform/fetchxml/aggregate-data#distinct-column-values
	///
	/// TODO - Haven't implemented distinct count yet!
	/// </summary>
	[Test]
	[Ignore("Haven't implemented distinct count yet!")]
	public void Verify_Microsoft_Documentation_Distinct_Values_Sample()
	{
		AddMicrosoftSampleData();
		_organizationService.Simulated().Data().Add(new Account { Name = "Example Account", NumberOfEmployees = null, Address1_City = null, CreatedOn = new DateTime(2023, 8, 27)});

		const string fetch = 
			"""
			<fetch aggregate='true'>
			  <entity name='account'>
			    <attribute name='numberofemployees'
			      alias='ColumnCount'
			      aggregate='countcolumn'
			      distinct='true' />
			  </entity>
			</fetch>
			""";
		var query = new FetchExpression(fetch);
		
		var sut = _organizationService.RetrieveMultiple(query);
		var result = sut.Entities.FirstOrDefault()!;

		result.Attributes["ColumnCount"].Should().Be(8);
	}

	/// <summary>
	/// Validates the results from https://learn.microsoft.com/en-us/power-apps/developer/data-platform/fetchxml/aggregate-data#grouping
	/// </summary>
	[Test]
	public void Verify_Microsoft_Documentation_GroupBy_Aggregate_Sample()
	{
		AddMicrosoftSampleData();
		_organizationService.Simulated().Data().Add(new Account { Name = "Example Account", NumberOfEmployees = null, Address1_City = null, CreatedOn = new DateTime(2023, 8, 27)});

		const string fetch =
			"""
			<fetch aggregate='true'>
			   <entity name='account'>
			      <attribute name='numberofemployees'
			         alias='Total'
			         aggregate='sum' />
			      <attribute name='address1_city'
			         alias='Count'
			         aggregate='count' />
			      <attribute name='address1_city'
			         alias='City'
			         groupby='true' />
			      <order alias='City' />
			   </entity>
			</fetch>
			""";
		
		var query = new FetchExpression(fetch);
		
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
	///		https://learn.microsoft.com/en-us/power-apps/developer/data-platform/fetchxml/aggregate-data#grouping-by-parts-of-a-date
	/// </summary>
	[Test]
	public void Verify_Microsoft_Documentation_GroupBy_Date_Aggregate_Sample()
	{
		AddMicrosoftSampleData();
		_organizationService.Simulated().Data().Add(new Account { Name = "Example Account", NumberOfEmployees = null, Address1_City = null, CreatedOn = new DateTime(2023, 8, 27)});

		var query = _dateTimeGroupingFetchQuery();
		
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
		
		var query = _dateTimeGroupingFetchQuery();

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

		var query = _dateTimeGroupingFetchQuery();

		var sut = _organizationService.RetrieveMultiple(query).Entities;
		
		sut.FirstOrDefault()!.Attributes["Year"].Should().Be(2023);
		
		// Uses 'AF' as prefix and 2024 as ending year
		sut.FirstOrDefault()!.Attributes["FiscalYear"].Should().Be("AF2024");
	}

	// TODO - Implement 50,000 aggregate value limits
	//	- https://learn.microsoft.com/en-us/power-apps/developer/data-platform/fetchxml/aggregate-data#limitations
	// TODO - Implement and test aggregateLimits
	//	- https://learn.microsoft.com/en-us/power-apps/developer/data-platform/fetchxml/aggregate-data#per-query-limit
	
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

	private FetchExpression _dateTimeGroupingFetchQuery()
	{
		var query =
			"""
			<fetch aggregate='true'>
			   <entity name='account'>
			      <attribute name='numberofemployees'
			         alias='Total'
			         aggregate='sum' />
			      <attribute name='createdon'
			         alias='Day'
			         groupby='true'
			         dategrouping='day' />
			      <attribute name='createdon'
			         alias='Week'
			         groupby='true'
			         dategrouping='week' />
			      <attribute name='createdon'
			         alias='Month'
			         groupby='true'
			         dategrouping='month' />
			      <attribute name='createdon'
			         alias='Year'
			         groupby='true'
			         dategrouping='year' />
			      <attribute name='createdon'
			         alias='FiscalPeriod'
			         groupby='true'
			         dategrouping='fiscal-period' />
			      <attribute name='createdon'
			         alias='FiscalYear'
			         groupby='true'
			         dategrouping='fiscal-year' />
			      <order alias='Month' />
			   </entity>
			</fetch>
			""";

		return new FetchExpression(query);
	}
}