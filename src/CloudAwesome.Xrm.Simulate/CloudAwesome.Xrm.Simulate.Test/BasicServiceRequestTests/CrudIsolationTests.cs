using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.BasicServiceRequestTests;

[TestFixture]
public sealed class CrudIsolationTests
{
	[Test]
	public void Successful_Writes_Isolate_Caller_And_Stored_Graphs(
		[Values("Create", "Update")] string message,
		[Values(false, true)] bool request,
		[Values(false, true)] bool earlyBound)
	{
		var service = Service();
		var input = Payload(earlyBound);
		if (message == "Update")
		{
			var original = Payload(earlyBound, input.Id);
			original["untouched"] = "keep";
			service.Simulated().Data().Add(original);
		}

		Write(service, input, message, request);
		var stored = service.Simulated().Data().Get("contact", input.Id);
		stored.GetType().Should().Be(input.GetType());
		stored.Should().NotBeSameAs(input);
		input.Contains("modifiedon").Should().BeFalse();
		input.Contains("ownerid").Should().BeFalse();
		AssertGraph(stored);
		var audits = service.Simulated().Audit().Get().ToArray();
		audits.Should().ContainSingle().Which.Message.Should().Be(message);

		MutateGraph(input);

		AssertGraph(stored);
		if (message == "Update") stored["untouched"].Should().Be("keep");
		service.Simulated().Audit().Get().Should().Equal(audits);
	}

	[Test]
	public void Retrieve_Isolates_All_And_Selected_Results(
		[Values(false, true)] bool request,
		[Values(false, true)] bool allColumns,
		[Values(false, true)] bool earlyBound)
	{
		var service = Service();
		var stored = Payload(earlyBound);
		stored["hidden"] = "keep";
		service.Simulated().Data().Add(stored);
		var columns = allColumns ? new ColumnSet(true) : new ColumnSet(GraphColumns);

		var result = Read(service, stored.Id, columns, request);
		result.Should().NotBeSameAs(stored);
		result.Id.Should().Be(stored.Id);
		result.Contains("hidden").Should().Be(allColumns);
		AssertGraph(result);
		var audits = service.Simulated().Audit().Get().ToArray();
		audits.Should().ContainSingle().Which.Message.Should().Be("Retrieve");

		MutateGraph(result);

		AssertGraph(stored);
		service.Simulated().Audit().Get().Should().Equal(audits);
		AssertGraph(Read(service, stored.Id, columns, request));
		// Isolation works in both directions, including between independent responses.
		var second = Read(service, stored.Id, columns, request);
		MutateGraph(stored);
		AssertGraph(second);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void Create_Does_Not_Assign_Generated_Fields_To_Caller(bool request)
	{
		var service = Service();
		var input = new Entity("contact") { ["lastname"] = "Before" };
		var id = Write(service, input, "Create", request);
		id.Should().NotBeEmpty();
		input.Id.Should().BeEmpty();
		input.Attributes.Keys.Should().Equal("lastname");
		service.Simulated().Data().Get("contact", id)["contactid"].Should().Be(id);
	}

	[Test]
	public void Failed_Writes_Do_Not_Mutate_Caller_Store_Or_Audit(
		[Values("Create", "Update")] string message,
		[Values(false, true)] bool request,
		[Values("forced", "owner", "processor", "identity")]
		string failure)
	{
		var processor = new MutatingProcessor();
		var options = new SimulatorOptions();
		if (failure == "forced")
			options.FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [new RequestFailureSetting(message)]
			};
		if (failure == "processor")
			options.EntityProcessors = new Dictionary<ProcessorType, IEntityProcessor>
			{
				[new("contact", message == "Create" ? ProcessorMessage.Create : ProcessorMessage.Update)] = processor
			};
		var service = Service(options);
		var input = Payload(false);
		var original = Payload(false, input.Id);
		if (message == "Update" && failure != "identity" || message == "Create" && failure == "identity")
			service.Simulated().Data().Add(original);
		if (failure == "owner") input["ownerid"] = new EntityReference("account", Guid.NewGuid());
		var keys = input.Attributes.Keys.ToArray();

		Action act = () => Write(service, input, message, request);
		act.Should().Throw<Exception>();

		AssertGraph(input);
		input.Attributes.Keys.Should().BeEquivalentTo(keys);
		AssertGraph(original);
		service.Simulated().Audit().Get().Should().BeEmpty();
		var count = service.Simulated().Data().Get("contact").Count;
		count.Should().Be(message == "Update" && failure != "identity" || message == "Create" && failure == "identity"
			? 1
			: 0);
		MutateGraph(input);
		AssertGraph(original);
	}

	[Test]
	public void Denied_Crud_Does_Not_Mutate_Caller_Store_Or_Audit(
		[Values("Create", "Update", "Retrieve")]
		string message,
		[Values(false, true)] bool request)
	{
		var user = new Entity("systemuser", Guid.NewGuid());
		var service = Service(new SimulatorOptions
		{
			AuthenticatedUser = user,
			SimulatedSecurityModel = new SimulatedSecurityModel { IgnoreMissingEntities = false }
		});
		var input = Payload(false);
		var stored = Payload(false, input.Id);
		stored["ownerid"] = new EntityReference("systemuser", Guid.NewGuid());
		service.Simulated().Data().Add(stored);

		Action act = () =>
		{
			if (message == "Retrieve") Read(service, input.Id, new ColumnSet(true), request);
			else Write(service, input, message, request);
		};
		act.Should().Throw<FaultException<OrganizationServiceFault>>();
		AssertGraph(input);
		MutateGraph(input);
		AssertGraph(stored);
		service.Simulated().Audit().Get().Should().BeEmpty();
	}

	[Test]
	public void Processor_Retained_Values_Do_Not_Alias_Committed_Data(
		[Values("Create", "Update")] string message)
	{
		var processor = new MutatingProcessor { Throw = false };
		var service = Service(new SimulatorOptions
		{
			EntityProcessors = new Dictionary<ProcessorType, IEntityProcessor>
			{
				[new("contact", message == "Create" ? ProcessorMessage.Create : ProcessorMessage.Update)] = processor
			}
		});
		var input = Payload(false);
		if (message == "Update") service.Simulated().Data().Add(Payload(false, input.Id));
		Write(service, input, message, false);
		AssertGraph(input);
		var stored = service.Simulated().Data().Get("contact", input.Id);
		stored.GetAttributeValue<Money>("revenue").Value.Should().Be(20);
		processor.Retained!.GetAttributeValue<Money>("revenue").Value = 99;
		stored.GetAttributeValue<Money>("revenue").Value.Should().Be(20);
		service.Simulated().Audit().Get().Should().ContainSingle();
	}

	[Test]
	public void Raw_Fixture_Access_Remains_Explicitly_Mutable()
	{
		var service = Service();
		var fixture = Payload(false);
		service.Simulated().Data().Add(fixture);
		service.Simulated().Data().Get("contact", fixture.Id).Should().BeSameAs(fixture);
		MutateGraph(fixture);
		service.Simulated().Data().Get("contact", fixture.Id)["lastname"].Should().Be("Changed");
		service.Simulated().Audit().Get().Should().BeEmpty();
	}

	[Test]
	public void Failed_Retrieves_Leave_Data_And_Audits_Untouched(
		[Values(false, true)] bool request,
		[Values(false, true)] bool forced)
	{
		var options = new SimulatorOptions();
		if (forced)
			options.FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [new RequestFailureSetting("Retrieve")]
			};
		var service = Service(options);
		var original = Payload(false);
		service.Simulated().Data().Add(original);
		Action act = () => Read(service, forced ? original.Id : Guid.NewGuid(), new ColumnSet(true), request);
		act.Should().Throw<Exception>();
		AssertGraph(original);
		service.Simulated().Audit().Get().Should().BeEmpty();
	}

	[Test]
	public void Service_Cloning_Preserves_Shared_And_Cyclic_Entity_Graphs()
	{
		var service = Service();
		var input = new Entity("contact", Guid.NewGuid());
		var shared = new Money(10);
		input["one"] = shared;
		input["two"] = shared;
		input["self"] = input;
		input.KeyAttributes["key"] = shared;
		service.Create(input);
		var stored = service.Simulated().Data().Get("contact", input.Id);
		stored["self"].Should().BeSameAs(stored);
		stored["one"].Should().BeSameAs(stored["two"]).And.NotBeSameAs(shared);
		stored.KeyAttributes["key"].Should().BeSameAs(stored["one"]);
		var read = service.Retrieve("contact", input.Id, new ColumnSet(true));
		read["self"].Should().BeSameAs(read);
		read["one"].Should().BeSameAs(read["two"]).And.NotBeSameAs(stored["one"]);
		shared.Value = 99;
		((Money)stored["one"]).Value.Should().Be(10);
		((Money)read["one"]).Value.Should().Be(10);
	}

	private static readonly string[] GraphColumns =
		["lastname", "revenue", "choice", "choices", "lookup", "bytes", "child", "children", "aliased"];

	private static Entity Payload(bool earlyBound, Guid? id = null)
	{
		Entity entity = earlyBound ? new Contact() : new Entity("contact");
		entity.Id = id ?? Guid.NewGuid();
		entity["lastname"] = "Before";
		entity["revenue"] = new Money(10);
		entity["choice"] = new OptionSetValue(1);
		entity["choices"] = new OptionSetValueCollection { new(1), new(2) };
		entity["lookup"] = new EntityReference("account", Guid.Parse("11111111-1111-1111-1111-111111111111"))
		{
			Name = "Before", KeyAttributes = { ["key"] = new Money(10) }
		};
		entity["bytes"] = new byte[] { 1, 2 };
		entity["child"] = new Entity("contact") { ["revenue"] = new Money(10) };
		entity["children"] = new EntityCollection([new Entity("contact") { ["revenue"] = new Money(10) }]);
		entity["aliased"] = new AliasedValue("account", "revenue", new Money(10));
		entity.KeyAttributes["key"] = new Money(10);
		entity.FormattedValues["revenue"] = "Ten";
		entity.RelatedEntities[new Relationship("related")] =
			new EntityCollection([new Entity("contact") { ["revenue"] = new Money(10) }]);
		return entity;
	}

	private static void MutateGraph(Entity entity)
	{
		entity["lastname"] = "Changed";
		entity.GetAttributeValue<Money>("revenue").Value = 99;
		entity.GetAttributeValue<OptionSetValue>("choice").Value = 99;
		var choices = entity.GetAttributeValue<OptionSetValueCollection>("choices");
		choices[0].Value = 99;
		choices.Add(new OptionSetValue(99));
		var lookup = entity.GetAttributeValue<EntityReference>("lookup");
		lookup.Id = Guid.NewGuid();
		lookup.Name = "Changed";
		((Money)lookup.KeyAttributes["key"]).Value = 99;
		entity.GetAttributeValue<byte[]>("bytes")[0] = 99;
		entity.GetAttributeValue<Entity>("child").GetAttributeValue<Money>("revenue").Value = 99;
		var children = entity.GetAttributeValue<EntityCollection>("children");
		children.Entities[0].GetAttributeValue<Money>("revenue").Value = 99;
		children.Entities.Clear();
		((Money)entity.GetAttributeValue<AliasedValue>("aliased").Value).Value = 99;
		if (entity.KeyAttributes.TryGetValue("key", out var key)) ((Money)key).Value = 99;
		entity.FormattedValues["revenue"] = "Changed";
		foreach (var related in entity.RelatedEntities.Values)
			related.Entities[0].GetAttributeValue<Money>("revenue").Value = 99;
	}

	private static void AssertGraph(Entity entity)
	{
		entity["lastname"].Should().Be("Before");
		entity.GetAttributeValue<Money>("revenue").Value.Should().Be(10);
		entity.GetAttributeValue<OptionSetValue>("choice").Value.Should().Be(1);
		entity.GetAttributeValue<OptionSetValueCollection>("choices").Select(v => v.Value).Should().Equal(1, 2);
		var lookup = entity.GetAttributeValue<EntityReference>("lookup");
		lookup.Id.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
		lookup.Name.Should().Be("Before");
		((Money)lookup.KeyAttributes["key"]).Value.Should().Be(10);
		entity.GetAttributeValue<byte[]>("bytes").Should().Equal(1, 2);
		entity.GetAttributeValue<Entity>("child").GetAttributeValue<Money>("revenue").Value.Should().Be(10);
		entity.GetAttributeValue<EntityCollection>("children").Entities.Should().ContainSingle()
			.Which.GetAttributeValue<Money>("revenue").Value.Should().Be(10);
		((Money)entity.GetAttributeValue<AliasedValue>("aliased").Value).Value.Should().Be(10);
		if (entity.KeyAttributes.TryGetValue("key", out var key)) ((Money)key).Value.Should().Be(10);
		if (entity.FormattedValues.TryGetValue("revenue", out var formatted)) formatted.Should().Be("Ten");
		foreach (var related in entity.RelatedEntities.Values)
			related.Entities[0].GetAttributeValue<Money>("revenue").Value.Should().Be(10);
	}

	private static IOrganizationService Service(SimulatorOptions? options = null)
		=> ((IOrganizationService)null!).Simulate(options);

	private static Guid Write(IOrganizationService service, Entity entity, string message, bool request)
	{
		if (message == "Create")
			return request
				? ((CreateResponse)service.Execute(new CreateRequest { Target = entity })).id
				: service.Create(entity);
		if (request) service.Execute(new UpdateRequest { Target = entity });
		else service.Update(entity);
		return entity.Id;
	}

	private static Entity Read(IOrganizationService service, Guid id, ColumnSet columns, bool request)
		=> request
			? ((RetrieveResponse)service.Execute(new RetrieveRequest
				{ Target = new EntityReference("contact", id), ColumnSet = columns })).Entity
			: service.Retrieve("contact", id, columns);

	private sealed class MutatingProcessor : IEntityProcessor
	{
		internal bool Throw { get; init; } = true;
		internal Entity? Retained { get; private set; }

		public Entity Process(Entity entity)
		{
			Retained = entity;
			entity.GetAttributeValue<Money>("revenue").Value = 20;
			if (Throw) throw new InvalidOperationException("Processor failure");
			return entity;
		}
	}
}