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

    private sealed class FixedClock(DateTime now) : IClockSimulator
    {
        public DateTime Now { get; } = now;
    }
}
