using System;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.SecurityModelTests;

[TestFixture]
public class SimulatedSecurityModelTests
{
    [Test]
    public void Validate_Allows_Complete_Security_Graph()
    {
        var rootBuId = Guid.NewGuid();
        var childBuId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var sut = SimulatedSecurityModel.Create()
            .WithBusinessUnit(rootBuId, "Root")
            .WithBusinessUnit(childBuId, "Sales", rootBuId)
            .WithUser(userId, childBuId, "Test User")
            .WithOwnerTeam(teamId, childBuId, "Sales Team")
            .WithTeamMember(teamId, userId)
            .WithRole("Salesperson", role => role.CanRead("account", PrivilegeDepthEnum.BusinessUnit))
            .AssignRoleToUser("Salesperson", userId)
            .WithPrincipalObjectAccess(
                new EntityReference("account", accountId),
                new EntityReference("systemuser", userId),
                AccessRights.ReadAccess);

        sut.Invoking(x => x.Validate()).Should().NotThrow();
        sut.TryGetBusinessUnit(childBuId, out var businessUnit).Should().BeTrue();
        businessUnit.Name.Should().Be("Sales");
        sut.GetTeamIdsForUser(userId).Should().BeEquivalentTo([teamId]);
    }

    [Test]
    public void Validate_Throws_For_Duplicate_Business_Unit_Ids()
    {
        var businessUnitId = Guid.NewGuid();
        var sut = new SimulatedSecurityModel
        {
            BusinessUnits =
            {
                new SimulatedBusinessUnit(businessUnitId, "Root"),
                new SimulatedBusinessUnit(businessUnitId, "Duplicate")
            }
        };

        sut.Invoking(x => x.Validate())
            .Should()
            .Throw<SimulatedSecurityModelException>()
            .WithMessage($"A business unit with id '{businessUnitId}' is duplicated.");
    }

    [Test]
    public void Validate_Throws_When_Business_Unit_References_Missing_Parent()
    {
        var businessUnitId = Guid.NewGuid();
        var missingParentBusinessUnitId = Guid.NewGuid();
        var sut = new SimulatedSecurityModel
        {
            BusinessUnits =
            {
                new SimulatedBusinessUnit(businessUnitId, "Sales", missingParentBusinessUnitId)
            }
        };

        sut.Invoking(x => x.Validate())
            .Should()
            .Throw<SimulatedSecurityModelException>()
            .WithMessage(
                $"Business unit '{businessUnitId}' references missing parent business unit '{missingParentBusinessUnitId}'.");
    }

    [Test]
    public void Validate_Throws_When_User_References_Missing_Business_Unit()
    {
        var userId = Guid.NewGuid();
        var missingBusinessUnitId = Guid.NewGuid();
        var sut = new SimulatedSecurityModel
        {
            Users =
            {
                new SimulatedUser(userId, missingBusinessUnitId)
            }
        };

        sut.Invoking(x => x.Validate())
            .Should()
            .Throw<SimulatedSecurityModelException>()
            .WithMessage($"User '{userId}' references missing business unit '{missingBusinessUnitId}'.");
    }

    [Test]
    public void Validate_Throws_When_Role_Assignment_References_Missing_Role()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sut = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId)
            .AssignRoleToUser("Missing Role", userId);

        sut.Invoking(x => x.Validate())
            .Should()
            .Throw<SimulatedSecurityModelException>()
            .WithMessage("Role assignment references missing role 'Missing Role'.");
    }

    [Test]
    public void Validate_Throws_When_Team_Membership_References_Missing_User()
    {
        var businessUnitId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var missingUserId = Guid.NewGuid();
        var sut = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithOwnerTeam(teamId, businessUnitId)
            .WithTeamMember(teamId, missingUserId);

        sut.Invoking(x => x.Validate())
            .Should()
            .Throw<SimulatedSecurityModelException>()
            .WithMessage($"Team membership references missing user '{missingUserId}'.");
    }

    [Test]
    public void IsBusinessUnitDescendantOrSelf_Returns_Expected_Hierarchy_Result()
    {
        var rootBuId = Guid.NewGuid();
        var childBuId = Guid.NewGuid();
        var grandchildBuId = Guid.NewGuid();
        var siblingBuId = Guid.NewGuid();

        var sut = SimulatedSecurityModel.Create()
            .WithBusinessUnit(rootBuId, "Root")
            .WithBusinessUnit(childBuId, "Sales", rootBuId)
            .WithBusinessUnit(grandchildBuId, "Enterprise", childBuId)
            .WithBusinessUnit(siblingBuId, "Service", rootBuId);

        sut.IsBusinessUnitDescendantOrSelf(grandchildBuId, rootBuId).Should().BeTrue();
        sut.IsBusinessUnitDescendantOrSelf(grandchildBuId, childBuId).Should().BeTrue();
        sut.IsBusinessUnitDescendantOrSelf(childBuId, childBuId).Should().BeTrue();
        sut.IsBusinessUnitDescendantOrSelf(childBuId, grandchildBuId).Should().BeFalse();
        sut.IsBusinessUnitDescendantOrSelf(grandchildBuId, siblingBuId).Should().BeFalse();
    }

    [Test]
    public void GetEffectiveEntityPermissionsForUser_Merges_Direct_And_Owner_Team_Roles_Using_Highest_Depth()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var sut = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId)
            .WithOwnerTeam(teamId, businessUnitId)
            .WithTeamMember(teamId, userId)
            .WithRole("User Role", role => role
                .CanRead("contact", PrivilegeDepthEnum.User)
                .CanWrite("contact", PrivilegeDepthEnum.BusinessUnit))
            .WithRole("Team Role", role => role
                .CanRead("contact", PrivilegeDepthEnum.Organization)
                .CanCreate("account", PrivilegeDepthEnum.User))
            .AssignRoleToUser("User Role", userId)
            .AssignRoleToTeam("Team Role", teamId);

        var contact = sut.GetEffectiveEntityPermissionForUser(userId, "contact");
        var account = sut.GetEffectiveEntityPermissionForUser(userId, "account");

        contact.Should().NotBeNull();
        contact!.Read.Should().Be(PrivilegeDepthEnum.Organization);
        contact.Write.Should().Be(PrivilegeDepthEnum.BusinessUnit);
        account.Should().NotBeNull();
        account!.Create.Should().Be(PrivilegeDepthEnum.User);
    }

    [Test]
    public void GetEffectiveEntityPermissionsForUser_Does_Not_Use_Access_Team_Roles_For_Mvp()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var sut = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId)
            .WithAccessTeam(teamId, businessUnitId)
            .WithTeamMember(teamId, userId)
            .WithRole("Access Team Role", role => role.CanRead("account", PrivilegeDepthEnum.Organization))
            .AssignRoleToTeam("Access Team Role", teamId);

        var permissions = sut.GetEffectiveEntityPermissionsForUser(userId);

        permissions.Should().BeEmpty();
    }

    [Test]
    public void CreatePrincipalEntities_Returns_Common_Dataverse_System_Rows()
    {
        var businessUnitId = Guid.NewGuid();
        var childBusinessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var sut = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithBusinessUnit(childBusinessUnitId, "Sales", businessUnitId)
            .WithUser(userId, childBusinessUnitId, "Test User")
            .WithOwnerTeam(teamId, childBusinessUnitId, "Sales Team");

        var entities = sut.CreatePrincipalEntities();

        entities.Should().Contain(x => x.LogicalName == "businessunit" && x.Id == businessUnitId);
        entities.Should().Contain(x =>
            x.LogicalName == "businessunit" &&
            x.Id == childBusinessUnitId &&
            x.GetAttributeValue<EntityReference>("parentbusinessunitid").Id == businessUnitId);
        entities.Should().Contain(x =>
            x.LogicalName == "systemuser" &&
            x.Id == userId &&
            x.GetAttributeValue<string>("fullname") == "Test User");
        entities.Should().Contain(x =>
            x.LogicalName == "team" &&
            x.Id == teamId &&
            x.GetAttributeValue<string>("name") == "Sales Team");
    }

    [Test]
    public void SetUp_Cannot_Create_Two_Root_BusinessUnits()
    {
        var rootBu1Id = Guid.NewGuid();
        var childBu1Id = Guid.NewGuid();
        var rootBu2Id = Guid.NewGuid();
        
        var sut = () => SimulatedSecurityModel.Create()
            .WithBusinessUnit(rootBu1Id, "Root 1")
            .WithBusinessUnit(childBu1Id, "Child", Guid.NewGuid())
            .WithBusinessUnit(rootBu2Id, "Root 2");
        
        sut.Should().Throw<SimulatedSecurityModelException>();
    }

    [Test]
    public void BusinessUnit_Creation_Should_Generate_A_Related_Team()
    {
        IOrganizationService service = null!;
        
        var rootBuId = Guid.NewGuid();
        service = service.Simulate(new SimulatorOptions
        {
            SimulatedSecurityModel = SimulatedSecurityModel.Create()
                .WithBusinessUnit(rootBuId, "Root") 
        });
        
        
        service.Simulated().SecurityModel().Model.BusinessUnits.Count.Should().Be(1);
        service.Simulated().SecurityModel().Model.Teams.Count.Should().Be(1);
        
        service.Simulated().SecurityModel().Model.BusinessUnits.Should().Contain(x =>
            x.Id == rootBuId &&
            x.Name == "Root");
        
        service.Simulated().SecurityModel().Model.Teams.Should().Contain(x =>
            x.Id != Guid.Empty &&
            x.Name == "Root");
    }
}
