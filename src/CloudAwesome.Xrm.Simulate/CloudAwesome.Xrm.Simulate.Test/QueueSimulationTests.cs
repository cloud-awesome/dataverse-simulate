using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.Queues;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
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

    [Test]
    public void AddToQueue_WithSecurity_AllowsWhenRequiredAccessIsPresent()
    {
        var fixture = CreateSecuredQueueFixture(role => role
            .CanRead("account", PrivilegeDepthEnum.Organization)
            .CanRead("queue", PrivilegeDepthEnum.Organization)
            .CanCreate("queueitem", PrivilegeDepthEnum.Organization));

        var queueItemId = fixture.Service.Simulated().Queues().AddToQueue(
            fixture.Account.ToEntityReference(),
            fixture.QueueId);

        fixture.Service.Simulated().Data().Get("queueitem", queueItemId)
            .GetAttributeValue<EntityReference>("objectid")
            .Should()
            .BeEquivalentTo(fixture.Account.ToEntityReference());
    }

    [Test]
    public void AddToQueue_WithSecurity_DeniesWhenTargetReadAccessIsMissing()
    {
        var fixture = CreateSecuredQueueFixture(role => role
            .CanRead("queue", PrivilegeDepthEnum.Organization)
            .CanCreate("queueitem", PrivilegeDepthEnum.Organization));

        var addToQueue = () => fixture.Service.Simulated().Queues().AddToQueue(
            fixture.Account.ToEntityReference(),
            fixture.QueueId);

        addToQueue.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void AddToQueue_WithSecurity_DeniesWhenQueueItemCreateAccessIsMissing()
    {
        var fixture = CreateSecuredQueueFixture(role => role
            .CanRead("account", PrivilegeDepthEnum.Organization)
            .CanRead("queue", PrivilegeDepthEnum.Organization));

        var addToQueue = () => fixture.Service.Simulated().Queues().AddToQueue(
            fixture.Account.ToEntityReference(),
            fixture.QueueId);

        addToQueue.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void RemoveFromQueue_WithSecurity_DeniesWhenQueueItemDeleteAccessIsMissing()
    {
        var fixture = CreateSecuredQueueFixture(
            role => role.CanWrite("queueitem", PrivilegeDepthEnum.Organization),
            includeQueueItem: true);

        var remove = () => fixture.Service.Simulated().Queues().RemoveFromQueue(fixture.QueueItemId!.Value);

        remove.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void PickFromQueue_WithSecurity_DeniesWhenQueueItemWriteAccessIsMissing()
    {
        var fixture = CreateSecuredQueueFixture(_ => { }, includeQueueItem: true);

        var pick = () => fixture.Service.Simulated().Queues().PickFromQueue(
            fixture.QueueItemId!.Value,
            fixture.ActingUser,
            removeQueueItem: false);

        pick.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void ReleaseToQueue_WithSecurity_DeniesWhenQueueItemWriteAccessIsMissing()
    {
        var fixture = CreateSecuredQueueFixture(_ => { }, includeQueueItem: true);

        var release = () => fixture.Service.Simulated().Queues().ReleaseToQueue(fixture.QueueItemId!.Value);

        release.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void RouteToQueue_WithSecurity_DeniesWhenDestinationQueueWriteAccessIsMissing()
    {
        var destinationQueueId = Guid.NewGuid();
        var fixture = CreateSecuredQueueFixture(
            role => role
                .CanRead("queue", PrivilegeDepthEnum.Organization)
                .CanWrite("queueitem", PrivilegeDepthEnum.Organization),
            includeQueueItem: true,
            destinationQueueId: destinationQueueId);

        var route = () => fixture.Service.Simulated().Queues().RouteTo(
            fixture.QueueItemId!.Value,
            new EntityReference("queue", destinationQueueId));

        route.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void Execute_AddToQueueRequest_CreatesQueueItemAndReturnsTypedResponse()
    {
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var organizationService = CreateQueueBackedService(queueId, accountId);

        var response = (AddToQueueResponse)organizationService.Execute(new AddToQueueRequest
        {
            Target = new EntityReference("account", accountId),
            DestinationQueueId = queueId,
            QueueItemProperties = new Entity("queueitem", queueItemId)
            {
                ["title"] = "Request title"
            }
        });

        response.ResponseName.Should().Be("AddToQueue");
        response.QueueItemId.Should().Be(queueItemId);
        organizationService.Simulated().Data().Get("queueitem", queueItemId)
            .GetAttributeValue<string>("title")
            .Should()
            .Be("Request title");
    }

    [Test]
    public void Execute_RemoveFromQueueRequest_DeletesQueueItem()
    {
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var organizationService = CreateQueueBackedService(queueId, accountId, queueItemId);

        var response = (RemoveFromQueueResponse)organizationService.Execute(new RemoveFromQueueRequest
        {
            QueueItemId = queueItemId
        });

        response.ResponseName.Should().Be("RemoveFromQueue");
        organizationService.Simulated().Data().Get("queueitem").Should().BeEmpty();
    }

    [Test]
    public void Execute_PickFromQueueRequest_SetsWorker()
    {
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var organizationService = CreateQueueBackedService(queueId, accountId, queueItemId);
        var workerId = organizationService.Simulated().Data().AuthenticatedUser.Id;

        var response = (PickFromQueueResponse)organizationService.Execute(new PickFromQueueRequest
        {
            QueueItemId = queueItemId,
            WorkerId = workerId,
            RemoveQueueItem = false
        });

        response.ResponseName.Should().Be("PickFromQueue");
        organizationService.Simulated().Data().Get("queueitem", queueItemId)
            .GetAttributeValue<EntityReference>("workerid")
            .Id
            .Should()
            .Be(workerId);
    }

    [Test]
    public void Execute_ReleaseToQueueRequest_ClearsWorker()
    {
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = Guid.NewGuid();
        var organizationService = CreateQueueBackedService(queueId, accountId, queueItemId);
        var workerId = organizationService.Simulated().Data().AuthenticatedUser.Id;
        organizationService.Simulated().Queues().PickFromQueue(queueItemId, workerId, removeQueueItem: false);

        var response = (ReleaseToQueueResponse)organizationService.Execute(new ReleaseToQueueRequest
        {
            QueueItemId = queueItemId
        });

        response.ResponseName.Should().Be("ReleaseToQueue");
        organizationService.Simulated().Data().Get("queueitem", queueItemId)
            .Attributes["workerid"]
            .Should()
            .BeNull();
    }

    [Test]
    public void Execute_RouteToRequest_MovesQueueItem()
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

        var response = (RouteToResponse)organizationService.Execute(new RouteToRequest
        {
            QueueItemId = queueItemId,
            Target = new EntityReference("queue", destinationQueueId)
        });

        response.ResponseName.Should().Be("RouteTo");
        organizationService.Simulated().Data().Get("queueitem", queueItemId)
            .GetAttributeValue<EntityReference>("queueid")
            .Id
            .Should()
            .Be(destinationQueueId);
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

    private static SecuredQueueFixture CreateSecuredQueueFixture(
        Action<SimulatedSecurityRole> configureRole,
        bool includeQueueItem = false,
        Guid? destinationQueueId = null)
    {
        var businessUnitId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var queueId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var queueItemId = includeQueueItem ? Guid.NewGuid() : (Guid?)null;
        var actingUser = new EntityReference("systemuser", actingUserId);
        var authenticatedUser = new Entity("systemuser", actingUserId)
        {
            ["businessunitid"] = new EntityReference("businessunit", businessUnitId)
        };

        var security = SimulatedSecurityModel.Create();
        security.IgnoreMissingEntities = false;
        security
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(actingUserId, businessUnitId)
            .WithUser(otherUserId, businessUnitId)
            .WithRole("Queue Role", configureRole)
            .AssignRoleToUser("Queue Role", actingUserId);

        var account = new Entity("account", accountId)
        {
            ["name"] = "Secured account",
            ["ownerid"] = actingUser
        };

        var queues = SimulatedQueueModel.Create()
            .WithQueue(queueId, "Secured queue");

        if (destinationQueueId is not null)
        {
            queues.WithQueue(destinationQueueId.Value, "Destination queue");
        }

        if (queueItemId is not null)
        {
            queues.WithQueueItem(queueItemId.Value, queueId, account.ToEntityReference());
        }

        IOrganizationService organizationService = null!;
        var service = organizationService.Simulate(new SimulatorOptions
        {
            AuthenticatedUser = authenticatedUser,
            InitialiseData = new Dictionary<string, List<Entity>>
            {
                ["account"] = [account]
            },
            Queues = queues,
            SimulatedSecurityModel = security
        });

        return new SecuredQueueFixture(
            service,
            actingUser,
            queueId,
            account,
            queueItemId);
    }

    private sealed record SecuredQueueFixture(
        IOrganizationService Service,
        EntityReference ActingUser,
        Guid QueueId,
        Entity Account,
        Guid? QueueItemId);

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
