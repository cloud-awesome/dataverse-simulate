using System;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.SecurityModelTests;

[TestFixture]
public class SecurityRoleBusinessUnitReplicationTests
{
    private const string RoleLogicalName = "role";
    private const string TeamLogicalName = "team";
    private const string SystemUserLogicalName = "systemuser";
    private const string AccountLogicalName = "account";
    private const string RoleName = "Basic User";
    private static readonly Relationship TeamRolesRelationship = new("teamroles_association");
    private static readonly Relationship SystemUserRolesRelationship = new("systemuserroles_association");

    [Test]
    public void Security_Roles_Are_Replicated_Across_Business_Unit_Hierarchy()
    {
        var ids = HierarchyIds.Create();
        var service = CreateService(ids);

        var roles = RetrieveRoles(service);

        roles.Should().HaveCount(4);
        roles.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        roles.Should().OnlyContain(x => x.GetAttributeValue<string>("name") == RoleName);

        var rootRole = RoleForBusinessUnit(roles, ids.Root);
        var childRole = RoleForBusinessUnit(roles, ids.Child);
        var grandchildRole = RoleForBusinessUnit(roles, ids.Grandchild);
        var siblingRole = RoleForBusinessUnit(roles, ids.Sibling);

        childRole.GetAttributeValue<EntityReference>("parentrootroleid").Id.Should().Be(rootRole.Id);
        childRole.GetAttributeValue<EntityReference>("parentroleid").Id.Should().Be(rootRole.Id);
        grandchildRole.GetAttributeValue<EntityReference>("parentrootroleid").Id.Should().Be(rootRole.Id);
        grandchildRole.GetAttributeValue<EntityReference>("parentroleid").Id.Should().Be(childRole.Id);
        siblingRole.GetAttributeValue<EntityReference>("parentrootroleid").Id.Should().Be(rootRole.Id);
        siblingRole.GetAttributeValue<EntityReference>("parentroleid").Id.Should().Be(rootRole.Id);
    }

    [Test]
    public void Configured_Role_Assignments_Use_Principals_Business_Unit_Role_Instance()
    {
        var ids = HierarchyIds.Create();
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var securityModel = CreateSecurityModel(ids)
            .WithOwnerTeam(teamId, ids.Child, "Child Team")
            .WithUser(userId, ids.Sibling, "Sibling User")
            .AssignRoleToTeam(RoleName, teamId)
            .AssignRoleToUser(RoleName, userId);
        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            SimulatedSecurityModel = securityModel
        });
        var roles = RetrieveRoles(service);

        var childRole = RoleForBusinessUnit(roles, ids.Child);
        var siblingRole = RoleForBusinessUnit(roles, ids.Sibling);

        service.Simulated().Data().Get("teamroles").Should().ContainSingle(x =>
            x.GetAttributeValue<Guid>("teamid") == teamId &&
            x.GetAttributeValue<Guid>("roleid") == childRole.Id);
        service.Simulated().Data().Get("systemuserroles").Should().ContainSingle(x =>
            x.GetAttributeValue<Guid>("systemuserid") == userId &&
            x.GetAttributeValue<Guid>("roleid") == siblingRole.Id);
    }

    [Test]
    public void Adding_Business_Unit_After_Role_Replicates_Existing_Role_To_New_Business_Unit()
    {
        var rootBusinessUnitId = Guid.NewGuid();
        var childBusinessUnitId = Guid.NewGuid();
        var service = ((IOrganizationService)null!).Simulate();

        service.Simulated().SecurityModel()
            .WithBusinessUnit(rootBusinessUnitId, "Root")
            .WithRole(RoleName)
            .WithBusinessUnit(childBusinessUnitId, "Child", rootBusinessUnitId);

        var roles = RetrieveRoles(service);

        roles.Should().HaveCount(2);
        RoleForBusinessUnit(roles, rootBusinessUnitId).Id.Should().NotBeEmpty();
        RoleForBusinessUnit(roles, childBusinessUnitId).Id.Should().NotBeEmpty();
        RoleForBusinessUnit(roles, childBusinessUnitId).Id.Should().NotBe(RoleForBusinessUnit(roles, rootBusinessUnitId).Id);
    }

    [Test]
    public void Associate_And_Disassociate_Team_Role_Uses_Role_Instance_From_Teams_Business_Unit()
    {
        var ids = HierarchyIds.Create();
        var teamId = Guid.NewGuid();
        var securityModel = CreateSecurityModel(ids)
            .WithOwnerTeam(teamId, ids.Grandchild, "Grandchild Team");
        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            SimulatedSecurityModel = securityModel
        });
        var grandchildRole = RoleForBusinessUnit(RetrieveRoles(service), ids.Grandchild);

        service.Associate(
            TeamLogicalName,
            teamId,
            TeamRolesRelationship,
            [grandchildRole.ToEntityReference()]);

        securityModel.GetEffectiveEntityPermissionForPrincipal(new EntityReference(TeamLogicalName, teamId), AccountLogicalName)!
            .Read
            .Should()
            .Be(PrivilegeDepthEnum.Organization);
        service.Simulated().Data().Get("teamroles").Should().ContainSingle(x =>
            x.GetAttributeValue<Guid>("teamid") == teamId &&
            x.GetAttributeValue<Guid>("roleid") == grandchildRole.Id);

        service.Disassociate(
            TeamLogicalName,
            teamId,
            TeamRolesRelationship,
            [grandchildRole.ToEntityReference()]);

        securityModel.GetEffectiveEntityPermissionForPrincipal(new EntityReference(TeamLogicalName, teamId), AccountLogicalName)
            .Should()
            .BeNull();
        service.Simulated().Data().Get("teamroles").Should().BeEmpty();
    }

    [Test]
    public void Associate_SystemUser_Role_Uses_Role_Instance_From_Users_Business_Unit()
    {
        var ids = HierarchyIds.Create();
        var userId = Guid.NewGuid();
        var securityModel = CreateSecurityModel(ids)
            .WithUser(userId, ids.Sibling, "Sibling User");
        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            SimulatedSecurityModel = securityModel
        });
        var siblingRole = RoleForBusinessUnit(RetrieveRoles(service), ids.Sibling);

        service.Associate(
            SystemUserLogicalName,
            userId,
            SystemUserRolesRelationship,
            [siblingRole.ToEntityReference()]);

        securityModel.GetEffectiveEntityPermissionForUser(userId, AccountLogicalName)!
            .Read
            .Should()
            .Be(PrivilegeDepthEnum.Organization);
        service.Simulated().Data().Get("systemuserroles").Should().ContainSingle(x =>
            x.GetAttributeValue<Guid>("systemuserid") == userId &&
            x.GetAttributeValue<Guid>("roleid") == siblingRole.Id);
    }

    [Test]
    public void Associate_Rejects_Role_Instance_From_Different_Business_Unit()
    {
        var ids = HierarchyIds.Create();
        var teamId = Guid.NewGuid();
        var securityModel = CreateSecurityModel(ids)
            .WithOwnerTeam(teamId, ids.Grandchild, "Grandchild Team");
        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            SimulatedSecurityModel = securityModel
        });
        var rootRole = RoleForBusinessUnit(RetrieveRoles(service), ids.Root);

        var associate = () => service.Associate(
            TeamLogicalName,
            teamId,
            TeamRolesRelationship,
            [rootRole.ToEntityReference()]);

        associate.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
        securityModel.RoleAssignments.Should().BeEmpty();
        service.Simulated().Data().Get("teamroles").Should().BeEmpty();
    }

    private static IOrganizationService CreateService(HierarchyIds ids)
    {
        return ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            SimulatedSecurityModel = CreateSecurityModel(ids)
        });
    }

    private static SimulatedSecurityModel CreateSecurityModel(HierarchyIds ids)
    {
        return SimulatedSecurityModel.Create()
            .WithBusinessUnit(ids.Root, "Root")
            .WithBusinessUnit(ids.Child, "Child", ids.Root)
            .WithBusinessUnit(ids.Grandchild, "Grandchild", ids.Child)
            .WithBusinessUnit(ids.Sibling, "Sibling", ids.Root)
            .WithRole(RoleName, role => role.CanRead(AccountLogicalName, PrivilegeDepthEnum.Organization));
    }

    private static DataCollection<Entity> RetrieveRoles(IOrganizationService service)
    {
        return service.RetrieveMultiple(new QueryExpression(RoleLogicalName)
        {
            ColumnSet = new ColumnSet("roleid", "name", "businessunitid", "parentrootroleid", "parentroleid")
        }).Entities;
    }

    private static Entity RoleForBusinessUnit(DataCollection<Entity> roles, Guid businessUnitId)
    {
        return roles.Single(x => x.GetAttributeValue<EntityReference>("businessunitid").Id == businessUnitId);
    }

    private sealed record HierarchyIds(Guid Root, Guid Child, Guid Grandchild, Guid Sibling)
    {
        public static HierarchyIds Create()
        {
            return new HierarchyIds(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        }
    }
}
