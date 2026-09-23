using System;
using System.Collections.Generic;
using System.Linq;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.SecurityModelTests;

[TestFixture]
public class PermissionsCalculatorTests
{
    [Test]
    public void ValidateEntityPermission_Returns_True_If_No_Model_Is_Defined()
    {
        var options = new SimulatorOptions();

        var result = PermissionsCalculator.ValidateEntityPermission("contact", "Create", options);

        result.Should().BeTrue();
    }

    [Test]
    public void ValidateEntityPermission_Returns_True_If_Entity_Not_Defined_But_Ignored()
    {
        var options = new SimulatorOptions
        {
            SimulatedSecurityModel = new SimulatedSecurityModel
            {
                IgnoreMissingEntities = true
            }
        };

        var result = PermissionsCalculator.ValidateEntityPermission("contact", "Create", options);

        result.Should().BeTrue();
    }

    [Test]
    public void ValidateEntityPermission_Returns_False_If_Entity_Not_Defined_And_Not_Ignored()
    {
        var options = new SimulatorOptions
        {
            SimulatedSecurityModel = new SimulatedSecurityModel
            {
                IgnoreMissingEntities = false
            }
        };

        var result = PermissionsCalculator.ValidateEntityPermission("contact", "Create", options);

        result.Should().BeFalse();
    }

    [Test]
    public void ValidateEntityPermission_Throws_Exception_If_Message_Not_Recognised()
    {
        var options = new SimulatorOptions
        {
            SimulatedSecurityModel = new SimulatedSecurityModel()
        };

        var sut = () => PermissionsCalculator.ValidateEntityPermission("contact", "Dummy", options);

        sut.Should()
            .Throw<ArgumentException>()
            .WithMessage("The message to validate is not recognised: Dummy");
    }

    [Test]
    public void CanPerform_Allows_Direct_User_Role_With_Privilege()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId)
            .WithRole("Creator", role => role.CanCreate("contact", PrivilegeDepthEnum.User))
            .AssignRoleToUser("Creator", userId);

        var decision = PermissionsCalculator.CanPerform(
            new SecurityEvaluationContext(model),
            User(userId),
            "contact",
            SecurityPrivilege.Create);

        decision.Allowed.Should().BeTrue();
        decision.EffectiveDepth.Should().Be(PrivilegeDepthEnum.User);
    }

    [Test]
    public void CanPerform_Denies_When_Privilege_Depth_Is_None()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId)
            .WithRole("No Read", role => role.CanRead("contact", PrivilegeDepthEnum.None))
            .AssignRoleToUser("No Read", userId);

        var decision = PermissionsCalculator.CanPerform(
            new SecurityEvaluationContext(model),
            User(userId),
            "contact",
            SecurityPrivilege.Read);

        decision.Allowed.Should().BeFalse();
        decision.EffectiveDepth.Should().Be(PrivilegeDepthEnum.None);
        decision.DenialReason.Should().Contain("does not have Read privilege");
    }

    [Test]
    public void CanAccessRecord_User_Depth_Allows_Own_Record_And_Denies_Other_User_Record()
    {
        var businessUnitId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(actingUserId, businessUnitId)
            .WithUser(otherUserId, businessUnitId)
            .WithRole("Basic Read", role => role.CanRead("account", PrivilegeDepthEnum.User))
            .AssignRoleToUser("Basic Read", actingUserId);

        var context = new SecurityEvaluationContext(model);
        var ownRecord = Record("account", User(actingUserId));
        var otherRecord = Record("account", User(otherUserId));

        PermissionsCalculator.CanAccessRecord(context, User(actingUserId), ownRecord, SecurityPrivilege.Read)
            .Allowed
            .Should()
            .BeTrue();
        PermissionsCalculator.CanAccessRecord(context, User(actingUserId), otherRecord, SecurityPrivilege.Read)
            .Allowed
            .Should()
            .BeFalse();
    }

    [Test]
    public void CanAccessRecord_BusinessUnit_Depth_Allows_Same_Business_Unit_Only()
    {
        var rootBuId = Guid.NewGuid();
        var siblingBuId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var sameBuOwnerId = Guid.NewGuid();
        var siblingBuOwnerId = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(rootBuId, "Root")
            .WithBusinessUnit(siblingBuId, "Sibling", rootBuId)
            .WithUser(actingUserId, rootBuId)
            .WithUser(sameBuOwnerId, rootBuId)
            .WithUser(siblingBuOwnerId, siblingBuId)
            .WithRole("Local Read", role => role.CanRead("account", PrivilegeDepthEnum.BusinessUnit))
            .AssignRoleToUser("Local Read", actingUserId);

        var context = new SecurityEvaluationContext(model);

        PermissionsCalculator.CanAccessRecord(context, User(actingUserId), Record("account", User(sameBuOwnerId)), SecurityPrivilege.Read)
            .Allowed
            .Should()
            .BeTrue();
        PermissionsCalculator.CanAccessRecord(context, User(actingUserId), Record("account", User(siblingBuOwnerId)), SecurityPrivilege.Read)
            .Allowed
            .Should()
            .BeFalse();
    }

    [Test]
    public void CanAccessRecord_ParentChild_Depth_Allows_Descendant_Business_Unit()
    {
        var rootBuId = Guid.NewGuid();
        var childBuId = Guid.NewGuid();
        var grandchildBuId = Guid.NewGuid();
        var siblingBuId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var grandchildOwnerId = Guid.NewGuid();
        var siblingOwnerId = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(rootBuId, "Root")
            .WithBusinessUnit(childBuId, "Child", rootBuId)
            .WithBusinessUnit(grandchildBuId, "Grandchild", childBuId)
            .WithBusinessUnit(siblingBuId, "Sibling", rootBuId)
            .WithUser(actingUserId, childBuId)
            .WithUser(grandchildOwnerId, grandchildBuId)
            .WithUser(siblingOwnerId, siblingBuId)
            .WithRole("Deep Read", role => role.CanRead("account", PrivilegeDepthEnum.ParentChild))
            .AssignRoleToUser("Deep Read", actingUserId);

        var context = new SecurityEvaluationContext(model);

        PermissionsCalculator.CanAccessRecord(context, User(actingUserId), Record("account", User(grandchildOwnerId)), SecurityPrivilege.Read)
            .Allowed
            .Should()
            .BeTrue();
        PermissionsCalculator.CanAccessRecord(context, User(actingUserId), Record("account", User(siblingOwnerId)), SecurityPrivilege.Read)
            .Allowed
            .Should()
            .BeFalse();
    }

    [Test]
    public void CanAccessRecord_Organization_Depth_Allows_All_Records_For_Table()
    {
        var actingBusinessUnitId = Guid.NewGuid();
        var otherBusinessUnitId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(actingBusinessUnitId, "Acting")
            .WithBusinessUnit(otherBusinessUnitId, "Other", actingBusinessUnitId)
            .WithUser(actingUserId, actingBusinessUnitId)
            .WithUser(otherUserId, otherBusinessUnitId)
            .WithRole("Global Read", role => role.CanRead("account", PrivilegeDepthEnum.Organization))
            .AssignRoleToUser("Global Read", actingUserId);

        var decision = PermissionsCalculator.CanAccessRecord(
            new SecurityEvaluationContext(model),
            User(actingUserId),
            Record("account", User(otherUserId)),
            SecurityPrivilege.Read);

        decision.Allowed.Should().BeTrue();
        decision.EffectiveDepth.Should().Be(PrivilegeDepthEnum.Organization);
    }

    [Test]
    public void CanAccessRecord_User_Depth_Allows_Record_Owned_By_User_Owner_Team()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId)
            .WithOwnerTeam(teamId, businessUnitId)
            .WithTeamMember(teamId, userId)
            .WithRole("Basic Read", role => role.CanRead("account", PrivilegeDepthEnum.User))
            .AssignRoleToUser("Basic Read", userId);

        var decision = PermissionsCalculator.CanAccessRecord(
            new SecurityEvaluationContext(model),
            User(userId),
            Record("account", Team(teamId)),
            SecurityPrivilege.Read);

        decision.Allowed.Should().BeTrue();
    }

    [Test]
    public void CanAccessRecord_Team_Role_Contributes_To_Member_Effective_Privileges()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId)
            .WithUser(ownerId, businessUnitId)
            .WithOwnerTeam(teamId, businessUnitId)
            .WithTeamMember(teamId, userId)
            .WithRole("Team Read", role => role.CanRead("account", PrivilegeDepthEnum.BusinessUnit))
            .AssignRoleToTeam("Team Read", teamId);

        var decision = PermissionsCalculator.CanAccessRecord(
            new SecurityEvaluationContext(model),
            User(userId),
            Record("account", User(ownerId)),
            SecurityPrivilege.Read);

        decision.Allowed.Should().BeTrue();
        decision.EffectiveDepth.Should().Be(PrivilegeDepthEnum.BusinessUnit);
    }

    [Test]
    public void CanAccessRecord_Explicit_User_Share_Allows_Record_Access_When_Table_Privilege_Exists()
    {
        var businessUnitId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var record = Record("account", User(ownerUserId));
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(actingUserId, businessUnitId)
            .WithUser(ownerUserId, businessUnitId)
            .WithRole("Basic Read", role => role.CanRead("account", PrivilegeDepthEnum.User))
            .AssignRoleToUser("Basic Read", actingUserId)
            .WithPrincipalObjectAccess(record.ToEntityReference(), User(actingUserId), AccessRights.ReadAccess);

        var decision = PermissionsCalculator.CanAccessRecord(
            new SecurityEvaluationContext(model),
            User(actingUserId),
            record,
            SecurityPrivilege.Read);

        decision.Allowed.Should().BeTrue();
    }

    [Test]
    public void CanAccessRecord_Explicit_Team_Share_Allows_Record_Access_For_Team_Member()
    {
        var businessUnitId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var record = Record("account", User(ownerUserId));
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(actingUserId, businessUnitId)
            .WithUser(ownerUserId, businessUnitId)
            .WithOwnerTeam(teamId, businessUnitId)
            .WithTeamMember(teamId, actingUserId)
            .WithRole("Basic Read", role => role.CanRead("account", PrivilegeDepthEnum.User))
            .AssignRoleToUser("Basic Read", actingUserId)
            .WithPrincipalObjectAccess(record.ToEntityReference(), Team(teamId), AccessRights.ReadAccess);

        var decision = PermissionsCalculator.CanAccessRecord(
            new SecurityEvaluationContext(model),
            User(actingUserId),
            record,
            SecurityPrivilege.Read);

        decision.Allowed.Should().BeTrue();
    }

    [Test]
    public void FilterReadableRecords_Returns_Only_Accessible_Rows()
    {
        var businessUnitId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(actingUserId, businessUnitId)
            .WithUser(otherUserId, businessUnitId)
            .WithRole("Basic Read", role => role.CanRead("account", PrivilegeDepthEnum.User))
            .AssignRoleToUser("Basic Read", actingUserId);
        var accessibleRecord = Record("account", User(actingUserId));
        var inaccessibleRecord = Record("account", User(otherUserId));

        var results = PermissionsCalculator.FilterReadableRecords(
            new SecurityEvaluationContext(model),
            User(actingUserId),
            "account",
            [accessibleRecord, inaccessibleRecord]);

        results.Should().BeEquivalentTo([accessibleRecord]);
    }

    private static EntityReference User(Guid userId) => new("systemuser", userId);

    private static EntityReference Team(Guid teamId) => new("team", teamId);

    private static Entity Record(string logicalName, EntityReference owner) =>
        new(logicalName)
        {
            Id = Guid.NewGuid(),
            Attributes =
            {
                ["ownerid"] = owner
            }
        };
}
