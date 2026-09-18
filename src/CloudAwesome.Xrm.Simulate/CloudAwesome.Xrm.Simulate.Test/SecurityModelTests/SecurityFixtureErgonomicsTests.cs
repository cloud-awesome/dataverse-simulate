using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
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
}
