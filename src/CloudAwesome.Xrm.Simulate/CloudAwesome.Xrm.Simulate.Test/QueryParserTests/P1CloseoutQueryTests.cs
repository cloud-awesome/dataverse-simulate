using System;
using System.IO;
using System.Linq;
using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.QueryParsers;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture]
public sealed class P1CloseoutQueryTests
{
	[Test]
	public void Linked_Ordering_Uses_Unselected_Values_Without_Leaking_Columns(
		[Values(false, true)] bool fetch,
		[Values(false, true)] bool descending,
		[Values("all", "top", "page", "distinct")]
		string mode)
	{
		var service = Service();
		Seed(service, "Zulu", "Zulu");
		Seed(service, "Alpha", "Alpha");
		Seed(service, "Middle", "Middle");
		var query = Query();
		query.LinkEntities[0].Orders
			.Add(new OrderExpression("lastname", descending ? OrderType.Descending : OrderType.Ascending));
		if (mode == "top") query.TopCount = 1;
		if (mode == "page")
			query.PageInfo = new PagingInfo { Count = 1, PageNumber = 2, ReturnTotalRecordCount = true };
		query.Distinct = mode == "distinct";
		QueryBase input = fetch
			? new FetchExpression($"""
			                       <fetch {(mode == "top" ? "top='1'" : mode == "page" ? "count='1' page='2' returntotalrecordcount='true'" : "")}
			                              distinct='{query.Distinct.ToString().ToLowerInvariant()}'>
			                         <entity name='account'><attribute name='name'/>
			                           <link-entity name='contact' from='contactid' to='primarycontactid' alias='c'>
			                             <attribute name='firstname'/><order attribute='lastname' descending='{descending.ToString().ToLowerInvariant()}'/>
			                           </link-entity>
			                         </entity>
			                       </fetch>
			                       """)
			: query;

		var result = service.RetrieveMultiple(input);

		var expected = descending ? new[] { "Zulu", "Middle", "Alpha" } : new[] { "Alpha", "Middle", "Zulu" };
		if (mode == "top") expected = expected.Take(1).ToArray();
		if (mode == "page") expected = ["Middle"];
		result.Entities.Select(e => e.GetAttributeValue<string>("name")).Should().Equal(expected);
		foreach (var row in result.Entities)
			row.Attributes.Keys.Should().BeEquivalentTo(mode == "distinct"
				? new[] { "name", "c.firstname" }
				: new[] { "accountid", "name", "c.firstname" });
		if (mode == "page")
		{
			result.TotalRecordCount.Should().Be(3);
			result.MoreRecords.Should().BeTrue();
		}

		query.LinkEntities[0].Columns.Columns.Should().Equal("firstname");
		service.Simulated().Data().Get("account").SelectMany(e => e.Attributes.Keys)
			.Should().OnlyContain(key => key == "accountid" || key == "name" || key == "primarycontactid");
	}

	[Test]
	public void Linked_Orders_Preserve_Base_Precedence_And_Multiple_Tie_Breakers()
	{
		var service = Service();
		Seed(service, "Group B", "Alpha", "Zulu");
		Seed(service, "Group A", "Zulu", "Alpha");
		Seed(service, "Group A", "Alpha", "Zulu");
		Seed(service, "Group A", "Alpha", "Alpha");
		var query = Query();
		query.Orders.Add(new OrderExpression("name", OrderType.Ascending));
		query.LinkEntities[0].Orders.Add(new OrderExpression("lastname", OrderType.Ascending));
		query.LinkEntities[0].Orders.Add(new OrderExpression("firstname", OrderType.Ascending));
		var result = service.RetrieveMultiple(query);
		result.Entities.Select(e => (e.GetAttributeValue<string>("name"),
				(string)e.GetAttributeValue<AliasedValue>("c.firstname").Value))
			.Should().Equal(("Group A", "Alpha"), ("Group A", "Zulu"), ("Group A", "Alpha"), ("Group B", "Zulu"));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void Hidden_Sort_Values_Do_Not_Change_Distinct_Identity(bool selectBaseId)
	{
		var service = Service();
		Seed(service, "Same", "Zulu", "Same");
		Seed(service, "Same", "Alpha", "Same");
		var query = Query();
		query.Distinct = true;
		if (selectBaseId) query.ColumnSet.AddColumn("accountid");
		query.LinkEntities[0].Orders.Add(new OrderExpression("lastname", OrderType.Ascending));
		var result = service.RetrieveMultiple(query);
		result.Entities.Should().HaveCount(selectBaseId ? 2 : 1);
		result.Entities.SelectMany(e => e.Attributes.Keys).Should().NotContain("c.lastname");
	}

	[TestCase(false)]
	[TestCase(true)]
	public void Nested_Link_Sort_Values_Survive_Parent_Projection(bool allParentColumns)
	{
		var service = Service();
		Seed(service, "Zulu", "Zulu");
		Seed(service, "Alpha", "Alpha");
		var query = Query();
		var parent = query.LinkEntities[0];
		parent.Columns = new ColumnSet(allParentColumns);
		var nested = parent.AddLink("contact", "contactid", "contactid");
		nested.EntityAlias = "nested";
		nested.Columns = new ColumnSet(false);
		nested.Orders.Add(new OrderExpression("lastname", OrderType.Ascending));
		query.TopCount = 1;

		var result = service.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

		result.GetAttributeValue<string>("name").Should().Be("Alpha");
		result.Attributes.Keys.Should().NotContain(key => key.Contains("__simulator_link_order_"));
		result.Contains("nested.lastname").Should().BeFalse();
	}

	[Test]
	public void Sibling_Link_Orders_Use_Declaration_Order_For_Precedence()
	{
		var service = Service();
		Seed(service, "First", "Alpha", "Zulu");
		Seed(service, "Second", "Zulu", "Alpha");
		var query = Query();
		query.LinkEntities[0].Columns = new ColumnSet(false);
		query.LinkEntities[0].Orders.Add(new OrderExpression("lastname", OrderType.Ascending));
		var second = query.AddLink("contact", "primarycontactid", "contactid");
		second.EntityAlias = "second";
		second.Columns = new ColumnSet(false);
		second.Orders.Add(new OrderExpression("firstname", OrderType.Ascending));

		var rows = service.RetrieveMultiple(query).Entities;

		rows.Select(e => e.GetAttributeValue<string>("name")).Should().Equal("First", "Second");
		rows.SelectMany(e => e.Attributes.Keys).Should().OnlyContain(key => key == "accountid" || key == "name");
	}

	[Test]
	public void AnyAll_Filter_Links_Are_Rejected_Even_In_Empty_Queries()
	{
		var service = Service();
		var query = Query();
		query.Criteria.AddFilter(LogicalOperator.Or).AnyAllFilterLinkEntity =
			new LinkEntity("account", "contact", "accountid", "parentcustomerid", JoinOperator.Any);
		Action act = () => service.RetrieveMultiple(query);
		act.Should().Throw<NotSupportedException>().WithMessage("*AnyAllFilterLinkEntity*");
		service.Simulated().Audit().Get().Should().BeEmpty();
	}

	[Test]
	public void Outer_Link_Orders_Missing_And_Null_Values_And_Does_Not_Collide_With_Base_Attributes(
		[Values(false, true)] bool alias,
		[Values(false, true)] bool descending)
	{
		var service = Service();
		Seed(service, "Zulu", "Zulu");
		Seed(service, "Alpha", "Alpha");
		var nullContact = Seed(service, "Null", "Null");
		service.Simulated().Data().Get("contact", nullContact.GetAttributeValue<EntityReference>("primarycontactid").Id)
			.Attributes.Remove("lastname");
		service.Simulated().Data().Add(new Entity("account", Guid.NewGuid()) { ["name"] = "Missing" });
		var query = Query();
		var link = query.LinkEntities[0];
		link.EntityAlias = alias ? "c" : null;
		link.Columns = new ColumnSet(false);
		link.JoinOperator = JoinOperator.LeftOuter;
		link.Orders.Add(new OrderExpression("lastname", descending ? OrderType.Descending : OrderType.Ascending));
		var results = service.RetrieveMultiple(query).Entities;
		results.Select(e => e.GetAttributeValue<string>("name")).Should().Equal(descending
			? new[] { "Zulu", "Alpha", "Null", "Missing" }
			: new[] { "Null", "Missing", "Alpha", "Zulu" });
		results.SelectMany(e => e.Attributes.Keys).Should().OnlyContain(key => key == "accountid" || key == "name");
	}

	[Test]
	public void Qualified_Inputs_Are_Rejected_Before_Execution_And_Auditing(
		[Values("condition", "comparison", "dotted-condition", "order", "dotted-order", "column")]
		string shape,
		[Values("base", "filter", "link", "nested-link")]
		string location,
		[Values(false, true)] bool request)
	{
		var service = Service();
		// Deliberately empty data: unsupported inputs must never quietly return no rows.
		var query = Query();
		var filter = query.Criteria;
		var orders = query.Orders;
		var columns = query.ColumnSet;
		if (location == "filter") filter = filter.AddFilter(LogicalOperator.Or);
		if (location is "link" or "nested-link")
		{
			var link = query.LinkEntities[0];
			if (location == "nested-link") link = link.AddLink("contact", "contactid", "contactid");
			filter = link.LinkCriteria.AddFilter(LogicalOperator.And);
			orders = link.Orders;
			columns = link.Columns;
		}

		switch (shape)
		{
			case "condition":
				filter.Conditions.Add(new ConditionExpression("lastname", ConditionOperator.Equal, "Alpha")
					{ EntityName = "c" }); break;
			case "comparison":
				filter.Conditions.Add(new ConditionExpression("firstname", ConditionOperator.Equal, "lastname")
					{ CompareColumns = true }); break;
			case "dotted-condition": filter.AddCondition("c.lastname", ConditionOperator.Equal, "Alpha"); break;
			case "order": orders.Add(new OrderExpression("lastname", OrderType.Ascending) { EntityName = "c" }); break;
			case "dotted-order": orders.Add(new OrderExpression("c.lastname", OrderType.Ascending)); break;
			case "column": columns.AddColumn("c.lastname"); break;
		}

		Action act = () =>
		{
			if (request) service.Execute(new RetrieveMultipleRequest { Query = query });
			else service.RetrieveMultiple(query);
		};
		act.Should().Throw<NotSupportedException>().WithMessage("*not supported*");
		service.Simulated().Audit().Get().Should().BeEmpty();
	}

	[Test]
	public void Qualified_Inputs_Are_Rejected_Consistently_With_Metadata()
	{
		var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
		{
			Metadata = SimulatedMetadata.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata",
				"entity-metadata.json"))
		});
		var query = new QueryExpression("account");
		query.Criteria.Conditions.Add(new ConditionExpression("lastname", ConditionOperator.Equal, "Alpha")
			{ EntityName = "c" });
		Action act = () => service.RetrieveMultiple(query);
		act.Should().Throw<NotSupportedException>().WithMessage("*EntityName*");
	}

	[Test]
	public void Direct_Parser_Rejects_Qualified_Conditions()
	{
		var query = Query();
		query.Criteria.Conditions.Add(new ConditionExpression("lastname", ConditionOperator.Equal, "Alpha")
			{ EntityName = "c" });
		Action act = () => QueryExpressionParser.Parse(query, [], new MockedEntityDataService()).ToList();
		act.Should().Throw<NotSupportedException>();
	}

	[TestCase("order")]
	[TestCase("attribute")]
	[TestCase("column")]
	public void QueryByAttribute_Rejects_Qualified_Inputs(string shape)
	{
		var service = Service();
		var query = new QueryByAttribute("contact") { ColumnSet = new ColumnSet("firstname") };
		if (shape == "order")
			query.Orders.Add(new OrderExpression("lastname", OrderType.Ascending) { EntityName = "c" });
		if (shape == "attribute") query.AddAttributeValue("c.lastname", "Alpha");
		if (shape == "column") query.ColumnSet.AddColumn("c.lastname");
		Action act = () => service.RetrieveMultiple(query);
		act.Should().Throw<NotSupportedException>();
		service.Simulated().Audit().Get().Should().BeEmpty();
	}

	[TestCase("<order attribute='lastname' entityname='c'/>")]
	[TestCase("<order attribute='c.lastname'/>")]
	public void FetchXml_Rejects_Qualified_Orders(string order)
	{
		var service = Service();
		Action act = () =>
			service.RetrieveMultiple(new FetchExpression($"<fetch><entity name='contact'>{order}</entity></fetch>"));
		act.Should().Throw<NotSupportedException>();
	}

	private static IOrganizationService Service() => ((IOrganizationService)null!).Simulate();

	private static QueryExpression Query()
	{
		var query = new QueryExpression("account") { ColumnSet = new ColumnSet("name") };
		var link = query.AddLink("contact", "primarycontactid", "contactid");
		link.EntityAlias = "c";
		link.Columns = new ColumnSet("firstname");
		return query;
	}

	private static Entity Seed(IOrganizationService service, string accountName, string lastName,
		string firstName = "Visible")
	{
		var contact = new Entity("contact", Guid.NewGuid()) { ["firstname"] = firstName, ["lastname"] = lastName };
		contact["contactid"] = contact.Id;
		var account = new Entity("account", Guid.NewGuid())
			{ ["name"] = accountName, ["primarycontactid"] = contact.ToEntityReference() };
		account["accountid"] = account.Id;
		service.Simulated().Data().Add(contact);
		service.Simulated().Data().Add(account);
		return account;
	}
}