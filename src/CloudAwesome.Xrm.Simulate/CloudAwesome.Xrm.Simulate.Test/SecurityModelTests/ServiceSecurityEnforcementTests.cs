using System;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.SecurityModelTests;

[TestFixture]
public class ServiceSecurityEnforcementTests
{
    private const string AccountLogicalName = "account";
    private const string ContactLogicalName = "contact";

    [Test]
    public void Create_Denies_When_Authenticated_User_Lacks_Create_Privilege()
    {
        var fixture = CreateFixture(role => role.CanRead(AccountLogicalName, PrivilegeDepthEnum.User));

        var create = () => fixture.Service.Create(new Entity(AccountLogicalName));

        create.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void Retrieve_Denies_Record_Not_Accessible_At_User_Depth()
    {
        var fixture = CreateFixture(role => role.CanRead(AccountLogicalName, PrivilegeDepthEnum.User));
        var otherRecord = fixture.AddAccount(fixture.OtherUser);

        var retrieve = () => fixture.Service.Retrieve(AccountLogicalName, otherRecord.Id, new ColumnSet(true));

        retrieve.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void RetrieveRequest_Uses_Same_Read_Security_As_Direct_Retrieve()
    {
        var fixture = CreateFixture(role => role.CanRead(AccountLogicalName, PrivilegeDepthEnum.User));
        var otherRecord = fixture.AddAccount(fixture.OtherUser);

        var retrieve = () => fixture.Service.Execute(new RetrieveRequest
        {
            Target = otherRecord.ToEntityReference(),
            ColumnSet = new ColumnSet(true)
        });

        retrieve.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void RetrieveMultiple_Filters_Inaccessible_Rows()
    {
        var fixture = CreateFixture(role => role.CanRead(AccountLogicalName, PrivilegeDepthEnum.User));
        var ownRecord = fixture.AddAccount(fixture.ActingUser);
        fixture.AddAccount(fixture.OtherUser);

        var results = fixture.Service.RetrieveMultiple(new QueryExpression(AccountLogicalName)
        {
            ColumnSet = new ColumnSet(true)
        });

        results.Entities.Select(x => x.Id).Should().BeEquivalentTo([ownRecord.Id]);
    }

    [Test]
    public void RetrieveMultipleRequest_Filters_Inaccessible_Rows()
    {
        var fixture = CreateFixture(role => role.CanRead(AccountLogicalName, PrivilegeDepthEnum.User));
        var ownRecord = fixture.AddAccount(fixture.ActingUser);
        fixture.AddAccount(fixture.OtherUser);

        var response = (RetrieveMultipleResponse)fixture.Service.Execute(new RetrieveMultipleRequest
        {
            Query = new QueryExpression(AccountLogicalName)
            {
                ColumnSet = new ColumnSet(true)
            }
        });

        response.EntityCollection.Entities.Select(x => x.Id).Should().BeEquivalentTo([ownRecord.Id]);
    }

    [Test]
    public void Update_Denies_Record_Not_Accessible_At_User_Depth()
    {
        var fixture = CreateFixture(role => role.CanWrite(AccountLogicalName, PrivilegeDepthEnum.User));
        var otherRecord = fixture.AddAccount(fixture.OtherUser);

        var update = () => fixture.Service.Update(new Entity(AccountLogicalName, otherRecord.Id)
        {
            ["name"] = "Updated"
        });

        update.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void DeleteRequest_Denies_Record_Not_Accessible_At_User_Depth()
    {
        var fixture = CreateFixture(role => role.CanDelete(AccountLogicalName, PrivilegeDepthEnum.User));
        var otherRecord = fixture.AddAccount(fixture.OtherUser);

        var delete = () => fixture.Service.Execute(new DeleteRequest
        {
            Target = otherRecord.ToEntityReference()
        });

        delete.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void AssignRequest_Denies_Record_Not_Accessible_At_User_Depth()
    {
        var fixture = CreateFixture(role => role.CanAssign(AccountLogicalName, PrivilegeDepthEnum.User));
        var otherRecord = fixture.AddAccount(fixture.OtherUser);

        var assign = () => fixture.Service.Execute(new AssignRequest
        {
            Target = otherRecord.ToEntityReference(),
            Assignee = fixture.ActingUser
        });

        assign.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void Associate_Denies_When_Related_Record_Lacks_Append_Access()
    {
        var fixture = CreateFixture(role => role
            .CanAppendTo(AccountLogicalName, PrivilegeDepthEnum.Organization)
            .CanAppend(ContactLogicalName, PrivilegeDepthEnum.User));
        var account = fixture.AddAccount(fixture.ActingUser);
        var contact = fixture.AddContact(fixture.OtherUser);

        var associate = () => fixture.Service.Associate(
            AccountLogicalName,
            account.Id,
            new Relationship("account_contact"),
            [contact.ToEntityReference()]);

        associate.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    [Test]
    public void DisassociateRequest_Denies_When_Target_Record_Lacks_AppendTo_Access()
    {
        var fixture = CreateFixture(role => role
            .CanAppendTo(AccountLogicalName, PrivilegeDepthEnum.User)
            .CanAppend(ContactLogicalName, PrivilegeDepthEnum.Organization));
        var account = fixture.AddAccount(fixture.OtherUser);
        var contact = fixture.AddContact(fixture.ActingUser);

        var disassociate = () => fixture.Service.Execute(new DisassociateRequest
        {
            Target = account.ToEntityReference(),
            Relationship = new Relationship("account_contact"),
            RelatedEntities = [contact.ToEntityReference()]
        });

        disassociate.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    private static SecurityFixture CreateFixture(Action<SimulatedSecurityRole> configureRole)
    {
        var businessUnitId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var authenticatedUser = new Entity("systemuser", actingUserId)
        {
            ["businessunitid"] = new EntityReference("businessunit", businessUnitId)
        };
        var security = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(actingUserId, businessUnitId)
            .WithUser(otherUserId, businessUnitId)
            .WithRole("Test Role", configureRole)
            .AssignRoleToUser("Test Role", actingUserId);

        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            AuthenticatedUser = authenticatedUser,
            SimulatedSecurityModel = security
        });

        return new SecurityFixture(
            service,
            new EntityReference("systemuser", actingUserId),
            new EntityReference("systemuser", otherUserId));
    }

    private sealed record SecurityFixture(
        IOrganizationService Service,
        EntityReference ActingUser,
        EntityReference OtherUser)
    {
        public Entity AddAccount(EntityReference owner) => AddRecord(AccountLogicalName, owner);

        public Entity AddContact(EntityReference owner) => AddRecord(ContactLogicalName, owner);

        private Entity AddRecord(string logicalName, EntityReference owner)
        {
            var record = new Entity(logicalName, Guid.NewGuid())
            {
                ["ownerid"] = owner,
                ["name"] = $"{logicalName} record"
            };

            Service.Simulated().Data().Add(record);
            return record;
        }
    }
}
