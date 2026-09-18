using System;
using System.IO;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather.MetadataTests;

[TestFixture]
[Category("Parity")]
[Category("Metadata")]
[Category("Security")]
public sealed class MetadataRetrievalSecurityParityTests : IntegrationBaseFixture
{
    private const string AccountLogicalName = "account";
    private const string AccountNumberAttribute = "accountnumber";
    private const string AccountPrimaryContactRelationship = "account_primary_contact";
    private const string CurrentUserIdKey = "current-user-id";
    private const string CurrentBusinessUnitIdKey = "current-business-unit-id";

    private static string MetadataPath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "entity-metadata.json");

    [Test]
    public void RetrieveEntity_And_RetrieveAttribute_Do_Not_Require_Table_Read_Privilege()
    {
        var scenario = new DataverseParityScenario<EntityAndAttributeMetadataSnapshot>
        {
            Name = nameof(RetrieveEntity_And_RetrieveAttribute_Do_Not_Require_Table_Read_Privilege),
            ArrangeLive = CaptureCurrentUser,
            CreateSimulatorOptions = CreateStrictMetadataOptions,
            Act = service =>
            {
                var entityResponse = (RetrieveEntityResponse)service.Execute(new RetrieveEntityRequest
                {
                    LogicalName = AccountLogicalName,
                    EntityFilters = EntityFilters.Entity
                });
                var attributeResponse = (RetrieveAttributeResponse)service.Execute(new RetrieveAttributeRequest
                {
                    EntityLogicalName = AccountLogicalName,
                    LogicalName = AccountNumberAttribute
                });

                return new EntityAndAttributeMetadataSnapshot(
                    entityResponse.ResponseName,
                    entityResponse.EntityMetadata.LogicalName,
                    attributeResponse.ResponseName,
                    attributeResponse.AttributeMetadata.LogicalName);
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void RetrieveAllEntities_And_RetrieveRelationship_Do_Not_Require_Table_Read_Privilege()
    {
        var scenario = new DataverseParityScenario<AllEntitiesAndRelationshipMetadataSnapshot>
        {
            Name = nameof(RetrieveAllEntities_And_RetrieveRelationship_Do_Not_Require_Table_Read_Privilege),
            ArrangeLive = CaptureCurrentUser,
            CreateSimulatorOptions = CreateStrictMetadataOptions,
            Act = service =>
            {
                var allEntitiesResponse = (RetrieveAllEntitiesResponse)service.Execute(new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity
                });
                var relationshipResponse = (RetrieveRelationshipResponse)service.Execute(new RetrieveRelationshipRequest
                {
                    Name = AccountPrimaryContactRelationship
                });

                return new AllEntitiesAndRelationshipMetadataSnapshot(
                    allEntitiesResponse.ResponseName,
                    allEntitiesResponse.EntityMetadata.Any(entity =>
                        string.Equals(entity.LogicalName, AccountLogicalName, StringComparison.OrdinalIgnoreCase)),
                    relationshipResponse.ResponseName,
                    relationshipResponse.RelationshipMetadata.SchemaName.ToLowerInvariant());
            },
            AssertEquivalent = static (live, simulated) =>
            {
                simulated.Should().BeEquivalentTo(live);
                simulated.AllEntitiesContainsAccount.Should().BeTrue();
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    private static void CaptureCurrentUser(LiveDataverseScenarioContext context)
    {
        var whoAmI = (WhoAmIResponse)context.Service.Execute(new WhoAmIRequest());
        context.State.Set(CurrentUserIdKey, whoAmI.UserId);
        context.State.Set(CurrentBusinessUnitIdKey, whoAmI.BusinessUnitId);
    }

    private static SimulatorOptions CreateStrictMetadataOptions(LiveDataverseScenarioContext context)
    {
        var userId = context.State.Get<Guid>(CurrentUserIdKey);
        var businessUnitId = context.State.Get<Guid>(CurrentBusinessUnitIdKey);
        var securityModel = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId);

        securityModel.IgnoreMissingEntities = false;

        return new SimulatorOptions
        {
            Metadata = SimulatedMetadata.Load(MetadataPath),
            AuthenticatedUser = new Entity("systemuser", userId)
            {
                ["businessunitid"] = new EntityReference("businessunit", businessUnitId)
            },
            SimulatedSecurityModel = securityModel
        };
    }

    private sealed record EntityAndAttributeMetadataSnapshot(
        string EntityResponseName,
        string? EntityLogicalName,
        string AttributeResponseName,
        string? AttributeLogicalName);

    private sealed record AllEntitiesAndRelationshipMetadataSnapshot(
        string AllEntitiesResponseName,
        bool AllEntitiesContainsAccount,
        string RelationshipResponseName,
        string RelationshipSchemaName);
}
