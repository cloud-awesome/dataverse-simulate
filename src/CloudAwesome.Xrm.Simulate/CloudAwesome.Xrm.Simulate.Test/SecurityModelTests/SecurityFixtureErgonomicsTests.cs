using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.SecurityModelTests;

[TestFixture]
public class SecurityFixtureErgonomicsTests
{
    [Test]
    public void SimulatedSecurityModel_From_Options_Seeds_Principal_Rows()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var securityModel = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId, "Security User")
            .WithOwnerTeam(teamId, businessUnitId, "Security Team");

        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            SimulatedSecurityModel = securityModel
        });

        service.Simulated().Data().Get("businessunit").Should().Contain(x => x.Id == businessUnitId);
        service.Simulated().Data().Get("systemuser").Should().Contain(x =>
            x.Id == userId &&
            x.GetAttributeValue<string>("fullname") == "Security User");
        service.Simulated().Data().Get("team").Should().Contain(x =>
            x.Id == teamId &&
            x.GetAttributeValue<string>("name") == "Security Team");
    }

    [Test]
    public void SimulatedSecurityModel_From_Options_Seeds_Role_And_Relationship_Rows()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var securityModel = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId, "Security User")
            .WithOwnerTeam(teamId, businessUnitId, "Security Team")
            .WithTeamMember(teamId, userId)
            .WithRole("Reader", role => role.CanRead("account", PrivilegeDepthEnum.Organization))
            .AssignRoleToUser("Reader", userId)
            .AssignRoleToTeam("Reader", teamId);

        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            SimulatedSecurityModel = securityModel
        });

        service.Simulated().Data().Get("role").Should().ContainSingle(x =>
            x.GetAttributeValue<string>("name") == "Reader" &&
            x.GetAttributeValue<Guid>("roleid") != Guid.Empty &&
            x.GetAttributeValue<EntityReference>("businessunitid").Id == businessUnitId);
        service.Simulated().Data().Get("systemuserroles").Should().ContainSingle(x =>
            x.GetAttributeValue<EntityReference>("systemuserid").Id == userId);
        service.Simulated().Data().Get("teamroles").Should().ContainSingle(x =>
            x.GetAttributeValue<EntityReference>("teamid").Id == teamId);
        service.Simulated().Data().Get("teammembership").Should().ContainSingle(x =>
            x.GetAttributeValue<EntityReference>("teamid").Id == teamId &&
            x.GetAttributeValue<EntityReference>("systemuserid").Id == userId);
    }

    [Test]
    public void SimulatedSecurityModel_Facade_Creates_Model_And_Upserts_Principal_Rows()
    {
        var businessUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = ((IOrganizationService)null!).Simulate();

        var security = service.Simulated()
            .SecurityModel()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(userId, businessUnitId, "Facade User")
            .WithRole("Reader", role => role.CanRead("account", PrivilegeDepthEnum.Organization))
            .AssignRoleToUser("Reader", userId)
            .Validate();

        service.Simulated().Data().Get("businessunit").Should().Contain(x => x.Id == businessUnitId);
        service.Simulated().Data().Get("systemuser").Should().Contain(x =>
            x.Id == userId &&
            x.GetAttributeValue<string>("fullname") == "Facade User");
        security.Model.GetEffectiveEntityPermissionForUser(userId, "account")!.Read
            .Should()
            .Be(PrivilegeDepthEnum.Organization);
    }

    [Test]
    public void SimulatedSecurityModel_Facade_Upserts_Roles_For_Normal_Queries()
    {
        var businessUnitId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var service = ((IOrganizationService)null!).Simulate();

        service.Simulated().SecurityModel()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithTeam(teamId, businessUnitId, "Delivery Team")
            .WithRole("Basic User");

        var roles = service.RetrieveMultiple(new QueryExpression("role")
        {
            ColumnSet = new ColumnSet("roleid", "name", "businessunitid")
        }).Entities;

        roles.Should().ContainSingle();
        roles.Single().Id.Should().NotBeEmpty();
        roles.Single().GetAttributeValue<Guid>("roleid").Should().Be(roles.Single().Id);
        roles.Single().GetAttributeValue<string>("name").Should().Be("Basic User");
        roles.Single().GetAttributeValue<EntityReference>("businessunitid").Id.Should().Be(businessUnitId);
    }
}
