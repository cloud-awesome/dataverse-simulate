using System;
using System.Collections.Generic;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture]
[Category("Parity")]
[Category("P1Closeout")]
public sealed class P1CloseoutParityTests : IntegrationBaseFixture
{
	[Test]
	public void Unselected_Linked_Ordering_Matches_Dataverse(
		[Values(false, true)] bool fetch,
		[Values(false, true)] bool descending,
		[Values("all", "top", "page")] string mode)
	{
		var marker = $"Simulate P1 {Guid.NewGuid():N}";
		DataverseParityHarness.Execute(Scenario(marker, service =>
		{
			var query = Query(marker);
			query.LinkEntities[0].Orders
				.Add(new OrderExpression("lastname", descending ? OrderType.Descending : OrderType.Ascending));
			if (mode == "top") query.TopCount = 1;
			if (mode == "page")
				query.PageInfo = new PagingInfo { Count = 1, PageNumber = 2, ReturnTotalRecordCount = true };
			QueryBase input = fetch
				? new FetchExpression($"""
				                       <fetch {(mode == "top" ? "top='1'" : mode == "page" ? "count='1' page='2' returntotalrecordcount='true'" : "")}>
				                         <entity name='account'><attribute name='accountnumber'/>
				                           <filter><condition attribute='name' operator='eq' value='{marker}'/></filter>
				                           <link-entity name='contact' from='contactid' to='primarycontactid' alias='c'>
				                             <attribute name='firstname'/><order attribute='lastname' descending='{descending.ToString().ToLowerInvariant()}'/>
				                           </link-entity>
				                         </entity>
				                       </fetch>
				                       """)
				: query;
			var result = service.RetrieveMultiple(input);
			var expected = descending ? new[] { "Zulu", "Alpha" } : new[] { "Alpha", "Zulu" };
			if (mode == "top") expected = expected.Take(1).ToArray();
			if (mode == "page") expected = expected.Skip(1).ToArray();
			result.Entities.Select(e => e.GetAttributeValue<string>("accountnumber")).Should().Equal(expected);
			foreach (var row in result.Entities)
				row.Attributes.Keys.Should().BeEquivalentTo("accountid", "accountnumber", "c.firstname");
			if (mode == "page")
			{
				result.TotalRecordCount.Should().Be(2);
				result.MoreRecords.Should().BeFalse();
			}

			return result.Entities.Select(e => e.Id).ToArray();
		}));
	}

	[TestCase("condition")]
	[TestCase("order")]
	[TestCase("comparison")]
	public void Supported_Live_Qualified_Forms_Are_Explicitly_Rejected_By_Simulator(string shape)
	{
		var marker = $"Simulate P1 {Guid.NewGuid():N}";
		var scenario = Scenario(marker, service =>
		{
			var query = Query(marker);
			if (shape == "condition")
				query.Criteria.Conditions.Add(
					new ConditionExpression("lastname", ConditionOperator.Equal, "Alpha") { EntityName = "c" });
			if (shape == "order")
				query.Orders.Add(new OrderExpression("lastname", OrderType.Ascending) { EntityName = "c" });
			if (shape == "comparison")
				query.LinkEntities[0].LinkCriteria.Conditions.Add(
					new ConditionExpression("firstname", ConditionOperator.Equal, "lastname")
						{ CompareColumns = true });
			try
			{
				var result = service.RetrieveMultiple(query);
				return new QueryOutcome(
					result.Entities.Select(e => e.GetAttributeValue<string>("accountnumber")).ToArray(), null);
			}
			catch (NotSupportedException exception)
			{
				return new QueryOutcome([], exception);
			}
		}, (live, simulated) =>
		{
			live.Error.Should().BeNull();
			live.Numbers.Should().Equal(shape == "order" ? new[] { "Alpha", "Zulu" } : new[] { "Alpha" });
			simulated.Error.Should().BeOfType<NotSupportedException>().Which.Message.Should()
				.Contain(shape == "comparison" ? "CompareColumns" : "EntityName");
			TestContext.WriteLine(
				$"Live {shape}: {string.Join(", ", live.Numbers)}; simulator: {simulated.Error!.Message}");
		});
		DataverseParityHarness.Execute(scenario);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void Unqualified_LinkCriteria_Remains_A_Supported_Alternative(bool fetch)
	{
		var marker = $"Simulate P1 {Guid.NewGuid():N}";
		DataverseParityHarness.Execute(Scenario(marker, service =>
		{
			var query = Query(marker);
			query.LinkEntities[0].LinkCriteria.AddCondition("lastname", ConditionOperator.Equal, "Alpha");
			QueryBase input = fetch
				? new FetchExpression($"""
				                       <fetch><entity name='account'><attribute name='accountnumber'/>
				                         <filter><condition attribute='name' operator='eq' value='{marker}'/></filter>
				                         <link-entity name='contact' from='contactid' to='primarycontactid' alias='c'>
				                           <attribute name='firstname'/><filter><condition attribute='lastname' operator='eq' value='Alpha'/></filter>
				                         </link-entity>
				                       </entity></fetch>
				                       """)
				: query;
			var row = service.RetrieveMultiple(input).Entities.Should().ContainSingle().Subject;
			row.GetAttributeValue<string>("accountnumber").Should().Be("Alpha");
			return row.Id;
		}));
	}

	[Test]
	public void Ordinary_Crud_Payloads_And_Responses_Are_Detached(
		[Values(false, true)] bool request,
		[Values(false, true)] bool allColumns)
	{
		LiveDataverseScenarioContext? liveContext = null;
		var scenario = new DataverseParityScenario<string[]>
		{
			Name = nameof(Ordinary_Crud_Payloads_And_Responses_Are_Detached),
			ArrangeLive = context => liveContext = context,
			Act = service =>
			{
				var input = new Entity("contact", Guid.NewGuid())
				{
					["lastname"] = "Before", ["annualincome"] = new Money(10), ["gendercode"] = new OptionSetValue(1)
				};
				if (ReferenceEquals(service, liveContext!.Service))
					liveContext.Cleanup.TrackForDelete("contact", input.Id);
				Guid id;
				if (request) id = ((CreateResponse)service.Execute(new CreateRequest { Target = input })).id;
				else id = service.Create(input);
				input["lastname"] = "Caller mutation";
				input.GetAttributeValue<Money>("annualincome").Value = 99;
				input.GetAttributeValue<OptionSetValue>("gendercode").Value = 2;
				var selected = new ColumnSet("lastname", "annualincome", "gendercode");
				var afterCreate = service.Retrieve("contact", id, selected);
				afterCreate.GetAttributeValue<string>("lastname").Should().Be("Before");
				afterCreate.GetAttributeValue<Money>("annualincome").Value.Should().Be(10);
				afterCreate.GetAttributeValue<OptionSetValue>("gendercode").Value.Should().Be(1);
				var patch = new Entity("contact", id)
				{
					["lastname"] = "Updated", ["annualincome"] = new Money(20), ["gendercode"] = new OptionSetValue(2)
				};
				if (request) service.Execute(new UpdateRequest { Target = patch });
				else service.Update(patch);
				patch["lastname"] = "Caller mutation";
				patch.GetAttributeValue<Money>("annualincome").Value = 99;
				patch.GetAttributeValue<OptionSetValue>("gendercode").Value = 1;
				var columns = allColumns ? new ColumnSet(true) : selected;
				var read = request
					? ((RetrieveResponse)service.Execute(new RetrieveRequest
						{ Target = new EntityReference("contact", id), ColumnSet = columns })).Entity
					: service.Retrieve("contact", id, columns);
				read.GetAttributeValue<string>("lastname").Should().Be("Updated");
				read.GetAttributeValue<Money>("annualincome").Value.Should().Be(20);
				read.GetAttributeValue<OptionSetValue>("gendercode").Value.Should().Be(2);
				read["lastname"] = "Response mutation";
				read.GetAttributeValue<Money>("annualincome").Value = 99;
				read.GetAttributeValue<OptionSetValue>("gendercode").Value = 1;
				var final = service.Retrieve("contact", id, selected);
				final.GetAttributeValue<string>("lastname").Should().Be("Updated");
				final.GetAttributeValue<Money>("annualincome").Value.Should().Be(20);
				final.GetAttributeValue<OptionSetValue>("gendercode").Value.Should().Be(2);
				return new[]
					{ afterCreate.GetAttributeValue<string>("lastname"), final.GetAttributeValue<string>("lastname") };
			}
		};
		DataverseParityHarness.Execute(scenario);
	}

	private static QueryExpression Query(string marker)
	{
		var query = new QueryExpression("account") { ColumnSet = new ColumnSet("accountnumber") };
		query.Criteria.AddCondition("name", ConditionOperator.Equal, marker);
		var link = query.AddLink("contact", "primarycontactid", "contactid");
		link.EntityAlias = "c";
		link.Columns = new ColumnSet("firstname");
		return query;
	}

	private static DataverseParityScenario<TResult> Scenario<TResult>(string marker,
		Func<IOrganizationService, TResult> act, Action<TResult, TResult>? assertEquivalent = null) => new()
	{
		Name = nameof(P1CloseoutParityTests),
		ArrangeLive = context =>
		{
			var rows = new List<Entity>();
			foreach (var name in new[] { "Zulu", "Alpha" })
			{
				var contact = new Entity("contact") { ["firstname"] = "Alpha", ["lastname"] = name };
				contact.Id = context.Service.Create(contact);
				context.Cleanup.TrackForDelete("contact", contact.Id);
				contact["contactid"] = contact.Id;
				var account = new Entity("account")
				{
					["name"] = marker, ["accountnumber"] = name, ["primarycontactid"] = contact.ToEntityReference()
				};
				account.Id = context.Service.Create(account);
				context.Cleanup.TrackForDelete("account", account.Id);
				account["accountid"] = account.Id;
				rows.Add(contact);
				rows.Add(account);
			}

			context.State.Set("rows", rows);
		},
		ArrangeSimulated = context =>
		{
			foreach (var row in context.State.Get<List<Entity>>("rows")) context.Simulation.Data().Add(row);
		},
		Act = act,
		AssertEquivalent = assertEquivalent ?? ((live, simulated) => simulated.Should().BeEquivalentTo(live))
	};

	private sealed record QueryOutcome(string[] Numbers, Exception? Error);
}