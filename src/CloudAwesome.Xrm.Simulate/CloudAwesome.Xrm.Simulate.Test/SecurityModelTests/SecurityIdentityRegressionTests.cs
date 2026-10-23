using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.SecurityModelTests;

[TestFixture]
public sealed class SecurityIdentityRegressionTests
{
    [TestCase(false, JoinOperator.Inner)]
    [TestCase(false, JoinOperator.LeftOuter)]
    [TestCase(true, JoinOperator.Inner)]
    [TestCase(true, JoinOperator.LeftOuter)]
    public void Links_Filter_Unreadable_Rows_Before_Matching(bool fetch, JoinOperator join)
    {
        var f = new Fixture();
        var visible = f.Add("account", f.User);
        var hidden = f.Add("account", f.Other);
        f.Add("contact", f.User, visible);
        var parentWithHiddenChild = f.Add("contact", f.User, hidden);
        var query = JoinQuery(join);

        var results = f.Service.RetrieveMultiple(fetch ? FetchJoin(join) : query);

        results.Entities.Should().HaveCount(join == JoinOperator.Inner ? 1 : 2);
        results.Entities.SelectMany(e => e.Attributes.Values).OfType<AliasedValue>()
            .Select(v => v.Value).Should().NotContain(hidden.Id);
        if (join == JoinOperator.LeftOuter)
            results.Entities.Single(e => e.Id == parentWithHiddenChild.Id).Contains("a.accountid").Should().BeFalse();
        f.Service.Simulated().Data().Get("account").Should().HaveCount(2);
    }

    [TestCase(JoinOperator.Inner)]
    [TestCase(JoinOperator.LeftOuter)]
    public void Missing_Link_Table_Privilege_Does_Not_Expose_Linked_Attributes(JoinOperator join)
    {
        var f = new Fixture();
        f.Role.EntityPermissions.Single(p => p.LogicalName == "account").Read = PrivilegeDepthEnum.None;
        f.Add("contact", f.User, f.Add("account", f.User));
        var results = f.Service.RetrieveMultiple(JoinQuery(join));
        results.Entities.Should().HaveCount(join == JoinOperator.Inner ? 0 : 1);
        results.Entities.SelectMany(e => e.Attributes.Keys).Should().NotContain("a.accountid");
    }

    [Test]
    public void Nested_Links_Cannot_Use_Unreadable_Grandchildren()
    {
        var f = new Fixture();
        var hidden = f.Add("account", f.Other);
        var intermediate = f.Add("contact", f.User, hidden);
        var root = f.Add("contact", f.User);
        root["parentcontactid"] = intermediate.ToEntityReference();
        var query = new QueryExpression("contact") { ColumnSet = new ColumnSet("contactid") };
        query.Criteria.AddCondition("contactid", ConditionOperator.Equal, root.Id);
        var link = query.AddLink("contact", "parentcontactid", "contactid");
        link.AddLink("account", "parentcustomerid", "accountid");
        f.Service.RetrieveMultiple(query).Entities.Should().BeEmpty();
    }

    [Test]
    public void Self_Links_Filter_Each_Alias()
    {
        var f = new Fixture();
        var hidden = f.Add("account", f.Other);
        var own = f.Add("account", f.User);
        own["parentaccountid"] = hidden.ToEntityReference();
        var query = new QueryExpression("account") { ColumnSet = new ColumnSet("accountid") };
        query.AddLink("account", "parentaccountid", "accountid");
        f.Service.RetrieveMultiple(query).Entities.Should().BeEmpty();
    }

    [Test]
    public void Linked_Security_Precedes_Total_Count_Page_Slicing_And_Top()
    {
        var f = new Fixture();
        f.Add("contact", f.User, f.Add("account", f.Other));
        var visible = f.Add("contact", f.User, f.Add("account", f.User));
        var query = JoinQuery(JoinOperator.Inner);
        query.PageInfo = new PagingInfo { Count = 1, PageNumber = 1, ReturnTotalRecordCount = true };
        var results = f.Service.RetrieveMultiple(query);
        results.Entities.Should().ContainSingle().Which.Id.Should().Be(visible.Id);
        results.TotalRecordCount.Should().Be(1);
        results.MoreRecords.Should().BeFalse();
        query.TopCount = 1;
        query.PageInfo = new PagingInfo();
        f.Service.RetrieveMultiple(query).Entities.Should().ContainSingle().Which.Id.Should().Be(visible.Id);
    }

    [Test]
    public void Linked_Security_Precedes_Fetch_Aggregation()
    {
        var f = new Fixture();
        f.Add("contact", f.User, f.Add("account", f.Other));
        f.Add("contact", f.User, f.Add("account", f.User));
        var result = f.Service.RetrieveMultiple(new FetchExpression("<fetch aggregate='true'><entity name='contact'><attribute name='contactid' alias='n' aggregate='count'/><link-entity name='account' from='accountid' to='parentcustomerid'/></entity></fetch>"));
        result.Entities.Single().GetAttributeValue<AliasedValue>("n").Value.Should().Be(1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Linked_Reads_Respect_User_And_Team_Shares(bool teamShare)
    {
        var f = new Fixture();
        var account = f.Add("account", f.Other);
        f.Add("contact", f.User, account);
        var principal = teamShare ? f.Team : f.User;
        f.Model.GrantAccess(account.ToEntityReference(), principal, AccessRights.ReadAccess);
        f.Service.RetrieveMultiple(JoinQuery(JoinOperator.Inner)).Entities.Should().ContainSingle();
        f.Model.RevokeAccess(account.ToEntityReference(), principal);
        f.Service.RetrieveMultiple(JoinQuery(JoinOperator.Inner)).Entities.Should().BeEmpty();
    }

    [Test]
    public void Linked_Reads_Include_Member_Owner_Team_Records()
    {
        var f = new Fixture();
        f.Add("contact", f.User, f.Add("account", f.Team));
        f.Service.RetrieveMultiple(JoinQuery(JoinOperator.Inner)).Entities.Should().ContainSingle();
    }

    [Test]
    public void Security_Is_Optional_For_Linked_Reads()
    {
        var f = new Fixture(security: false);
        f.Add("contact", f.User, f.Add("account", f.Other));
        f.Service.RetrieveMultiple(JoinQuery(JoinOperator.Inner)).Entities.Should().ContainSingle();
    }

    [Test]
    public void Factory_Services_Isolate_Caller_Reads_And_WhoAmI_While_Sharing_Data()
    {
        var f = new Fixture();
        var mine = f.Add("account", f.User);
        var theirs = f.Add("account", f.Other);
        var first = f.Factory.CreateOrganizationService(f.User.Id);
        var second = f.Factory.CreateOrganizationService(f.Other.Id);
        first.RetrieveMultiple(new QueryExpression("account")).Entities.Select(e => e.Id).Should().Equal(mine.Id);
        second.RetrieveMultiple(new QueryExpression("account")).Entities.Select(e => e.Id).Should().Equal(theirs.Id);
        Identity(first).UserId.Should().Be(f.User.Id);
        Identity(second).UserId.Should().Be(f.User.Id);
        Identity(second).BusinessUnitId.Should().Be(f.RootBu);
        second.Simulated().Data().AuthenticatedUser.Should().Be(f.Other);
        second.Simulated().Data().BusinessUnit.Id.Should().Be(f.ChildBu);
        Identity(first).UserId.Should().Be(f.User.Id);
        f.Provider.Simulated().Data().AuthenticatedUser.Should().Be(f.User);
    }

    [Test]
    public void Factory_Caller_Drives_CreatedBy_ModifiedBy_And_Default_Owner()
    {
        var f = new Fixture();
        var other = f.Factory.CreateOrganizationService(f.Other.Id);
        var mine = f.Factory.CreateOrganizationService(f.User.Id);
        var id = other.Create(new Entity("account") { ["name"] = "created" });
        var stored = f.Provider.Simulated().Data().Get("account", id);
        stored.GetAttributeValue<EntityReference>("createdby").Should().Be(f.Other);
        stored.GetAttributeValue<EntityReference>("ownerid").Should().Be(f.Other);
        stored.GetAttributeValue<EntityReference>("owningbusinessunit").Id.Should().Be(f.ChildBu);
        other.Update(new Entity("account", id) { ["name"] = "changed" });
        stored.GetAttributeValue<EntityReference>("modifiedby").Should().Be(f.Other);
        f.Provider.Simulated().Audits().Get("Create").Should().ContainSingle(a => a.Id == id);
        mine.RetrieveMultiple(new QueryExpression("account")).Entities.Should().BeEmpty();
    }

    [Test]
    public void Empty_Factory_Identity_Uses_Plugin_User_And_Preserves_Initiating_User()
    {
        var f = new Fixture();
        Identity(f.Factory.CreateOrganizationService(Guid.Empty)).UserId.Should().Be(f.User.Id);
        var context = (IPluginExecutionContext)f.Provider.GetService(typeof(IPluginExecutionContext))!;
        context.UserId.Should().Be(f.User.Id);
        context.InitiatingUserId.Should().Be(f.Other.Id);
        f.Factory.CreateOrganizationService(f.Other.Id);
        context.UserId.Should().Be(f.User.Id);
        context.InitiatingUserId.Should().Be(f.Other.Id);
    }

    [Test]
    public void Null_Factory_Identity_Uses_System_Without_Elevating_Other_Services()
    {
        var f = new Fixture();
        var hidden = f.Add("account", f.Other);
        var caller = f.Factory.CreateOrganizationService(Guid.Empty);
        var system = f.Factory.CreateOrganizationService(null);
        system.Retrieve("account", hidden.Id, new ColumnSet(true)).Id.Should().Be(hidden.Id);
        system.Update(new Entity("account", hidden.Id) { ["name"] = "system changed" });
        var systemId = system.Simulated().Data().AuthenticatedUser.Id;
        Identity(system).UserId.Should().Be(f.User.Id);
        systemId.Should().NotBe(Guid.Empty).And.NotBe(f.User.Id);
        hidden.GetAttributeValue<EntityReference>("modifiedby").Id.Should().Be(systemId);
        Action denied = () => caller.Retrieve("account", hidden.Id, new ColumnSet(true));
        denied.Should().Throw<FaultException<OrganizationServiceFault>>();
        Identity(caller).UserId.Should().Be(f.User.Id);
    }

    [Test]
    public void Unknown_Factory_User_Is_Rejected_Without_Changing_Existing_Caller()
    {
        var f = new Fixture();
        Action create = () => f.Factory.CreateOrganizationService(Guid.NewGuid());
        create.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(-2147220969);
        Identity(f.Factory.CreateOrganizationService(Guid.Empty)).UserId.Should().Be(f.User.Id);
    }

    [TestCase("explicit")]
    [TestCase("empty")]
    [TestCase("system")]
    public void WhoAmI_Reports_Connection_Identity_While_Crud_Uses_Effective_Caller(string identity)
    {
        var f = new Fixture();
        Guid? userId = identity switch { "explicit" => f.Other.Id, "empty" => Guid.Empty, _ => null };
        var service = f.Factory.CreateOrganizationService(userId);
        var connection = Identity(service);
        var effective = service.Simulated().Data().AuthenticatedUser;

        connection.UserId.Should().Be(f.User.Id);
        connection.BusinessUnitId.Should().Be(f.RootBu);
        connection.OrganizationId.Should().Be(Identity(f.Service).OrganizationId);
        var id = service.Create(new Entity("account") { ["name"] = "effective caller" });
        service.Update(new Entity("account", id) { ["name"] = "updated by effective caller" });
        var row = f.Provider.Simulated().Data().Get("account", id);
        row.GetAttributeValue<EntityReference>("ownerid").Should().Be(effective);
        row.GetAttributeValue<EntityReference>("createdby").Should().Be(effective);
        row.GetAttributeValue<EntityReference>("modifiedby").Should().Be(effective);
        Identity(service).UserId.Should().Be(connection.UserId);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Custom_Interface_Models_Are_Explicitly_Rejected(bool provider)
    {
        var options = new SimulatorOptions { SimulatedSecurityModel = new CustomModel() };
        Action initialise = () =>
        {
            if (provider) ((IServiceProvider)null!).Simulate(options);
            else ((IOrganizationService)null!).Simulate(options);
        };
        initialise.Should().Throw<NotSupportedException>().WithMessage("*Custom ISecurityModel*SimulatedSecurityModel*");
    }

    [TestCase("Assign")]
    [TestCase("Update")]
    [TestCase("UpdateRequest")]
    [TestCase("Upsert")]
    public void Ownership_Changes_Clear_Stale_Fields_And_Set_BusinessUnit(string operation)
    {
        var f = new Fixture(global: true);
        var record = f.Add("account", f.User);
        ChangeOwner(f.Service, record, f.Team, operation);
        AssertOwner(record, f.Team, f.ChildBu);
        ChangeOwner(f.Service, record, f.Other, operation);
        AssertOwner(record, f.Other, f.ChildBu);
        ChangeOwner(f.Service, record, f.User, operation);
        AssertOwner(record, f.User, f.RootBu);
    }

    [TestCase("Assign")]
    [TestCase("Update")]
    [TestCase("UpdateRequest")]
    [TestCase("Upsert")]
    public void Ownership_Changes_Require_Assign_And_Leave_Row_Unchanged_On_Denial(string operation)
    {
        var f = new Fixture(global: true);
        f.Role.EntityPermissions.Single(p => p.LogicalName == "account").Assign = PrivilegeDepthEnum.None;
        var record = f.Add("account", f.User);
        Action change = () => ChangeOwner(f.Service, record, f.Other, operation);
        change.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(-2147187962);
        record.GetAttributeValue<EntityReference>("ownerid").Should().Be(f.User);
        record.GetAttributeValue<string>("name").Should().Be("record");
        record.Contains("modifiedon").Should().BeFalse();
    }

    [TestCase("Assign", false)]
    [TestCase("Update", false)]
    [TestCase("Upsert", false)]
    [TestCase("Assign", true)]
    [TestCase("Update", true)]
    [TestCase("Upsert", true)]
    public void Missing_And_Access_Team_Assignees_Fail_Atomically(string operation, bool accessTeam)
    {
        var f = new Fixture(global: true);
        var record = f.Add("account", f.User);
        var owner = accessTeam ? f.AccessTeam : new EntityReference("systemuser", Guid.NewGuid());
        Action change = () => ChangeOwner(f.Service, record, owner, operation);
        if (accessTeam) change.Should().Throw<NotSupportedException>().WithMessage("*owner teams*");
        else change.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(-2147220969);
        record.GetAttributeValue<EntityReference>("ownerid").Should().Be(f.User);
        record.GetAttributeValue<string>("name").Should().Be("record");
    }

    [TestCase("Assign", SecurityPrivilege.Read)]
    [TestCase("Assign", SecurityPrivilege.Write)]
    [TestCase("Update", SecurityPrivilege.Read)]
    public void Assign_Depends_On_Caller_Read_And_Write(string operation, SecurityPrivilege missing)
    {
        var f = new Fixture(global: true);
        var permission = f.Role.EntityPermissions.Single(p => p.LogicalName == "account");
        if (missing == SecurityPrivilege.Read) permission.Read = PrivilegeDepthEnum.None;
        else permission.Write = PrivilegeDepthEnum.None;
        var record = f.Add("account", f.User);
        Action change = () => ChangeOwner(f.Service, record, f.Other, operation);
        change.Should().Throw<FaultException<OrganizationServiceFault>>();
        record.GetAttributeValue<EntityReference>("ownerid").Should().Be(f.User);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void New_Owner_Must_Have_Table_Read(bool create)
    {
        var f = new Fixture(global: true);
        var unprivileged = Guid.NewGuid();
        f.Model.WithUser(unprivileged, f.RootBu);
        f.Provider.Simulated().Data().Add(new Entity("systemuser", unprivileged));
        var record = f.Add("account", f.User);
        Action act = () =>
        {
            if (create) f.Service.Create(new Entity("account") { ["ownerid"] = new EntityReference("systemuser", unprivileged) });
            else ChangeOwner(f.Service, record, new EntityReference("systemuser", unprivileged), "Assign");
        };
        act.Should().Throw<FaultException<OrganizationServiceFault>>().WithMessage("*proposed owner lacks Read*");
        f.Provider.Simulated().Data().Get("account").Should().ContainSingle();
    }

    [TestCase("user")]
    [TestCase("team")]
    public void Create_On_Behalf_Of_A_Valid_Owner_Sets_All_Owning_Fields(string ownerType)
    {
        var f = new Fixture();
        var owner = ownerType == "user" ? f.Other : f.Team;
        var id = f.Service.Create(new Entity("account") { ["ownerid"] = owner });
        AssertOwner(f.Provider.Simulated().Data().Get("account", id), owner, f.ChildBu);
    }

    [Test]
    public void Explicit_Owning_BusinessUnit_Mismatch_Is_Rejected()
    {
        var f = new Fixture();
        Action create = () => f.Service.Create(new Entity("account")
        {
            ["ownerid"] = f.Other,
            ["owningbusinessunit"] = new EntityReference("businessunit", f.RootBu)
        });
        create.Should().Throw<NotSupportedException>().WithMessage("*Modernized*");
        f.Provider.Simulated().Data().Get("account").Should().BeEmpty();
    }

    [TestCase("owninguser", false)]
    [TestCase("owningteam", false)]
    [TestCase("owningbusinessunit", false)]
    [TestCase("owninguser", true)]
    [TestCase("owningteam", true)]
    [TestCase("owningbusinessunit", true)]
    public void Derived_Owner_Columns_Cannot_Bypass_Owner_Validation(string column, bool create)
    {
        var f = new Fixture(global: true);
        var existing = f.Add("account", f.User);
        var changes = new Entity("account", create ? Guid.Empty : existing.Id)
        {
            [column] = column == "owningbusinessunit" ? new EntityReference("businessunit", f.ChildBu) : f.Other
        };
        Action change = () =>
        {
            if (create) f.Service.Create(changes);
            else f.Service.Update(changes);
        };
        change.Should().Throw<NotSupportedException>();
        existing.GetAttributeValue<EntityReference>("ownerid").Should().Be(f.User);
        f.Provider.Simulated().Data().Get("account").Should().ContainSingle();
    }

    [TestCase("account")]
    [TestCase("team")]
    public void Invalid_Principal_Types_Cannot_Own_Records(string type)
    {
        var f = new Fixture();
        var owner = new EntityReference(type, type == "team" ? Guid.Empty : Guid.NewGuid());
        Action create = () => f.Service.Create(new Entity("account") { ["ownerid"] = owner });
        create.Should().Throw<ArgumentException>();
        f.Provider.Simulated().Data().Get("account").Should().BeEmpty();
    }

    [TestCase(PrivilegeDepthEnum.BusinessUnit)]
    [TestCase(PrivilegeDepthEnum.ParentChild)]
    public void Ownership_Grants_Record_Access_When_OwningBu_Differs_From_PrincipalBu(PrivilegeDepthEnum depth)
    {
        var f = new Fixture();
        f.Role.CanRead("account", depth);
        var own = f.Add("account", f.User);
        own["owningbusinessunit"] = new EntityReference("businessunit", Guid.NewGuid());
        f.Service.Retrieve("account", own.Id, new ColumnSet(true)).Id.Should().Be(own.Id);
    }

    [Test]
    public void Organization_Owned_Tables_Reject_Ownership_Even_Without_Security()
    {
        var f = new Fixture(security: false);
        f.Options.Metadata = OwnershipMetadata();
        var row = new Entity("ca_catalog", Guid.NewGuid());
        f.Provider.Simulated().Data().Add(row);
        Action assign = () => ChangeOwner(f.Service, row, f.Other, "Assign");
        assign.Should().Throw<FaultException<OrganizationServiceFault>>();
        Action share = () => f.Service.Execute(new GrantAccessRequest
        {
            Target = row.ToEntityReference(),
            PrincipalAccess = new PrincipalAccess { Principal = f.Other, AccessMask = AccessRights.ReadAccess }
        });
        share.Should().Throw<FaultException<OrganizationServiceFault>>();
        row.Contains("ownerid").Should().BeFalse();
    }

    [TestCase(PrivilegeDepthEnum.User)]
    [TestCase(PrivilegeDepthEnum.BusinessUnit)]
    [TestCase(PrivilegeDepthEnum.ParentChild)]
    public void Organization_Owned_Rows_Use_Table_Permission_Without_An_Owner(PrivilegeDepthEnum depth)
    {
        var f = new Fixture();
        f.Options.Metadata = OwnershipMetadata();
        f.Role.CanRead("ca_catalog", depth).CanCreate("ca_catalog", depth).CanWrite("ca_catalog", depth).CanDelete("ca_catalog", depth);
        var id = f.Service.Create(new Entity("ca_catalog") { ["ca_name"] = "catalog" });
        var row = f.Service.Retrieve("ca_catalog", id, new ColumnSet(true));
        row.Contains("ownerid").Should().BeFalse();
        f.Service.RetrieveMultiple(new QueryExpression("ca_catalog")).Entities.Should().ContainSingle();
        f.Service.Update(new Entity("ca_catalog", id) { ["ca_name"] = "updated" });
        f.Service.Retrieve("ca_catalog", id, new ColumnSet("ca_name"))["ca_name"].Should().Be("updated");
        f.Service.Delete("ca_catalog", id);
        f.Provider.Simulated().Data().Get("ca_catalog").Should().BeEmpty();
    }

    [Test]
    public void Organization_Owned_Rows_Still_Require_Table_Privileges()
    {
        var f = new Fixture();
        f.Options.Metadata = OwnershipMetadata();
        f.Role.CanRead("ca_catalog", PrivilegeDepthEnum.None);
        var row = f.Add("ca_catalog", f.User);
        Action retrieve = () => f.Service.Retrieve("ca_catalog", row.Id, new ColumnSet(true));
        retrieve.Should().Throw<FaultException<OrganizationServiceFault>>();
        f.Service.RetrieveMultiple(new QueryExpression("ca_catalog")).Entities.Should().BeEmpty();
    }

    [Test]
    public void Organization_Owned_Tables_Reject_Assignment_And_Sharing()
    {
        var f = new Fixture(global: true);
        f.Options.Metadata = OwnershipMetadata();
        f.Role.CanAssign("ca_catalog", PrivilegeDepthEnum.Organization).CanShare("ca_catalog", PrivilegeDepthEnum.Organization);
        var row = f.Add("ca_catalog", f.User);
        Action assign = () => ChangeOwner(f.Service, row, f.Other, "Assign");
        assign.Should().Throw<FaultException<OrganizationServiceFault>>();
        Action share = () => f.Service.Execute(new GrantAccessRequest
        {
            Target = row.ToEntityReference(),
            PrincipalAccess = new PrincipalAccess { Principal = f.Other, AccessMask = AccessRights.ReadAccess }
        });
        share.Should().Throw<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public void Failed_Transaction_Rolls_Back_Ownership_Across_Factory_Services()
    {
        var f = new Fixture(global: true);
        var record = f.Add("account", f.User);
        Action transaction = () => f.Service.Execute(new ExecuteTransactionRequest
        {
            Requests = new OrganizationRequestCollection
            {
                new AssignRequest { Target = record.ToEntityReference(), Assignee = f.Other },
                new DeleteRequest { Target = new EntityReference("account", Guid.NewGuid()) }
            }
        });
        transaction.Should().Throw<FaultException<OrganizationServiceFault>>();
        var otherService = f.Factory.CreateOrganizationService(f.Other.Id);
        otherService.Simulated().Data().Get("account", record.Id).GetAttributeValue<EntityReference>("ownerid").Should().Be(f.User);
        Identity(otherService).UserId.Should().Be(f.User.Id);
        otherService.Simulated().Data().AuthenticatedUser.Should().Be(f.Other);
        f.Provider.Simulated().Audits().Get("Assign").Should().BeEmpty();
    }

    private static SimulatedMetadata OwnershipMetadata() => SimulatedMetadata.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "security-ownership-metadata.json"));
    private static WhoAmIResponse Identity(IOrganizationService service) => (WhoAmIResponse)service.Execute(new WhoAmIRequest());
    private static void AssertOwner(Entity record, EntityReference owner, Guid businessUnit)
    {
        record.GetAttributeValue<EntityReference>("ownerid").Should().Be(owner);
        record.GetAttributeValue<EntityReference>("owninguser").Should().Be(owner.LogicalName == "systemuser" ? owner : null);
        record.GetAttributeValue<EntityReference>("owningteam").Should().Be(owner.LogicalName == "team" ? owner : null);
        record.GetAttributeValue<EntityReference>("owningbusinessunit").Id.Should().Be(businessUnit);
    }

    private static void ChangeOwner(IOrganizationService service, Entity record, EntityReference owner, string operation)
    {
        var target = new Entity(record.LogicalName, record.Id) { ["ownerid"] = owner, ["name"] = "owner changed" };
        switch (operation)
        {
            case "Assign": service.Execute(new AssignRequest { Target = record.ToEntityReference(), Assignee = owner }); break;
            case "Update": service.Update(target); break;
            case "UpdateRequest": service.Execute(new UpdateRequest { Target = target }); break;
            case "Upsert": service.Execute(new UpsertRequest { Target = target }); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static QueryExpression JoinQuery(JoinOperator join)
    {
        var query = new QueryExpression("contact") { ColumnSet = new ColumnSet("contactid") };
        var link = query.AddLink("account", "parentcustomerid", "accountid", join);
        link.EntityAlias = "a";
        link.Columns = new ColumnSet("accountid");
        return query;
    }
    private static FetchExpression FetchJoin(JoinOperator join) => new($"<fetch><entity name='contact'><attribute name='contactid'/><link-entity name='account' from='accountid' to='parentcustomerid' alias='a' link-type='{(join == JoinOperator.Inner ? "inner" : "outer")}'><attribute name='accountid'/></link-entity></entity></fetch>");

    private sealed class CustomModel : ISecurityModel
    {
        public bool IgnoreMissingEntities { get; set; }
        public List<IEntityPermission> EntityPermissions { get; set; } = [];
    }

    private sealed class Fixture
    {
        internal readonly Guid RootBu = Guid.NewGuid();
        internal readonly Guid ChildBu = Guid.NewGuid();
        internal readonly EntityReference User = new("systemuser", Guid.NewGuid());
        internal readonly EntityReference Other = new("systemuser", Guid.NewGuid());
        internal readonly EntityReference Team = new("team", Guid.NewGuid());
        internal readonly EntityReference AccessTeam = new("team", Guid.NewGuid());
        internal SimulatedSecurityModel Model { get; }
        internal SimulatedSecurityRole Role => Model.Roles.Single(r => r.Name == "Role");
        internal SimulatorOptions Options { get; }
        internal IServiceProvider Provider { get; }
        internal IOrganizationServiceFactory Factory { get; }
        internal IOrganizationService Service { get; }

        internal Fixture(bool global = false, bool security = true)
        {
            var depth = global ? PrivilegeDepthEnum.Organization : PrivilegeDepthEnum.User;
            Model = SimulatedSecurityModel.Create().WithBusinessUnit(RootBu, "Root").WithBusinessUnit(ChildBu, "Child", RootBu)
                .WithUser(User.Id, RootBu).WithUser(Other.Id, ChildBu)
                .WithTeam(Team.Id, ChildBu).WithTeam(AccessTeam.Id, RootBu, teamType: SimulatedTeamType.Access)
                .WithTeamMember(Team.Id, User.Id)
                .WithRole("Role", role => role.CanRead("account", depth).CanCreate("account", depth).CanWrite("account", depth)
                    .CanAssign("account", depth).CanRead("contact", depth))
                .AssignRoleToUser("Role", User.Id).AssignRoleToUser("Role", Other.Id).AssignRoleToTeam("Role", Team.Id);
            Model.IgnoreMissingEntities = false;
            Options = new SimulatorOptions
            {
                AuthenticatedUser = new Entity("systemuser", User.Id) { ["businessunitid"] = new EntityReference("businessunit", RootBu) },
                BusinessUnit = new Entity("businessunit", RootBu),
                PluginExecutionContextMock = new PluginExecutionContextMock { InitiatingUserId = Other.Id },
                SimulatedSecurityModel = security ? Model : null
            };
            Provider = ((IServiceProvider)null!).Simulate(Options);
            if (!security)
            {
                Provider.Simulated().Data().Add(new Entity("systemuser", Other.Id));
            }
            Factory = (IOrganizationServiceFactory)Provider.GetService(typeof(IOrganizationServiceFactory))!;
            Service = Factory.CreateOrganizationService(Guid.Empty);
        }

        internal Entity Add(string table, EntityReference owner, Entity? parent = null)
        {
            var row = new Entity(table, Guid.NewGuid()) { [$"{table}id"] = Guid.NewGuid(), ["ownerid"] = owner, ["name"] = "record" };
            row[$"{table}id"] = row.Id;
            if (parent is not null) row["parentcustomerid"] = parent.ToEntityReference();
            Provider.Simulated().Data().Add(row);
            return row;
        }
    }
}
