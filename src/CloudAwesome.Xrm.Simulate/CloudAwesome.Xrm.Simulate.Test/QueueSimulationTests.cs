using System;
using System.Collections.Generic;
using System.IO;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.Queues;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test;

[TestFixture]
public class QueueSimulationTests
{
    private static string QueueMetadataPath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "queue-metadata.json");

    [Test]
    public void InitialiseQueues_AddsQueueAndQueueItemRowsToDataStore()
    {
        var queueId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var systemTime = new DateTime(2026, 9, 18, 10, 30, 0, DateTimeKind.Utc);
        IOrganizationService organizationService = null!;

        organizationService = organizationService.Simulate(new SimulatorOptions
        {
            ClockSimulator = new FixedClock(systemTime),
            InitialiseData = new Dictionary<string, List<Entity>>
            {
                ["account"] =
                [
                    new Entity("account", accountId)
                    {
                        ["name"] = "Contoso"
                    }
                ]
            },
            Queues = SimulatedQueueModel.Create()
                .WithQueue(queueId, "Support queue", isDefault: true)
                .WithQueueItem(queueItemId, queueId, new EntityReference("account", accountId))
        });

        var queue = organizationService.Simulated().Data().Get("queue", queueId);
        var queueItem = organizationService.Simulated().Data().Get("queueitem", queueItemId);

        queue.GetAttributeValue<Guid>("queueid").Should().Be(queueId);
        queue.GetAttributeValue<string>("name").Should().Be("Support queue");
        queue.GetAttributeValue<bool>("isdefault").Should().BeTrue();
        queue.GetAttributeValue<DateTime>("createdon").Should().Be(systemTime);

        queueItem.GetAttributeValue<Guid>("queueitemid").Should().Be(queueItemId);
        queueItem.GetAttributeValue<EntityReference>("queueid").Id.Should().Be(queueId);
        queueItem.GetAttributeValue<EntityReference>("objectid").Should().BeEquivalentTo(new EntityReference("account", accountId));
        queueItem.GetAttributeValue<DateTime>("enteredon").Should().Be(systemTime);
    }

    [Test]
    public void InitialiseQueues_WithoutMetadata_AcceptsAnyExistingTargetTable()
    {
        var queueId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        IOrganizationService organizationService = null!;

        organizationService = organizationService.Simulate(new SimulatorOptions
        {
            InitialiseData = new Dictionary<string, List<Entity>>
            {
                ["notarealtable"] =
                [
                    new Entity("notarealtable", targetId)
                    {
                        ["notarealcolumn"] = "allowed without metadata"
                    }
                ]
            },
            Queues = SimulatedQueueModel.Create()
                .WithQueue(queueId, "Permissive queue")
                .WithQueueItem(queueItemId, queueId, new EntityReference("notarealtable", targetId))
        });

        organizationService.Simulated().Data().Get("queueitem", queueItemId)
            .GetAttributeValue<EntityReference>("objectid")
            .LogicalName
            .Should()
            .Be("notarealtable");
    }

    [Test]
    public void InitialiseQueues_WithMetadata_RejectsTargetThatIsNotEnabledForQueues()
    {
        var queueId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        IOrganizationService organizationService = null!;

        var simulate = () => organizationService.Simulate(new SimulatorOptions
        {
            Metadata = SimulatedMetadata.Load(QueueMetadataPath),
            InitialiseData = new Dictionary<string, List<Entity>>
            {
                ["lead"] =
                [
                    new Entity("lead", leadId)
                    {
                        ["fullname"] = "Ada Lovelace"
                    }
                ]
            },
            Queues = SimulatedQueueModel.Create()
                .WithQueue(queueId, "Strict queue")
                .WithQueueItem(queueItemId, queueId, new EntityReference("lead", leadId))
        });

        simulate.Should()
            .Throw<SimulatedMetadataException>()
            .WithMessage("Entity 'lead' is not enabled for queues.");
    }

    [Test]
    public void Metadata_LoadsQueueEnabledCapability()
    {
        var metadata = SimulatedMetadata.Load(QueueMetadataPath);

        metadata.GetEntity("account").IsValidForQueue.Should().BeTrue();
        metadata.GetEntity("lead").IsValidForQueue.Should().BeFalse();
    }

    [Test]
    public void AddToQueue_CreatesQueueItemAndReturnsId()
    {
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var organizationService = CreateQueueBackedService(queueId, accountId);

        var createdQueueItemId = organizationService.Simulated().Queues().AddToQueue(
            new EntityReference("account", accountId),
            queueId,
            queueItemProperties: new Entity("queueitem", queueItemId)
            {
                ["title"] = "Queue title"
            });

        createdQueueItemId.Should().Be(queueItemId);

        var queueItem = organizationService.Simulated().Data().Get("queueitem", queueItemId);
        queueItem.GetAttributeValue<EntityReference>("queueid").Id.Should().Be(queueId);
        queueItem.GetAttributeValue<EntityReference>("objectid").Should().BeEquivalentTo(new EntityReference("account", accountId));
        queueItem.GetAttributeValue<string>("title").Should().Be("Queue title");
        organizationService.Simulated().Audit().Get("AddToQueue").Should().ContainSingle(audit => audit.Id == queueItemId);
    }

    [Test]
    public void AddToQueue_WithSourceQueue_RemovesExistingSourceQueueItem()
    {
        var sourceQueueId = Guid.NewGuid();
        var destinationQueueId = Guid.NewGuid();
        var oldQueueItemId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        IOrganizationService organizationService = null!;

        organizationService = organizationService.Simulate(new SimulatorOptions
        {
            InitialiseData = CreateAccountData(accountId),
            Queues = SimulatedQueueModel.Create()
                .WithQueue(sourceQueueId, "Source")
                .WithQueue(destinationQueueId, "Destination")
                .WithQueueItem(oldQueueItemId, sourceQueueId, new EntityReference("account", accountId))
        });

        var newQueueItemId = organizationService.Simulated().Queues().AddToQueue(
            new EntityReference("account", accountId),
            destinationQueueId,
            sourceQueueId);

        organizationService.Simulated().Data().Get("queueitem").Should().ContainSingle();
        organizationService.Simulated().Data().Get("queueitem", newQueueItemId)
            .GetAttributeValue<EntityReference>("queueid")
            .Id
            .Should()
            .Be(destinationQueueId);
    }

    [Test]
    public void RemoveFromQueue_DeletesQueueItem()
    {
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var organizationService = CreateQueueBackedService(queueId, accountId, queueItemId);

        organizationService.Simulated().Queues().RemoveFromQueue(queueItemId);

        organizationService.Simulated().Data().Get("queueitem").Should().BeEmpty();
        organizationService.Simulated().Audit().Get("RemoveFromQueue").Should().ContainSingle(audit => audit.Id == queueItemId);
    }

    [Test]
    public void PickFromQueue_WhenNotRemoving_SetsWorker()
    {
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var organizationService = CreateQueueBackedService(queueId, accountId, queueItemId);
        var worker = organizationService.Simulated().Data().AuthenticatedUser;

        organizationService.Simulated().Queues().PickFromQueue(queueItemId, worker.Id, removeQueueItem: false);

        organizationService.Simulated().Data().Get("queueitem", queueItemId)
            .GetAttributeValue<EntityReference>("workerid")
            .Should()
            .BeEquivalentTo(worker);
        organizationService.Simulated().Audit().Get("PickFromQueue").Should().ContainSingle(audit => audit.Id == queueItemId);
    }

    [Test]
    public void PickFromQueue_WhenRemoving_DeletesQueueItem()
    {
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var organizationService = CreateQueueBackedService(queueId, accountId, queueItemId);
        var worker = organizationService.Simulated().Data().AuthenticatedUser;

        organizationService.Simulated().Queues().PickFromQueue(queueItemId, worker, removeQueueItem: true);

        organizationService.Simulated().Data().Get("queueitem").Should().BeEmpty();
        organizationService.Simulated().Audit().Get("PickFromQueue").Should().ContainSingle(audit => audit.Id == queueItemId);
    }

    [Test]
    public void ReleaseToQueue_ClearsWorker()
    {
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var workerId = Guid.NewGuid();
        IOrganizationService organizationService = null!;

        organizationService = organizationService.Simulate(new SimulatorOptions
        {
            InitialiseData = CreateAccountData(accountId)
                .With("systemuser", new Entity("systemuser", workerId)
                {
                    ["fullname"] = "Queue Worker"
                }),
            Queues = SimulatedQueueModel.Create()
                .WithQueue(queueId, "Support queue")
                .WithQueueItem(
                    queueItemId,
                    queueId,
                    new EntityReference("account", accountId),
                    new EntityReference("systemuser", workerId))
        });

        organizationService.Simulated().Queues().ReleaseToQueue(queueItemId);

        organizationService.Simulated().Data().Get("queueitem", queueItemId)
            .Attributes["workerid"]
            .Should()
            .BeNull();
        organizationService.Simulated().Audit().Get("ReleaseToQueue").Should().ContainSingle(audit => audit.Id == queueItemId);
    }

    [Test]
    public void RouteTo_Queue_MovesQueueItem()
    {
        var sourceQueueId = Guid.NewGuid();
        var destinationQueueId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        IOrganizationService organizationService = null!;

        organizationService = organizationService.Simulate(new SimulatorOptions
        {
            InitialiseData = CreateAccountData(accountId),
            Queues = SimulatedQueueModel.Create()
                .WithQueue(sourceQueueId, "Source")
                .WithQueue(destinationQueueId, "Destination")
                .WithQueueItem(queueItemId, sourceQueueId, new EntityReference("account", accountId))
        });

        organizationService.Simulated().Queues().RouteTo(queueItemId, new EntityReference("queue", destinationQueueId));

        organizationService.Simulated().Data().Get("queueitem", queueItemId)
            .GetAttributeValue<EntityReference>("queueid")
            .Id
            .Should()
            .Be(destinationQueueId);
        organizationService.Simulated().Audit().Get("RouteTo").Should().ContainSingle(audit => audit.Id == queueItemId);
    }

    [Test]
    public void RouteTo_Principal_SetsWorker()
    {
        var queueId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        IOrganizationService organizationService = null!;

        organizationService = organizationService.Simulate(new SimulatorOptions
        {
            InitialiseData = CreateAccountData(accountId)
                .With("team", new Entity("team", teamId)
                {
                    ["name"] = "Queue Team"
                }),
            Queues = SimulatedQueueModel.Create()
                .WithQueue(queueId, "Support queue")
                .WithQueueItem(queueItemId, queueId, new EntityReference("account", accountId))
        });

        organizationService.Simulated().Queues().RouteTo(queueItemId, new EntityReference("team", teamId));

        organizationService.Simulated().Data().Get("queueitem", queueItemId)
            .GetAttributeValue<EntityReference>("workerid")
            .Should()
            .BeEquivalentTo(new EntityReference("team", teamId));
    }

    [Test]
    public void AddToQueue_WithMetadata_RejectsTargetThatIsNotEnabledForQueues()
    {
        var queueId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        IOrganizationService organizationService = null!;

        organizationService = organizationService.Simulate(new SimulatorOptions
        {
            Metadata = SimulatedMetadata.Load(QueueMetadataPath),
            InitialiseData = new Dictionary<string, List<Entity>>
            {
                ["lead"] =
                [
                    new Entity("lead", leadId)
                    {
                        ["fullname"] = "Ada Lovelace"
                    }
                ]
            },
            Queues = SimulatedQueueModel.Create()
                .WithQueue(queueId, "Strict queue")
        });

        var addToQueue = () => organizationService.Simulated().Queues().AddToQueue(
            new EntityReference("lead", leadId),
            queueId);

        addToQueue.Should()
            .Throw<SimulatedMetadataException>()
            .WithMessage("Entity 'lead' is not enabled for queues.");
    }

    private sealed class FixedClock(DateTime now) : IClockSimulator
    {
        public DateTime Now { get; } = now;
    }

    private static IOrganizationService CreateQueueBackedService(
        Guid queueId,
        Guid accountId,
        Guid? queueItemId = null)
    {
        IOrganizationService organizationService = null!;
        var queueModel = SimulatedQueueModel.Create()
            .WithQueue(queueId, "Support queue");

        if (queueItemId is not null)
        {
            queueModel.WithQueueItem(queueItemId.Value, queueId, new EntityReference("account", accountId));
        }

        return organizationService.Simulate(new SimulatorOptions
        {
            InitialiseData = CreateAccountData(accountId),
            Queues = queueModel
        });
    }

    private static Dictionary<string, List<Entity>> CreateAccountData(Guid accountId)
    {
        return new Dictionary<string, List<Entity>>
        {
            ["account"] =
            [
                new Entity("account", accountId)
                {
                    ["name"] = "Contoso"
                }
            ]
        };
    }
}

internal static class QueueSimulationTestDataExtensions
{
    internal static Dictionary<string, List<Entity>> With(
        this Dictionary<string, List<Entity>> data,
        string logicalName,
        Entity entity)
    {
        if (data.TryGetValue(logicalName, out var entities))
        {
            entities.Add(entity);
        }
        else
        {
            data[logicalName] = [entity];
        }

        return data;
    }
}
