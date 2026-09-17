using System;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.SecurityModelTests;

[TestFixture]
public class SecurityRequestHandlerTests
{
    private const string AccountLogicalName = "account";

    [Test]
    public void GrantAccessRequest_Adds_Principal_Object_Access_And_Read_Security_Uses_It()
    {
        var fixture = CreateFixture(role => role
            .CanRead(AccountLogicalName, PrivilegeDepthEnum.User)
            .CanShare(AccountLogicalName, PrivilegeDepthEnum.Organization));
        var account = fixture.AddAccount(fixture.OwnerUser);

        var response = fixture.Service.Execute(new GrantAccessRequest
        {
            Target = account.ToEntityReference(),
            PrincipalAccess = new PrincipalAccess
            {
                Principal = fixture.ActingUser,
                AccessMask = AccessRights.ReadAccess
            }
        });

        response.ResponseName.Should().Be("GrantAccess");
        fixture.Service.Retrieve(AccountLogicalName, account.Id, new ColumnSet(true)).Id.Should().Be(account.Id);
    }

    [Test]
    public void ModifyAccessRequest_Replaces_Existing_Access_Mask()
    {
        var fixture = CreateFixture(role => role
            .CanRead(AccountLogicalName, PrivilegeDepthEnum.User)
            .CanWrite(AccountLogicalName, PrivilegeDepthEnum.User)
            .CanShare(AccountLogicalName, PrivilegeDepthEnum.Organization));
        var account = fixture.AddAccount(fixture.OwnerUser);
        fixture.Security.WithPrincipalObjectAccess(
            account.ToEntityReference(),
            fixture.ActingUser,
            AccessRights.ReadAccess | AccessRights.WriteAccess);

        var response = fixture.Service.Execute(new ModifyAccessRequest
        {
            Target = account.ToEntityReference(),
            PrincipalAccess = new PrincipalAccess
            {
                Principal = fixture.ActingUser,
                AccessMask = AccessRights.ReadAccess
            }
        });

        response.ResponseName.Should().Be("ModifyAccess");
        fixture.Security.GetPrincipalAccess(account.ToEntityReference(), fixture.ActingUser)
            .Should()
            .Be(AccessRights.ReadAccess);
    }

    [Test]
    public void RevokeAccessRequest_Removes_Principal_Object_Access()
    {
        var fixture = CreateFixture(role => role
            .CanRead(AccountLogicalName, PrivilegeDepthEnum.User)
            .CanShare(AccountLogicalName, PrivilegeDepthEnum.Organization));
        var account = fixture.AddAccount(fixture.OwnerUser);
        fixture.Security.WithPrincipalObjectAccess(
            account.ToEntityReference(),
            fixture.ActingUser,
            AccessRights.ReadAccess);

        var response = fixture.Service.Execute(new RevokeAccessRequest
        {
            Target = account.ToEntityReference(),
            Revokee = fixture.ActingUser
        });

        response.ResponseName.Should().Be("RevokeAccess");
        fixture.Security.GetPrincipalAccess(account.ToEntityReference(), fixture.ActingUser)
            .Should()
            .Be(default(AccessRights));
    }

    [Test]
    public void RetrievePrincipalAccessRequest_Returns_Effective_Record_Access()
    {
        var fixture = CreateFixture(role => role
            .CanRead(AccountLogicalName, PrivilegeDepthEnum.User)
            .CanShare(AccountLogicalName, PrivilegeDepthEnum.Organization));
        var account = fixture.AddAccount(fixture.OwnerUser);
        fixture.Security.WithPrincipalObjectAccess(
            account.ToEntityReference(),
            fixture.ActingUser,
            AccessRights.ReadAccess);

        var response = (RetrievePrincipalAccessResponse)fixture.Service.Execute(new RetrievePrincipalAccessRequest
        {
            Target = account.ToEntityReference(),
            Principal = fixture.ActingUser
        });

        response.ResponseName.Should().Be("RetrievePrincipalAccess");
        response.AccessRights.Should().HaveFlag(AccessRights.ReadAccess);
        response.AccessRights.Should().NotHaveFlag(AccessRights.WriteAccess);
    }

    [Test]
    public void RetrieveSharedPrincipalsAndAccessRequest_Returns_Explicit_Shares()
    {
        var fixture = CreateFixture(role => role.CanShare(AccountLogicalName, PrivilegeDepthEnum.Organization));
        var account = fixture.AddAccount(fixture.OwnerUser);
        fixture.Security.WithPrincipalObjectAccess(
            account.ToEntityReference(),
            fixture.ActingUser,
            AccessRights.ReadAccess | AccessRights.WriteAccess);

        var response = (RetrieveSharedPrincipalsAndAccessResponse)fixture.Service.Execute(
            new RetrieveSharedPrincipalsAndAccessRequest
            {
                Target = account.ToEntityReference()
            });

        response.ResponseName.Should().Be("RetrieveSharedPrincipalsAndAccess");
        response.PrincipalAccesses.Should().ContainSingle();
        response.PrincipalAccesses.Single().Principal.Should().Be(fixture.ActingUser);
        response.PrincipalAccesses.Single().AccessMask.Should().Be(AccessRights.ReadAccess | AccessRights.WriteAccess);
    }

    [Test]
    public void AddMembersTeamRequest_Adds_Team_Membership_That_Contributes_Role_Privileges()
    {
        var fixture = CreateFixture(_ => { }, configureSecurity: security =>
        {
            security
                .WithOwnerTeam(TestIds.TeamId, TestIds.BusinessUnitId)
                .WithRole("Team Reader", role => role.CanRead(AccountLogicalName, PrivilegeDepthEnum.Organization))
                .AssignRoleToTeam("Team Reader", TestIds.TeamId);
        });
        var account = fixture.AddAccount(fixture.OwnerUser);

        var deniedBeforeMembership = () => fixture.Service.Retrieve(AccountLogicalName, account.Id, new ColumnSet(true));
        deniedBeforeMembership.Should().Throw<FaultException<OrganizationServiceFault>>();

        var response = fixture.Service.Execute(new AddMembersTeamRequest
        {
            TeamId = TestIds.TeamId,
            MemberIds = [fixture.ActingUser.Id]
        });

        response.ResponseName.Should().Be("AddMembersTeam");
        fixture.Service.Retrieve(AccountLogicalName, account.Id, new ColumnSet(true)).Id.Should().Be(account.Id);
    }

    [Test]
    public void RemoveMembersTeamRequest_Removes_Team_Membership()
    {
        var fixture = CreateFixture(_ => { }, configureSecurity: security =>
        {
            security
                .WithOwnerTeam(TestIds.TeamId, TestIds.BusinessUnitId)
                .WithTeamMember(TestIds.TeamId, TestIds.ActingUserId)
                .WithRole("Team Reader", role => role.CanRead(AccountLogicalName, PrivilegeDepthEnum.Organization))
                .AssignRoleToTeam("Team Reader", TestIds.TeamId);
        });

        var response = fixture.Service.Execute(new RemoveMembersTeamRequest
        {
            TeamId = TestIds.TeamId,
            MemberIds = [fixture.ActingUser.Id]
        });

        response.ResponseName.Should().Be("RemoveMembersTeam");
        fixture.Security.GetTeamIdsForUser(fixture.ActingUser.Id).Should().BeEmpty();
    }

    [Test]
    public void GrantAccessRequest_Denies_When_Authenticated_User_Lacks_Share_Access()
    {
        var fixture = CreateFixture(role => role.CanRead(AccountLogicalName, PrivilegeDepthEnum.Organization));
        var account = fixture.AddAccount(fixture.OwnerUser);

        var grant = () => fixture.Service.Execute(new GrantAccessRequest
        {
            Target = account.ToEntityReference(),
            PrincipalAccess = new PrincipalAccess
            {
                Principal = fixture.ActingUser,
                AccessMask = AccessRights.ReadAccess
            }
        });

        grant.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    private static SecurityFixture CreateFixture(
        Action<SimulatedSecurityRole> configureRole,
        Action<SimulatedSecurityModel>? configureSecurity = null)
    {
        var authenticatedUser = new Entity("systemuser", TestIds.ActingUserId)
        {
            ["businessunitid"] = new EntityReference("businessunit", TestIds.BusinessUnitId)
        };
        var security = SimulatedSecurityModel.Create();
        security.IgnoreMissingEntities = false;
        security
            .WithBusinessUnit(TestIds.BusinessUnitId, "Root")
            .WithUser(TestIds.ActingUserId, TestIds.BusinessUnitId)
            .WithUser(TestIds.OwnerUserId, TestIds.BusinessUnitId)
            .WithRole("User Role", configureRole)
            .AssignRoleToUser("User Role", TestIds.ActingUserId);

        configureSecurity?.Invoke(security);

        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            AuthenticatedUser = authenticatedUser,
            SimulatedSecurityModel = security
        });

        return new SecurityFixture(
            service,
            security,
            new EntityReference("systemuser", TestIds.ActingUserId),
            new EntityReference("systemuser", TestIds.OwnerUserId));
    }

    private sealed record SecurityFixture(
        IOrganizationService Service,
        SimulatedSecurityModel Security,
        EntityReference ActingUser,
        EntityReference OwnerUser)
    {
        public Entity AddAccount(EntityReference owner)
        {
            var record = new Entity(AccountLogicalName, Guid.NewGuid())
            {
                ["ownerid"] = owner,
                ["name"] = "Test account"
            };

            Service.Simulated().Data().Add(record);
            return record;
        }
    }

    private static class TestIds
    {
        public static readonly Guid BusinessUnitId = Guid.Parse("00000000-0000-0000-0000-000000000101");
        public static readonly Guid ActingUserId = Guid.Parse("00000000-0000-0000-0000-000000000102");
        public static readonly Guid OwnerUserId = Guid.Parse("00000000-0000-0000-0000-000000000103");
        public static readonly Guid TeamId = Guid.Parse("00000000-0000-0000-0000-000000000104");
    }
}
