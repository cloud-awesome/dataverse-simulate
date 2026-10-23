using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Queues;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test;

[TestFixture]
public sealed class QueueMutationConsistencyTests
{
    private static IEnumerable<TestCaseData> MoveCases()
    {
        foreach (var sdk in new[] { false, true })
        foreach (var kind in new[] { "implicit", "explicit", "same", "repeat", "properties", "empty-source" })
            yield return new TestCaseData(sdk, kind);
    }

    [TestCaseSource(nameof(MoveCases))]
    public void Queue_Moves_Preserve_Identity_And_Audit_One_Operation(bool sdk, string kind)
    {
        var f = Fixture();
        var props = kind == "properties" ? new Entity("queueitem", Guid.NewGuid())
        {
            ["queueid"] = new EntityReference("queue", f.Source),
            ["objectid"] = new EntityReference("contact", Guid.NewGuid()),
            ["workerid"] = f.Service.Simulated().Data().AuthenticatedUser,
            ["title"] = "updated"
        } : null;
        var destination = kind is "same" or "repeat" ? f.Source : f.Destination;
        var source = kind is "explicit" or "same" ? f.Source : kind == "empty-source" ? Guid.Empty : (Guid?)null;
        var result = Add(f, sdk, destination, source, props);
        result.Should().Be(f.Item);
        var stored = f.Service.Simulated().Data().Get("queueitem").Should().ContainSingle().Subject;
        stored.Id.Should().Be(f.Item);
        stored.GetAttributeValue<EntityReference>("queueid").Id.Should().Be(destination);
        stored.GetAttributeValue<EntityReference>("objectid").Id.Should().Be(f.Target);
        if (props is not null)
        {
            stored.GetAttributeValue<string>("title").Should().Be("updated");
            props.GetAttributeValue<EntityReference>("queueid").Id.Should().Be(f.Source);
            props.GetAttributeValue<EntityReference>("objectid").LogicalName.Should().Be("contact");
        }
        var audit = f.Service.Simulated().Audit().Get().Should().ContainSingle().Subject;
        audit.Message.Should().Be("AddToQueue");
        audit.Id.Should().Be(f.Item);
    }

    private static IEnumerable<TestCaseData> FailureCases()
    {
        foreach (var sdk in new[] { false, true })
        foreach (var kind in new[] { "source-missing", "source-wrong", "destination-missing", "destination-empty", "target-missing", "properties-type", "worker-missing", "worker-empty", "worker-type", "worker-value", "enteredon-value", "route-target", "route-missing", "pick-missing", "pick-empty" })
            yield return new TestCaseData(sdk, kind);
    }

    [TestCaseSource(nameof(FailureCases))]
    public void Failed_Queue_Operations_Leave_Source_And_Audits_Untouched(bool sdk, string kind)
    {
        var f = Fixture();
        var missing = Guid.NewGuid();
        var storedBefore = f.Service.Simulated().Data().Get("queueitem", f.Item);
        Action act;
        if (kind.StartsWith("route"))
        {
            var target = new EntityReference(kind == "route-target" ? "task" : "queue", kind == "route-target" ? f.Target : missing);
            act = () => { if (sdk) f.Service.Execute(new RouteToRequest { QueueItemId = f.Item, Target = target }); else f.Service.Simulated().Queues().RouteTo(f.Item, target); };
        }
        else if (kind.StartsWith("pick"))
        {
            var worker = kind == "pick-empty" ? Guid.Empty : missing;
            act = () => { if (sdk) f.Service.Execute(new PickFromQueueRequest { QueueItemId = f.Item, WorkerId = worker }); else f.Service.Simulated().Queues().PickFromQueue(f.Item, worker, false); };
        }
        else
        {
            Entity? props = kind switch
            {
                "properties-type" => new Entity("task"),
                "worker-missing" => new Entity("queueitem") { ["workerid"] = new EntityReference("systemuser", missing) },
                "worker-empty" => new Entity("queueitem") { ["workerid"] = new EntityReference("systemuser", Guid.Empty) },
                "worker-type" => new Entity("queueitem") { ["workerid"] = new EntityReference("task", f.Target) },
                "worker-value" => new Entity("queueitem") { ["workerid"] = "invalid" },
                "enteredon-value" => new Entity("queueitem") { ["enteredon"] = "invalid" },
                _ => null
            };
            var destination = kind == "destination-missing" ? missing : kind == "destination-empty" ? Guid.Empty : f.Destination;
            var source = kind == "source-missing" ? missing : kind == "source-wrong" ? f.Destination : f.Source;
            if (kind == "target-missing") f = f with { Target = missing };
            act = () => Add(f, sdk, destination, source, props);
        }
        if (kind is "properties-type" or "worker-type" or "worker-value" or "enteredon-value" or "route-target")
            act.Should().Throw<SimulatedQueueException>();
        else
            act.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(
                kind is "source-missing" or "source-wrong" ? -2147220188 : kind is "worker-missing" or "pick-missing" or "worker-empty" or "pick-empty" or "destination-empty" ? -2147220989 : -2147220969);
        var stored = f.Service.Simulated().Data().Get("queueitem").Should().ContainSingle().Subject;
        stored.Should().BeSameAs(storedBefore);
        stored.GetAttributeValue<EntityReference>("queueid").Id.Should().Be(f.Source);
        stored.GetAttributeValue<string>("title").Should().Be("original");
        stored.Contains("workerid").Should().BeFalse();
        f.Service.Simulated().Audit().Get().Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Pick_With_Removal_Requires_Delete_And_Preserves_Item_On_Denial(bool allowDelete)
    {
        var unit = Guid.NewGuid();
        var user = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create().WithBusinessUnit(unit, "Root").WithUser(user, unit)
            .WithRole("Queue", role => { role.CanWrite("queueitem", PrivilegeDepthEnum.Organization); if (allowDelete) role.CanDelete("queueitem", PrivilegeDepthEnum.Organization); })
            .AssignRoleToUser("Queue", user);
        var f = Fixture(new SimulatorOptions { AuthenticatedUser = new Entity("systemuser", user), SimulatedSecurityModel = model });
        Action act = () => f.Service.Simulated().Queues().PickFromQueue(f.Item, user, true);
        if (allowDelete)
        {
            act();
            f.Service.Simulated().Data().Get("queueitem").Should().BeEmpty();
            f.Service.Simulated().Audit().Get().Should().ContainSingle(audit => audit.Message == "PickFromQueue");
        }
        else
        {
            act.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(-2147187962);
            f.Service.Simulated().Data().Get("queueitem").Should().ContainSingle();
            f.Service.Simulated().Audit().Get().Should().BeEmpty();
        }
    }

    [TestCase("AddToQueue", false)]
    [TestCase("AddToQueue", true)]
    [TestCase("RemoveFromQueue", false)]
    [TestCase("RemoveFromQueue", true)]
    [TestCase("PickFromQueue", false)]
    [TestCase("PickFromQueue", true)]
    [TestCase("ReleaseToQueue", false)]
    [TestCase("ReleaseToQueue", true)]
    [TestCase("RouteTo", false)]
    [TestCase("RouteTo", true)]
    public void Failure_Injection_Applies_Equally_To_Facade_And_Sdk(string operation, bool sdk)
    {
        var f = Fixture(new SimulatorOptions { FakeServiceFailureSettings = new FakeServiceFailureSettings { RequestFailureSettings = [new RequestFailureSetting(operation) { Exception = new InvalidOperationException("injected") }] } });
        var worker = f.Service.Simulated().Data().AuthenticatedUser;
        Action act = operation switch
        {
            "AddToQueue" => () => Add(f, sdk, f.Destination),
            "RemoveFromQueue" => () => { if (sdk) f.Service.Execute(new RemoveFromQueueRequest { QueueItemId = f.Item }); else f.Service.Simulated().Queues().RemoveFromQueue(f.Item); },
            "PickFromQueue" => () => { if (sdk) f.Service.Execute(new PickFromQueueRequest { QueueItemId = f.Item, WorkerId = worker.Id }); else f.Service.Simulated().Queues().PickFromQueue(f.Item, worker, false); },
            "ReleaseToQueue" => () => { if (sdk) f.Service.Execute(new ReleaseToQueueRequest { QueueItemId = f.Item }); else f.Service.Simulated().Queues().ReleaseToQueue(f.Item); },
            _ => () => { if (sdk) f.Service.Execute(new RouteToRequest { QueueItemId = f.Item, Target = new EntityReference("queue", f.Destination) }); else f.Service.Simulated().Queues().RouteTo(f.Item, new EntityReference("queue", f.Destination)); }
        };
        act.Should().Throw<InvalidOperationException>().WithMessage("injected");
        f.Service.Simulated().Data().Get("queueitem", f.Item).GetAttributeValue<EntityReference>("queueid").Id.Should().Be(f.Source);
        f.Service.Simulated().Audit().Get().Should().BeEmpty();
    }

    [TestCase("duplicate")]
    [TestCase("conflicting-id")]
    [TestCase("invalid-id")]
    public void New_Item_Identity_Validation_Does_Not_Change_Other_Items(string kind)
    {
        var f = Fixture();
        var target = new Entity("task", Guid.NewGuid());
        f.Service.Simulated().Data().Add(target);
        var props = new Entity("queueitem", kind == "invalid-id" ? Guid.Empty : f.Item);
        if (kind == "conflicting-id") props["queueitemid"] = Guid.NewGuid();
        if (kind == "invalid-id") props["queueitemid"] = "invalid";
        Action act = () => f.Service.Simulated().Queues().AddToQueue(target.ToEntityReference(), f.Destination, queueItemProperties: props);
        act.Should().Throw<Exception>();
        f.Service.Simulated().Data().Get("queueitem").Should().ContainSingle(row => row.Id == f.Item);
        f.Service.Simulated().Audit().Get().Should().BeEmpty();
    }

    [Test]
    public void Successful_Queue_Inputs_Are_Cloned()
    {
        var f = Fixture();
        var worker = f.Service.Simulated().Data().AuthenticatedUser;
        f.Service.Simulated().Queues().PickFromQueue(f.Item, worker, false);
        var originalWorkerId = worker.Id;
        worker.Id = Guid.NewGuid();
        f.Service.Simulated().Data().Get("queueitem", f.Item).GetAttributeValue<EntityReference>("workerid").Id.Should().Be(originalWorkerId);
        var destination = new EntityReference("queue", f.Destination);
        f.Service.Simulated().Queues().RouteTo(f.Item, destination);
        destination.Id = Guid.NewGuid();
        f.Service.Simulated().Data().Get("queueitem", f.Item).GetAttributeValue<EntityReference>("queueid").Id.Should().Be(f.Destination);
    }

    [Test]
    public void Failed_Transaction_Restores_Queue_Move_And_Operation_Audits()
    {
        var f = Fixture();
        Action act = () => f.Service.Execute(new ExecuteTransactionRequest { Requests = [new AddToQueueRequest { Target = new EntityReference("task", f.Target), DestinationQueueId = f.Destination, SourceQueueId = f.Source }, new UpdateRequest { Target = new Entity("account", Guid.NewGuid()) }] });
        act.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.Should().BeOfType<ExecuteTransactionFault>().Which.FaultedRequestIndex.Should().Be(1);
        f.Service.Simulated().Data().Get("queueitem", f.Item).GetAttributeValue<EntityReference>("queueid").Id.Should().Be(f.Source);
        f.Service.Simulated().Audit().Get().Should().BeEmpty();
    }

    private static Guid Add(QueueFixture f, bool sdk, Guid destination, Guid? source = null, Entity? props = null)
        => sdk ? ((AddToQueueResponse)f.Service.Execute(new AddToQueueRequest { Target = new EntityReference("task", f.Target), DestinationQueueId = destination, SourceQueueId = source ?? Guid.Empty, QueueItemProperties = props })).QueueItemId
            : f.Service.Simulated().Queues().AddToQueue(new EntityReference("task", f.Target), destination, source, props);

    private static QueueFixture Fixture(SimulatorOptions? options = null)
    {
        var service = ((IOrganizationService)null!).Simulate(options);
        var source = Guid.NewGuid(); var destination = Guid.NewGuid(); var target = Guid.NewGuid(); var item = Guid.NewGuid();
        service.Simulated().Data().Add(new Entity("queue", source));
        service.Simulated().Data().Add(new Entity("queue", destination));
        service.Simulated().Data().Add(new Entity("task", target));
        service.Simulated().Data().Add(new Entity("queueitem", item) { ["queueid"] = new EntityReference("queue", source), ["objectid"] = new EntityReference("task", target), ["title"] = "original" });
        return new QueueFixture(service, source, destination, target, item);
    }
    private sealed record QueueFixture(IOrganizationService Service, Guid Source, Guid Destination, Guid Target, Guid Item);
}
