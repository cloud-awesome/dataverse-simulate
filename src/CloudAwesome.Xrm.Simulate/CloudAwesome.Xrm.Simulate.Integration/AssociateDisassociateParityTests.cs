using System;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture]
[Category("ParitySmoke")]
public sealed class AssociateDisassociateParityTests : IntegrationBaseFixture
{
    private const string AssociationSeedKey = "association-seed";
    private const string AccountLogicalName = "account";
    private const string LeadLogicalName = "lead";
    private const string RelationshipName = "accountleads_association";
    private const string AccountNameAttribute = "name";
    private const string LeadSubjectAttribute = "subject";
    private const string LeadLastNameAttribute = "lastname";

    [Test]
    public void Associate_Direct_Method_Should_Not_Mutate_Retrieved_Target_RelatedEntities()
    {
        var scenario = CreateAssociationScenario(
            nameof(Associate_Direct_Method_Should_Not_Mutate_Retrieved_Target_RelatedEntities),
            (service, seed) =>
            {
                service.Associate(AccountLogicalName, seed.AccountId, Relationship(),
                    new EntityReferenceCollection { new(LeadLogicalName, seed.LeadId) });

                return RetrievedRelatedEntitiesCount(service, AccountLogicalName, seed.AccountId);
            });

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void Associate_Request_Message_Should_Not_Mutate_Retrieved_Target_RelatedEntities()
    {
        var scenario = CreateAssociationScenario(
            nameof(Associate_Request_Message_Should_Not_Mutate_Retrieved_Target_RelatedEntities),
            (service, seed) =>
            {
                service.Execute(new AssociateRequest
                {
                    Target = new EntityReference(AccountLogicalName, seed.AccountId),
                    Relationship = Relationship(),
                    RelatedEntities = new EntityReferenceCollection { new(LeadLogicalName, seed.LeadId) }
                });

                return RetrievedRelatedEntitiesCount(service, AccountLogicalName, seed.AccountId);
            });

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void Disassociate_Direct_Method_Should_Not_Mutate_Retrieved_Target_RelatedEntities()
    {
        var scenario = CreateAssociationScenario(
            nameof(Disassociate_Direct_Method_Should_Not_Mutate_Retrieved_Target_RelatedEntities),
            (service, seed) =>
            {
                var relatedEntities = new EntityReferenceCollection { new(LeadLogicalName, seed.LeadId) };
                service.Associate(AccountLogicalName, seed.AccountId, Relationship(), relatedEntities);
                service.Disassociate(AccountLogicalName, seed.AccountId, Relationship(), relatedEntities);

                return RetrievedRelatedEntitiesCount(service, AccountLogicalName, seed.AccountId);
            });

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void Disassociate_Request_Message_Should_Not_Mutate_Retrieved_Target_RelatedEntities()
    {
        var scenario = CreateAssociationScenario(
            nameof(Disassociate_Request_Message_Should_Not_Mutate_Retrieved_Target_RelatedEntities),
            (service, seed) =>
            {
                var relatedEntities = new EntityReferenceCollection { new(LeadLogicalName, seed.LeadId) };
                service.Associate(AccountLogicalName, seed.AccountId, Relationship(), relatedEntities);
                service.Execute(new DisassociateRequest
                {
                    Target = new EntityReference(AccountLogicalName, seed.AccountId),
                    Relationship = Relationship(),
                    RelatedEntities = relatedEntities
                });

                return RetrievedRelatedEntitiesCount(service, AccountLogicalName, seed.AccountId);
            });

        DataverseParityHarness.Execute(scenario);
    }

    private static DataverseParityScenario<int> CreateAssociationScenario(
        string testName,
        Func<IOrganizationService, AssociationSeed, int> act)
    {
        AssociationSeed? seed = null;

        return new DataverseParityScenario<int>
        {
            Name = testName,
            ArrangeLive = context =>
            {
                seed = CreateLiveAccountAndLead(context, testName);
                context.State.Set(AssociationSeedKey, seed);
            },
            ArrangeSimulated = context =>
            {
                var seed = context.State.Get<AssociationSeed>(AssociationSeedKey);
                context.Simulation.Data().Add(Account(seed.AccountId, testName));
                context.Simulation.Data().Add(Lead(seed.LeadId, testName));
            },
            Act = service => act(
                service,
                seed ?? throw new InvalidOperationException("Association seed was not arranged."))
        };
    }

    private static AssociationSeed CreateLiveAccountAndLead(
        LiveDataverseScenarioContext context,
        string testName)
    {
        var accountId = context.Service.Create(Account(Guid.Empty, testName));
        context.Cleanup.TrackForDelete(AccountLogicalName, accountId);

        var leadId = context.Service.Create(Lead(Guid.Empty, testName));
        context.Cleanup.TrackForDelete(LeadLogicalName, leadId);

        return new AssociationSeed(accountId, leadId);
    }

    private static int RetrievedRelatedEntitiesCount(
        IOrganizationService service,
        string logicalName,
        Guid id)
    {
        return service.Retrieve(logicalName, id, new ColumnSet(true))
            .RelatedEntities
            .Count;
    }

    private static Relationship Relationship()
    {
        return new Relationship(RelationshipName);
    }

    private static Entity Account(Guid id, string testName)
    {
        var account = new Entity(AccountLogicalName)
        {
            [AccountNameAttribute] = $"CASim {testName} {Guid.NewGuid():N}"
        };

        if (id != Guid.Empty)
        {
            account.Id = id;
        }

        return account;
    }

    private static Entity Lead(Guid id, string testName)
    {
        var lead = new Entity(LeadLogicalName)
        {
            [LeadSubjectAttribute] = $"CASim {testName} {Guid.NewGuid():N}",
            [LeadLastNameAttribute] = "Parity"
        };

        if (id != Guid.Empty)
        {
            lead.Id = id;
        }

        return lead;
    }

    private sealed record AssociationSeed(Guid AccountId, Guid LeadId);
}
