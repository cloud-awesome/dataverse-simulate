using System;
using System.IO;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test;

[TestFixture]
public class MetadataSimulationTests
{
    private static string MetadataPath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "entity-metadata.json");

    [Test]
    public void Load_Reads_DvCli_Split_Metadata_Output()
    {
        var metadata = SimulatedMetadata.Load(MetadataPath);

        metadata.ContractVersion.Should().Be("1.0");
        metadata.Entities.Should().Contain(entity => entity.LogicalName == "account");
        metadata.GetEntity("account").PrimaryIdAttribute.Should().Be("accountid");
        metadata.GetEntity("account").GetAttribute("name").RequiredLevel.Should().Be("ApplicationRequired");
    }

    [Test]
    public void Create_Without_Metadata_Remains_Permissive()
    {
        IOrganizationService organizationService = null!;
        organizationService = organizationService.Simulate();

        var id = organizationService.Create(new Entity("notarealtable")
        {
            ["notarealcolumn"] = "still allowed without metadata"
        });

        id.Should().NotBeEmpty();
        organizationService.Simulated().Data().Get("notarealtable").Should().ContainSingle();
    }

    [Test]
    public void Create_With_Metadata_Rejects_Unknown_Entity()
    {
        var organizationService = CreateMetadataBackedService();

        var create = () => organizationService.Create(new Entity("notarealtable"));

        create.Should()
            .Throw<SimulatedMetadataException>()
            .WithMessage("Metadata does not define entity 'notarealtable'.");
    }
    
    [Test]
    public void Metadata_Can_Be_Loaded_After_Simulation()
    {
        IOrganizationService organizationService = null!;
        organizationService = organizationService.Simulate();
        
        organizationService.Simulated().Metadata().Load(MetadataPath);
        organizationService.Simulated().Metadata().IsLoaded.Should().BeTrue();
    }

    [Test]
    public void Create_With_Metadata_Rejects_Unknown_Attribute()
    {
        var organizationService = CreateMetadataBackedService();

        var create = () => organizationService.Create(new Entity("account")
        {
            ["name"] = "Contoso",
            ["notarealcolumn"] = "nope"
        });

        create.Should()
            .Throw<SimulatedMetadataException>()
            .WithMessage("Metadata for entity 'account' does not define attribute 'notarealcolumn'.");
    }

    [Test]
    public void Create_With_Metadata_Rejects_Missing_Required_Attribute()
    {
        var organizationService = CreateMetadataBackedService();

        var create = () => organizationService.Create(new Entity("account"));

        create.Should()
            .Throw<SimulatedMetadataException>()
            .WithMessage("Attribute 'account.name' is required for create.");
    }

    [Test]
    public void Create_With_Metadata_Validates_Length_Range_Lookup_And_Option_Values()
    {
        var organizationService = CreateMetadataBackedService();

        Action createTooLong = () => organizationService.Create(new Entity("account")
        {
            ["name"] = "Contoso",
            ["accountnumber"] = new string('A', 21)
        });
        Action createOutOfRange = () => organizationService.Create(new Entity("account")
        {
            ["name"] = "Contoso",
            ["numberofemployees"] = -1
        });
        Action createInvalidLookup = () => organizationService.Create(new Entity("account")
        {
            ["name"] = "Contoso",
            ["primarycontactid"] = new EntityReference("lead", Guid.NewGuid())
        });
        Action createInvalidOption = () => organizationService.Create(new Entity("account")
        {
            ["name"] = "Contoso",
            ["accountcategorycode"] = new OptionSetValue(999)
        });

        createTooLong.Should().Throw<SimulatedMetadataException>()
            .WithMessage("Attribute 'account.accountnumber' exceeds max length 20.");
        createOutOfRange.Should().Throw<SimulatedMetadataException>()
            .WithMessage("Attribute 'account.numberofemployees' is below minimum value 0.");
        createInvalidLookup.Should().Throw<SimulatedMetadataException>()
            .WithMessage("Attribute 'account.primarycontactid' does not allow lookup target 'lead'.");
        createInvalidOption.Should().Throw<SimulatedMetadataException>()
            .WithMessage("Attribute 'account.accountcategorycode' does not define option value 999.");
    }

    [Test]
    public void Create_With_Metadata_Defaults_State_And_Status()
    {
        var organizationService = CreateMetadataBackedService();

        var id = organizationService.Create(new Entity("account")
        {
            ["name"] = "Contoso"
        });

        var stored = organizationService.Simulated().Data().Get("account", id);
        stored.GetAttributeValue<OptionSetValue>("statecode").Value.Should().Be(0);
        stored.GetAttributeValue<OptionSetValue>("statuscode").Value.Should().Be(1);
    }

    [Test]
    public void Update_With_Metadata_Rejects_Invalid_State_Status_Pair()
    {
        var organizationService = CreateMetadataBackedService();
        var id = organizationService.Create(new Entity("account")
        {
            ["name"] = "Contoso"
        });

        var update = () => organizationService.Update(new Entity("account", id)
        {
            ["statecode"] = new OptionSetValue(0),
            ["statuscode"] = new OptionSetValue(2)
        });

        update.Should()
            .Throw<SimulatedMetadataException>()
            .WithMessage("Entity 'account' does not allow state '0' with status '2'.");
    }

    [Test]
    public void Update_With_Metadata_Rejects_Update_Invalid_Attribute()
    {
        var organizationService = CreateMetadataBackedService();
        var id = organizationService.Create(new Entity("account")
        {
            ["name"] = "Contoso"
        });

        var update = () => organizationService.Update(new Entity("account", id)
        {
            ["createdon"] = DateTime.UtcNow
        });

        update.Should()
            .Throw<SimulatedMetadataException>()
            .WithMessage("Attribute 'account.createdon' is not valid for update.");
    }

    [Test]
    public void Retrieve_With_Metadata_Rejects_Unknown_Column()
    {
        var organizationService = CreateMetadataBackedService();
        var id = organizationService.Create(new Entity("account")
        {
            ["name"] = "Contoso"
        });

        var retrieve = () => organizationService.Retrieve("account", id, new ColumnSet("notarealcolumn"));

        retrieve.Should()
            .Throw<SimulatedMetadataException>()
            .WithMessage("Metadata for entity 'account' does not define attribute 'notarealcolumn'.");
    }

    [Test]
    public void RetrieveMultiple_With_Metadata_Rejects_Unknown_Filter_Attribute()
    {
        var organizationService = CreateMetadataBackedService();

        var retrieve = () => organizationService.RetrieveMultiple(new QueryExpression("account")
        {
            ColumnSet = new ColumnSet("name"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("notarealcolumn", ConditionOperator.Equal, "x")
                }
            }
        });

        retrieve.Should()
            .Throw<SimulatedMetadataException>()
            .WithMessage("Metadata for entity 'account' does not define attribute 'notarealcolumn'.");
    }

    [Test]
    public void RetrieveEntityRequest_Returns_Sdk_Entity_Metadata()
    {
        var organizationService = CreateMetadataBackedService();

        var response = (RetrieveEntityResponse)organizationService.Execute(new RetrieveEntityRequest
        {
            LogicalName = "account",
            EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships
        });

        response.ResponseName.Should().Be("RetrieveEntity");
        response.EntityMetadata.LogicalName.Should().Be("account");
        response.EntityMetadata.Attributes.Should().Contain(attribute => attribute.LogicalName == "name");
        response.EntityMetadata.ManyToOneRelationships.Should()
            .Contain(relationship => relationship.SchemaName == "account_primary_contact");
    }

    [Test]
    public void RetrieveAttributeRequest_Returns_Sdk_Attribute_Metadata()
    {
        var organizationService = CreateMetadataBackedService();

        var response = (RetrieveAttributeResponse)organizationService.Execute(new RetrieveAttributeRequest
        {
            EntityLogicalName = "account",
            LogicalName = "accountnumber"
        });

        response.ResponseName.Should().Be("RetrieveAttribute");
        response.AttributeMetadata.Should().BeOfType<StringAttributeMetadata>();
        response.AttributeMetadata.LogicalName.Should().Be("accountnumber");
        ((StringAttributeMetadata)response.AttributeMetadata).MaxLength.Should().Be(20);
    }

    [Test]
    public void RetrieveAllEntitiesRequest_Returns_Loaded_Entities()
    {
        var organizationService = CreateMetadataBackedService();

        var response = (RetrieveAllEntitiesResponse)organizationService.Execute(new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity
        });

        response.ResponseName.Should().Be("RetrieveAllEntities");
        response.EntityMetadata.Select(entity => entity.LogicalName)
            .Should()
            .BeEquivalentTo(["account", "contact", "lead"]);
    }

    [Test]
    public void RetrieveRelationshipRequest_Returns_Sdk_Relationship_Metadata()
    {
        var organizationService = CreateMetadataBackedService();

        var response = (RetrieveRelationshipResponse)organizationService.Execute(new RetrieveRelationshipRequest
        {
            Name = "account_primary_contact"
        });

        response.ResponseName.Should().Be("RetrieveRelationship");
        response.RelationshipMetadata.Should().BeOfType<OneToManyRelationshipMetadata>();
        var relationship = (OneToManyRelationshipMetadata)response.RelationshipMetadata;
        relationship.ReferencingEntity.Should().Be("account");
        relationship.ReferencingAttribute.Should().Be("primarycontactid");
        relationship.ReferencedEntity.Should().Be("contact");
    }

    [Test]
    public void Metadata_Retrieval_Requests_Do_Not_Require_Table_Read_Security()
    {
        var organizationService = CreateMetadataBackedService(_ => { });

        var retrieveContact = (RetrieveEntityResponse)organizationService.Execute(new RetrieveEntityRequest
        {
            LogicalName = "contact",
            EntityFilters = EntityFilters.Entity
        });
        var retrieveAll = (RetrieveAllEntitiesResponse)organizationService.Execute(new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity
        });

        retrieveContact.EntityMetadata.LogicalName.Should().Be("contact");
        retrieveAll.EntityMetadata.Select(entity => entity.LogicalName)
            .Should()
            .BeEquivalentTo(["account", "contact", "lead"]);
    }

    private static IOrganizationService CreateMetadataBackedService(Action<SimulatedSecurityRole>? configureRole = null)
    {
        IOrganizationService organizationService = null!;
        var options = new SimulatorOptions
        {
            Metadata = SimulatedMetadata.Load(MetadataPath)
        };

        if (configureRole is not null)
        {
            var businessUnitId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            options.AuthenticatedUser = new Entity("systemuser", userId)
            {
                ["businessunitid"] = new EntityReference("businessunit", businessUnitId)
            };
            options.SimulatedSecurityModel = SimulatedSecurityModel.Create()
                .WithBusinessUnit(businessUnitId, "Root")
                .WithUser(userId, businessUnitId)
                .WithRole("Metadata Reader", configureRole)
                .AssignRoleToUser("Metadata Reader", userId);
            ((SimulatedSecurityModel)options.SimulatedSecurityModel).IgnoreMissingEntities = false;
        }

        return organizationService.Simulate(new SimulatorOptions
        {
            Metadata = options.Metadata,
            AuthenticatedUser = options.AuthenticatedUser,
            SimulatedSecurityModel = options.SimulatedSecurityModel
        });
    }
}
