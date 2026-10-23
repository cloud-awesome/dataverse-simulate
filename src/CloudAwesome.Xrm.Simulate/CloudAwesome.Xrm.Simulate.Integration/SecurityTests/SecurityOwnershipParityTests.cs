using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather.SecurityTests;

[TestFixture]
[Category("Parity")]
[Category("SecurityIdentity")]
[Category("Security")]
public sealed class SecurityOwnershipParityTests : IntegrationBaseFixture
{
    private static readonly Guid MissingOwner = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [TestCase("Assign")]
    [TestCase("Update")]
    [TestCase("Upsert")]
    public void Missing_Assignee_Fault_And_Unchanged_Owner_Match(string operation)
    {
        Guid accountId = default;
        var scenario = new DataverseParityScenario<OwnerFault>
        {
            Name = $"Missing_Assignee_{operation}",
            ArrangeLive = context =>
            {
                accountId = context.Service.Create(new Entity("account") { ["name"] = $"CASim missing owner {Guid.NewGuid():N}" });
                context.Cleanup.TrackForDelete("account", accountId);
                context.State.Set("who", (WhoAmIResponse)context.Service.Execute(new WhoAmIRequest()));
            },
            CreateSimulatorOptions = context => Options(context.State.Get<WhoAmIResponse>("who")),
            ArrangeSimulated = context => context.Simulation.Data().Add(new Entity("account", accountId)
            {
                ["ownerid"] = new EntityReference("systemuser", context.State.Get<WhoAmIResponse>("who").UserId)
            }),
            Act = service =>
            {
                int error;
                try
                {
                    ChangeOwner(service, accountId, new EntityReference("systemuser", MissingOwner), operation);
                    throw new AssertionException("Missing assignee unexpectedly accepted.");
                }
                catch (FaultException<OrganizationServiceFault> fault) { error = fault.Detail.ErrorCode; }
                var owner = service.Retrieve("account", accountId, new ColumnSet("ownerid")).GetAttributeValue<EntityReference>("ownerid");
                return new OwnerFault(error, owner.Id);
            }
        };
        DataverseParityHarness.Execute(scenario);
    }

    [TestCase("Assign")]
    [TestCase("Update")]
    [TestCase("Upsert")]
    public void User_Team_User_Ownership_Fields_Match(string operation)
    {
        Guid accountId = default;
        Guid teamId = default;
        WhoAmIResponse who = null!;
        var scenario = new DataverseParityScenario<OwnerShape[]>
        {
            Name = $"Ownership_Fields_{operation}",
            ArrangeLive = context =>
            {
                who = (WhoAmIResponse)context.Service.Execute(new WhoAmIRequest());
                teamId = context.Service.Create(new Entity("team")
                {
                    ["name"] = $"CASim owner {Guid.NewGuid():N}",
                    ["businessunitid"] = new EntityReference("businessunit", who.BusinessUnitId),
                    ["teamtype"] = new OptionSetValue(0)
                });
                context.Cleanup.TrackForDelete("team", teamId);

                // Reuse the caller's existing roles on a transient team; no existing principal is modified.
                var roles = new QueryExpression("role") { ColumnSet = new ColumnSet("roleid") };
                roles.Criteria.AddCondition("businessunitid", ConditionOperator.Equal, who.BusinessUnitId);
                roles.AddLink("systemuserroles", "roleid", "roleid").LinkCriteria.AddCondition("systemuserid", ConditionOperator.Equal, who.UserId);
                var references = context.Service.RetrieveMultiple(roles).Entities.Select(e => e.ToEntityReference()).ToArray();
                references.Should().NotBeEmpty("the live caller needs roles that grant account read and assign access");
                context.Service.Associate("team", teamId, new Relationship("teamroles_association"), new EntityReferenceCollection(references));
                accountId = context.Service.Create(new Entity("account") { ["name"] = $"CASim owner fields {Guid.NewGuid():N}" });
                context.Cleanup.TrackForDelete("account", accountId);
            },
            CreateSimulatorOptions = _ =>
            {
                var options = Options(who);
                var model = (SimulatedSecurityModel)options.SimulatedSecurityModel!;
                model.WithOwnerTeam(teamId, who.BusinessUnitId).AssignRoleToTeam("Account", teamId);
                return options;
            },
            ArrangeSimulated = context => context.Simulation.Data().Add(new Entity("account", accountId)
            {
                ["ownerid"] = new EntityReference("systemuser", who.UserId),
                ["owninguser"] = new EntityReference("systemuser", who.UserId),
                ["owningbusinessunit"] = new EntityReference("businessunit", who.BusinessUnitId)
            }),
            Act = service =>
            {
                ChangeOwner(service, accountId, new EntityReference("team", teamId), operation);
                var teamOwner = Snapshot(service, accountId);
                ChangeOwner(service, accountId, new EntityReference("systemuser", who.UserId), operation);
                return [teamOwner, Snapshot(service, accountId)];
            }
        };
        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void Organization_Owned_Table_Reads_Do_Not_Require_Owner_Columns()
    {
        Entity currency = null!;
        WhoAmIResponse who = null!;
        var scenario = new DataverseParityScenario<Guid[]>
        {
            Name = nameof(Organization_Owned_Table_Reads_Do_Not_Require_Owner_Columns),
            ArrangeLive = context =>
            {
                who = (WhoAmIResponse)context.Service.Execute(new WhoAmIRequest());
                currency = context.Service.RetrieveMultiple(new QueryExpression("transactioncurrency")
                {
                    ColumnSet = new ColumnSet("transactioncurrencyid"), TopCount = 1
                }).Entities.Single();
            },
            CreateSimulatorOptions = _ =>
            {
                var options = Options(who);
                options.Metadata = SimulatedMetadata.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "security-parity-metadata.json"));
                ((SimulatedSecurityModel)options.SimulatedSecurityModel!).Roles.Single()
                    .CanRead("transactioncurrency", PrivilegeDepthEnum.Organization);
                return options;
            },
            ArrangeSimulated = context => context.Simulation.Data().Add(currency),
            Act = service =>
            {
                service.Retrieve("transactioncurrency", currency.Id, new ColumnSet("transactioncurrencyid")).Id.Should().Be(currency.Id);
                var query = new QueryExpression("transactioncurrency") { ColumnSet = new ColumnSet("transactioncurrencyid") };
                query.Criteria.AddCondition("transactioncurrencyid", ConditionOperator.Equal, currency.Id);
                return service.RetrieveMultiple(query).Entities.Select(e => e.Id).ToArray();
            }
        };
        DataverseParityHarness.Execute(scenario);
    }

    private static SimulatorOptions Options(WhoAmIResponse who) => new()
    {
        AuthenticatedUser = new Entity("systemuser", who.UserId) { ["businessunitid"] = new EntityReference("businessunit", who.BusinessUnitId) },
        BusinessUnit = new Entity("businessunit", who.BusinessUnitId),
        SimulatedSecurityModel = SimulatedSecurityModel.Create().WithBusinessUnit(who.BusinessUnitId, "Root")
            .WithUser(who.UserId, who.BusinessUnitId)
            .WithRole("Account", r => r.CanRead("account", PrivilegeDepthEnum.Organization)
                .CanWrite("account", PrivilegeDepthEnum.Organization).CanAssign("account", PrivilegeDepthEnum.Organization))
            .AssignRoleToUser("Account", who.UserId)
    };

    private static void ChangeOwner(IOrganizationService service, Guid id, EntityReference owner, string operation)
    {
        var target = new Entity("account", id) { ["ownerid"] = owner };
        switch (operation)
        {
            case "Assign": service.Execute(new AssignRequest { Target = target.ToEntityReference(), Assignee = owner }); break;
            case "Update": service.Update(target); break;
            case "Upsert": service.Execute(new UpsertRequest { Target = target }); break;
        }
    }

    private static OwnerShape Snapshot(IOrganizationService service, Guid id)
    {
        var row = service.Retrieve("account", id, new ColumnSet("ownerid", "owninguser", "owningteam", "owningbusinessunit"));
        return new OwnerShape(row.GetAttributeValue<EntityReference>("ownerid")?.Id,
            row.GetAttributeValue<EntityReference>("owninguser")?.Id,
            row.GetAttributeValue<EntityReference>("owningteam")?.Id,
            row.GetAttributeValue<EntityReference>("owningbusinessunit")?.Id);
    }
    private sealed record OwnerShape(Guid? Owner, Guid? User, Guid? Team, Guid? BusinessUnit);
    private sealed record OwnerFault(int ErrorCode, Guid Owner);
}
