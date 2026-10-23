using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.OrganizationRequestsTests;

[TestFixture]
public sealed class UpsertConsistencyTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Primary_Id_And_Primary_Key_Upserts_Return_An_Isolated_Reference(bool exists, bool keyAttribute)
    {
        var service = Create();
        var id = Guid.NewGuid();
        if (exists) service.Simulated().Data().Add(new Entity("account", id) { ["description"] = "preserved" });
        var target = keyAttribute ? new Entity("account", "accountid", id) : new Entity("account", id);
        target["name"] = "upserted";
        var response = Upsert(service, target);
        response.RecordCreated.Should().Be(!exists);
        response.ResponseName.Should().Be("Upsert");
        response.Results.Keys.Should().BeEquivalentTo("RecordCreated", "Target");
        response.Target.Id.Should().Be(id);
        response.Target.KeyAttributes.Should().BeEmpty();
        response.Target.Name.Should().BeNull();
        target.Id.Should().Be(keyAttribute ? Guid.Empty : id);
        target.Attributes.Keys.Should().Equal("name");
        target.KeyAttributes.Should().HaveCount(keyAttribute ? 1 : 0);
        var stored = service.Simulated().Data().Get("account", id);
        stored.GetAttributeValue<string>("name").Should().Be("upserted");
        if (exists) stored.GetAttributeValue<string>("description").Should().Be("preserved");
        stored.KeyAttributes.Should().BeEmpty();
        response.Target.Id = Guid.NewGuid();
        stored.Id.Should().Be(id);
        service.Simulated().Audit().Get().Select(audit => audit.Message).Should().Equal("Upsert");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Declared_Key_Update_Strips_Key_Columns_And_Preserves_Request(bool conflict)
    {
        var service = Create();
        var id = Guid.NewGuid();
        service.Simulated().Data().Add(new Entity("account", id) { ["accountnumber"] = "A1", ["name"] = "before" });
        var target = new Entity("account", "accountnumber", "A1") { ["accountnumber"] = conflict ? "A2" : "A1", ["name"] = "after" };
        var response = Upsert(service, target);
        response.RecordCreated.Should().BeFalse();
        response.Target.Id.Should().Be(id);
        service.Simulated().Data().Get("account", id).GetAttributeValue<string>("accountnumber").Should().Be("A1");
        service.Simulated().Data().Get("account", id).GetAttributeValue<string>("name").Should().Be("after");
        target.Id.Should().BeEmpty();
        target.GetAttributeValue<string>("accountnumber").Should().Be(conflict ? "A2" : "A1");
        target.KeyAttributes["accountnumber"].Should().Be("A1");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Create_Copies_Only_Missing_Key_Attributes(bool conflictingBody)
    {
        var service = Create();
        var target = new Entity("account", "accountnumber", "A1") { ["name"] = "created" };
        if (conflictingBody) target["accountnumber"] = "A2";
        var response = Upsert(service, target);
        response.RecordCreated.Should().BeTrue();
        var stored = service.Simulated().Data().Get("account", response.Target.Id);
        stored.GetAttributeValue<string>("accountnumber").Should().Be(conflictingBody ? "A2" : "A1");
        target.Contains("accountnumber").Should().Be(conflictingBody);
        target.Id.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Composite_Key_Order_And_Lookup_Display_Name_Do_Not_Affect_Matching(bool reverse)
    {
        var service = Create();
        var parent = Guid.NewGuid();
        var id = Guid.NewGuid();
        service.Simulated().Data().Add(new Entity("account", id) { ["code"] = "C1", ["parentid"] = new EntityReference("account", parent) { Name = "display" } });
        var keys = reverse
            ? new KeyAttributeCollection { ["parentid"] = new EntityReference("account", parent), ["code"] = "C1" }
            : new KeyAttributeCollection { ["code"] = "C1", ["parentid"] = new EntityReference("account", parent) };
        Upsert(service, new Entity("account", keys) { ["name"] = "changed" }).Target.Id.Should().Be(id);
        service.Simulated().Data().Get("account").Should().ContainSingle();
    }

    [TestCase("name")]
    [TestCase("code")]
    [TestCase("description")]
    public void Matching_Undeclared_Or_Partial_Keys_Fault_Without_Writing(string key)
    {
        var service = Create();
        var id = Guid.NewGuid();
        var valueColumn = key == "name" ? "description" : "name";
        service.Simulated().Data().Add(new Entity("account", id) { [key] = "match", [valueColumn] = "before" });
        var target = new Entity("account", key, "match") { [valueColumn] = "after" };
        Action act = () => Upsert(service, target);
        act.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(-2147088247);
        service.Simulated().Data().Get("account", id).GetAttributeValue<string>(valueColumn).Should().Be("before");
        service.Simulated().Audit().Get().Should().BeEmpty();
        target.Id.Should().BeEmpty();
    }

    [Test]
    public void Unmatched_Undeclared_Key_Flows_To_Create_As_Verified_Live()
    {
        var service = Create();
        var response = Upsert(service, new Entity("account", "name", "unmatched"));
        response.RecordCreated.Should().BeTrue();
        service.Simulated().Data().Get("account", response.Target.Id).GetAttributeValue<string>("name").Should().Be("unmatched");
    }

    [Test]
    public void Metadata_Rejects_Unknown_Key_Columns_Even_Without_Matches()
    {
        var service = Create();
        Action act = () => Upsert(service, new Entity("account", "unknown", "key"));
        act.Should().Throw<SimulatedMetadataException>();
        service.Simulated().Data().Get("account").Should().BeEmpty();
        service.Simulated().Audit().Get().Should().BeEmpty();
    }

    [Test]
    public void Primary_Id_Takes_Precedence_Over_Alternate_Key_Values()
    {
        var service = Create();
        var id = Guid.NewGuid();
        service.Simulated().Data().Add(new Entity("account", id) { ["accountnumber"] = "A1" });
        service.Simulated().Data().Add(new Entity("account", Guid.NewGuid()) { ["accountnumber"] = "A2" });
        var target = new Entity("account", "accountnumber", "A2") { Id = id, ["name"] = "id wins" };
        Upsert(service, target).Target.Id.Should().Be(id);
        service.Simulated().Data().Get("account", id).GetAttributeValue<string>("accountnumber").Should().Be("A1");
    }

    [Test]
    public void Ambiguous_Seeded_Keys_Produce_A_Service_Fault_Without_A_Partial_Update()
    {
        var service = Create();
        foreach (var id in new[] { Guid.NewGuid(), Guid.NewGuid() }) service.Simulated().Data().Add(new Entity("account", id) { ["accountnumber"] = "A1" });
        Action act = () => Upsert(service, new Entity("account", "accountnumber", "A1") { ["name"] = "must fail" });
        act.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(-2147220937);
        service.Simulated().Data().Get("account").Should().OnlyContain(row => !row.Contains("name"));
        service.Simulated().Audit().Get().Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Upsert_Uses_Create_Or_Update_Processor_Once_With_Isolated_Payload(bool exists)
    {
        var processor = new RecordingProcessor();
        var options = new SimulatorOptions { EntityProcessors = new Dictionary<ProcessorType, IEntityProcessor> { [new ProcessorType("account", exists ? ProcessorMessage.Update : ProcessorMessage.Create)] = processor } };
        var service = ((IOrganizationService)null!).Simulate(options);
        var id = Guid.NewGuid();
        if (exists) service.Simulated().Data().Add(new Entity("account", id));
        var target = new Entity("account", id) { ["name"] = "original" };
        Upsert(service, target);
        processor.Calls.Should().Be(1);
        target.GetAttributeValue<string>("name").Should().Be("original");
        service.Simulated().Data().Get("account", id).GetAttributeValue<string>("name").Should().Be("processed");
        service.Simulated().Audit().Get().Select(audit => audit.Message).Should().Equal("Upsert");
    }

    [Test]
    public void Create_Validation_Failure_Does_Not_Populate_Request_Id_Or_Audit()
    {
        var service = Create();
        var target = new Entity("account", "accountnumber", new string('x', 21));
        Action act = () => Upsert(service, target);
        act.Should().Throw<SimulatedMetadataException>();
        target.Id.Should().BeEmpty();
        target.Attributes.Should().BeEmpty();
        service.Simulated().Data().Get("account").Should().BeEmpty();
        service.Simulated().Audit().Get().Should().BeEmpty();
    }

    private sealed class RecordingProcessor : IEntityProcessor
    {
        public int Calls { get; private set; }
        public Entity Process(Entity entity) { Calls++; entity["name"] = "processed"; return entity; }
    }
    private static IOrganizationService Create() => ((IOrganizationService)null!).Simulate(new SimulatorOptions { Metadata = SimulatedMetadata.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "mutation-metadata.json")) });
    private static UpsertResponse Upsert(IOrganizationService service, Entity target) => (UpsertResponse)service.Execute(new UpsertRequest { Target = target });
}
