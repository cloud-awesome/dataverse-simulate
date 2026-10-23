using System;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture, Category("Parity"), Category("MutationConsistency"), Category("Queues")]
public sealed class QueueMutationParityTests : IntegrationBaseFixture
{
    [TestCase("repeat", false)]
    [TestCase("implicit", false)]
    [TestCase("explicit", false)]
    [TestCase("same", false)]
    [TestCase("wrong-source", false)]
    [TestCase("missing-source", false)]
    [TestCase("conflict-properties", false)]
    [TestCase("move-id", false)]
    [TestCase("implicit", true)]
    [TestCase("explicit", true)]
    [TestCase("repeat", true)]
    [TestCase("release", false)]
    [TestCase("release", true)]
    [TestCase("pick", false)]
    [TestCase("pick", true)]
    [TestCase("pick-empty", false)]
    [TestCase("pick-remove-empty", false)]
    [TestCase("route-queue", true)]
    [TestCase("route-user", false)]
    [TestCase("properties-worker", false)]
    [TestCase("pick-missing", false)]
    [TestCase("release-missing", false)]
    [TestCase("remove-missing", false)]
    [TestCase("route-missing", false)]
    [TestCase("destination-missing", false)]
    [TestCase("target-missing", false)]
    [TestCase("destination-empty", false)]
    public void Queue_Mutations_Response_Identity_And_State_Match(string operation, bool picked)
    {
        var task = Guid.NewGuid();
        var q1 = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var item = Guid.Empty;
        var user = Guid.Empty;
        Entity? seed = null;
        DataverseParityHarness.Execute(new DataverseParityScenario<QueueShape>
        {
            Name = $"Queue_{operation}_picked_{picked}",
            ArrangeLive = context =>
            {
                context.Service.Create(new Entity("queue", q1) { ["name"] = "CA parity source" }); context.Cleanup.TrackForDelete("queue", q1);
                context.Service.Create(new Entity("queue", q2) { ["name"] = "CA parity destination" }); context.Cleanup.TrackForDelete("queue", q2);
                context.Service.Create(new Entity("task", task) { ["subject"] = "CA queue parity" }); context.Cleanup.TrackForDelete("task", task);
                user = ((WhoAmIResponse)context.Service.Execute(new WhoAmIRequest())).UserId;
                item = ((AddToQueueResponse)context.Service.Execute(new AddToQueueRequest { Target = new EntityReference("task", task), DestinationQueueId = q1 })).QueueItemId;
                context.Cleanup.TrackForDelete("queueitem", item);
                if (picked) context.Service.Execute(new PickFromQueueRequest { QueueItemId = item, WorkerId = user });
                seed = context.Service.Retrieve("queueitem", item, new ColumnSet("queueid", "objectid", "workerid", "enteredon"));
            },
            ArrangeSimulated = context =>
            {
                context.Simulation.Data().Add(new Entity("queue", q1));
                context.Simulation.Data().Add(new Entity("queue", q2));
                context.Simulation.Data().Add(new Entity("task", task));
                context.Simulation.Data().Add(new Entity("systemuser", user));
                context.Simulation.Data().Add(seed!);
            },
            Act = service =>
            {
                OrganizationRequest request = operation switch
                {
                    "release" => new ReleaseToQueueRequest { QueueItemId = item },
                    "pick" => new PickFromQueueRequest { QueueItemId = item, WorkerId = user },
                    "pick-empty" => new PickFromQueueRequest { QueueItemId = item, WorkerId = Guid.Empty },
                    "pick-remove-empty" => new PickFromQueueRequest { QueueItemId = item, WorkerId = Guid.Empty, RemoveQueueItem = true },
                    "pick-missing" => new PickFromQueueRequest { QueueItemId = item, WorkerId = missing },
                    "release-missing" => new ReleaseToQueueRequest { QueueItemId = missing },
                    "remove-missing" => new RemoveFromQueueRequest { QueueItemId = missing },
                    "route-missing" => new RouteToRequest { QueueItemId = item, Target = new EntityReference("queue", missing) },
                    "route-queue" => new RouteToRequest { QueueItemId = item, Target = new EntityReference("queue", q2) },
                    "route-user" => new RouteToRequest { QueueItemId = item, Target = new EntityReference("systemuser", user) },
                    _ => new AddToQueueRequest { Target = new EntityReference("task", task), DestinationQueueId = operation is "repeat" or "same" ? q1 : q2 }
                };
                if (request is AddToQueueRequest add)
                {
                    if (operation is "explicit" or "same" or "conflict-properties") add.SourceQueueId = q1;
                    if (operation == "wrong-source") add.SourceQueueId = q2;
                    if (operation == "missing-source") add.SourceQueueId = missing;
                    if (operation == "conflict-properties") add.QueueItemProperties = new Entity("queueitem") { ["queueid"] = new EntityReference("queue", q1), ["objectid"] = new EntityReference("systemuser", user) };
                    if (operation == "move-id") add.QueueItemProperties = new Entity("queueitem", missing);
                    if (operation == "properties-worker") add.QueueItemProperties = new Entity("queueitem") { ["workerid"] = new EntityReference("systemuser", user) };
                    if (operation == "destination-missing") add.DestinationQueueId = missing;
                    if (operation == "target-missing") add.Target = new EntityReference("task", missing);
                    if (operation == "destination-empty") add.DestinationQueueId = Guid.Empty;
                }
                string? responseName = null;
                string[] keys = [];
                int? faultCode = null;
                string? faultMessage = null;
                Guid? responseItem = null;
                try
                {
                    var response = service.Execute(request);
                    responseName = response.ResponseName;
                    keys = response.Results.Keys.Order().ToArray();
                    if (response is AddToQueueResponse added) responseItem = added.QueueItemId;
                }
                catch (FaultException<OrganizationServiceFault> exception)
                {
                    faultCode = exception.Detail.ErrorCode;
                    // Missing-record message casing varies by internal platform request path.
                    if (operation is "wrong-source" or "missing-source" or "pick-empty" or "pick-remove-empty" or "pick-missing" or "destination-empty") faultMessage = exception.Detail.Message;
                }
                var rows = service.RetrieveMultiple(new QueryExpression("queueitem") { ColumnSet = new ColumnSet("queueid", "objectid", "workerid"), Criteria = new FilterExpression { Conditions = { new ConditionExpression("objectid", ConditionOperator.Equal, task) } } }).Entities;
                rows.Should().ContainSingle();
                rows[0].Id.Should().Be(item);
                return new QueueShape(responseName, keys, responseItem, faultCode, faultMessage, rows[0].Id, rows[0].GetAttributeValue<EntityReference>("queueid")?.Id, rows[0].GetAttributeValue<EntityReference>("objectid")?.Id, rows[0].GetAttributeValue<EntityReference>("workerid")?.Id);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Removing_A_Queue_Item_Leaves_The_Target_Record_Intact(bool pick)
    {
        var queue = Guid.NewGuid(); var task = Guid.NewGuid(); var item = Guid.Empty; var user = Guid.Empty;
        DataverseParityHarness.Execute(new DataverseParityScenario<RemovalShape>
        {
            Name = $"Queue_remove_pick_{pick}",
            ArrangeLive = context =>
            {
                context.Service.Create(new Entity("queue", queue) { ["name"] = "CA removal parity" }); context.Cleanup.TrackForDelete("queue", queue);
                context.Service.Create(new Entity("task", task) { ["subject"] = "CA removal target" }); context.Cleanup.TrackForDelete("task", task);
                item = ((AddToQueueResponse)context.Service.Execute(new AddToQueueRequest { DestinationQueueId = queue, Target = new EntityReference("task", task) })).QueueItemId;
                context.Cleanup.TrackForDelete("queueitem", item);
                user = ((WhoAmIResponse)context.Service.Execute(new WhoAmIRequest())).UserId;
            },
            ArrangeSimulated = context =>
            {
                context.Simulation.Data().Add(new Entity("queue", queue));
                context.Simulation.Data().Add(new Entity("task", task) { ["subject"] = "CA removal target" });
                context.Simulation.Data().Add(new Entity("systemuser", user));
                context.Simulation.Data().Add(new Entity("queueitem", item) { ["queueid"] = new EntityReference("queue", queue), ["objectid"] = new EntityReference("task", task) });
            },
            Act = service =>
            {
                OrganizationRequest request = pick ? new PickFromQueueRequest { QueueItemId = item, WorkerId = user, RemoveQueueItem = true } : new RemoveFromQueueRequest { QueueItemId = item };
                var response = service.Execute(request);
                var count = service.RetrieveMultiple(new QueryExpression("queueitem") { Criteria = new FilterExpression { Conditions = { new ConditionExpression("queueitemid", ConditionOperator.Equal, item) } } }).Entities.Count;
                count.Should().Be(0);
                var target = service.Retrieve("task", task, new ColumnSet("subject"));
                return new RemovalShape(response.ResponseName, response.Results.Keys.Order().ToArray(), count, target.GetAttributeValue<string>("subject"));
            },
            AfterLiveAct = (context, _) => context.Cleanup.MarkDeleted("queueitem", item)
        });
    }

    [Test]
    public void AddToQueue_New_Item_Response_And_Properties_Match()
    {
        var task = Guid.NewGuid();
        var queue = Guid.NewGuid();
        Guid liveItem = Guid.Empty;
        DataverseParityHarness.Execute(new DataverseParityScenario<NewItemShape>
        {
            Name = nameof(AddToQueue_New_Item_Response_And_Properties_Match),
            ArrangeLive = context =>
            {
                context.Service.Create(new Entity("queue", queue) { ["name"] = "CA parity new queue" }); context.Cleanup.TrackForDelete("queue", queue);
                context.Service.Create(new Entity("task", task) { ["subject"] = "CA parity new task" }); context.Cleanup.TrackForDelete("task", task);
            },
            ArrangeSimulated = context => { context.Simulation.Data().Add(new Entity("queue", queue)); context.Simulation.Data().Add(new Entity("task", task)); },
            Act = service =>
            {
                var response = (AddToQueueResponse)service.Execute(new AddToQueueRequest { DestinationQueueId = queue, Target = new EntityReference("task", task) });
                if (liveItem == Guid.Empty) liveItem = response.QueueItemId;
                var stored = service.Retrieve("queueitem", response.QueueItemId, new ColumnSet("queueid", "objectid", "enteredon", "createdon", "modifiedon"));
                return new NewItemShape(response.ResponseName, response.Results.Keys.Order().ToArray(), response.QueueItemId != Guid.Empty, stored.GetAttributeValue<EntityReference>("queueid").Id, stored.GetAttributeValue<EntityReference>("objectid").Id, stored.Contains("enteredon"), stored.Contains("createdon"), stored.Contains("modifiedon"));
            },
            AfterLiveAct = (context, _) => context.Cleanup.TrackForDelete("queueitem", liveItem)
        });
    }

    private sealed record QueueShape(string? ResponseName, string[] Keys, Guid? ResponseItem, int? FaultCode, string? FaultMessage, Guid Item, Guid? Queue, Guid? Object, Guid? Worker);
    private sealed record NewItemShape(string ResponseName, string[] Keys, bool NonEmptyId, Guid Queue, Guid Object, bool EnteredOn, bool CreatedOn, bool ModifiedOn);
    private sealed record RemovalShape(string ResponseName, string[] Keys, int Count, string? TargetSubject);
}
