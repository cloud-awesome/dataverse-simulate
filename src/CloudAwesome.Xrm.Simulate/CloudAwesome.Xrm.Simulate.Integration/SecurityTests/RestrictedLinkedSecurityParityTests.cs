using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather.SecurityTests;

/// <summary>
/// Requires an existing sandbox user with Basic account/contact Read and no broader
/// role/team/hierarchy access to the caller's new accounts. No existing roles are changed.
/// </summary>
[TestFixture]
[Category("Parity")]
[Category("SecurityIdentity")]
[Category("Security")]
public sealed class RestrictedLinkedSecurityParityTests : IntegrationBaseFixture
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Restricted_Caller_Link_Visibility_And_Counts_Match(bool fetch, bool outer)
    {
        Run(fetch, outer, aggregate: false);
    }

    [Test]
    public void Restricted_Caller_Link_Aggregate_Count_Matches() => Run(fetch: true, outer: false, aggregate: true);

    private static void Run(bool fetch, bool outer, bool aggregate)
    {
        var setting = Environment.GetEnvironmentVariable("DATAVERSE_SECURITY_USER_ID");
        if (string.IsNullOrWhiteSpace(setting))
            Assert.Ignore("Set DATAVERSE_SECURITY_USER_ID to a sandbox user with Basic account/contact Read and no broader account access; caller needs impersonation and fixture CRUD privileges.");
        var restrictedUserId = Guid.Parse(setting!);
        WhoAmIResponse who = null!;
        SimulatorOptions simulatedOptions = null!;
        Entity[] rows = [];
        string prefix = $"CASec {Guid.NewGuid():N}";
        var scenario = new DataverseParityScenario<LinkShape>
        {
            Name = $"Restricted_Links_fetch_{fetch}_outer_{outer}_aggregate_{aggregate}",
            ArrangeLive = context =>
            {
                who = (WhoAmIResponse)context.Service.Execute(new WhoAmIRequest());
                restrictedUserId.Should().NotBe(who.UserId, "a separate restricted user is required");
                var restricted = new EntityReference("systemuser", restrictedUserId);
                var hidden = new Entity("account") { ["name"] = prefix + " hidden" };
                hidden.Id = context.Service.Create(hidden);
                context.Cleanup.TrackForDelete(hidden.LogicalName, hidden.Id);
                hidden["ownerid"] = new EntityReference("systemuser", who.UserId);
                var visible = new Entity("account") { ["name"] = prefix + " visible", ["ownerid"] = restricted };
                visible.Id = context.Service.Create(visible);
                context.Cleanup.TrackForDelete(visible.LogicalName, visible.Id);
                rows = [hidden, visible];
                foreach (var account in rows.ToArray())
                {
                    var contact = new Entity("contact")
                    {
                        ["lastname"] = prefix,
                        ["ownerid"] = restricted,
                        ["parentcustomerid"] = account.ToEntityReference()
                    };
                    contact.Id = context.Service.Create(contact);
                    context.Cleanup.TrackForDelete(contact.LogicalName, contact.Id);
                    rows = [..rows, contact];
                }
            },
            CreateSimulatorOptions = _ => simulatedOptions = new SimulatorOptions
            {
                AuthenticatedUser = new Entity("systemuser", who.UserId),
                BusinessUnit = new Entity("businessunit", who.BusinessUnitId),
                Organization = new Entity("organization", who.OrganizationId),
                SimulatedSecurityModel = SimulatedSecurityModel.Create().WithBusinessUnit(who.BusinessUnitId, "Root")
                    .WithUser(who.UserId, who.BusinessUnitId).WithUser(restrictedUserId, who.BusinessUnitId)
                    .WithRole("Restricted", r => r.CanRead("account", PrivilegeDepthEnum.User).CanRead("contact", PrivilegeDepthEnum.User))
                    .AssignRoleToUser("Restricted", restrictedUserId)
            },
            ArrangeSimulated = context =>
            {
                foreach (var row in rows)
                {
                    row[$"{row.LogicalName}id"] = row.Id;
                    context.Simulation.Data().Add(row);
                }
            },
            Act = service => AsRestricted(service, restrictedUserId, simulatedOptions, caller =>
            {
                // WhoAmI reports the authenticated connection user even while CallerId
                // impersonates another principal. Prove the effective read scope instead.
                var connectionIdentity = (WhoAmIResponse)caller.Execute(new WhoAmIRequest());
                connectionIdentity.UserId.Should().Be(who.UserId);
                connectionIdentity.BusinessUnitId.Should().Be(who.BusinessUnitId);
                connectionIdentity.OrganizationId.Should().Be(who.OrganizationId);
                caller.Retrieve("account", rows[1].Id, new ColumnSet("accountid")).Id.Should().Be(rows[1].Id);
                var hiddenAccount = new QueryExpression("account") { ColumnSet = new ColumnSet("accountid") };
                hiddenAccount.Criteria.AddCondition("accountid", ConditionOperator.Equal, rows[0].Id);
                caller.RetrieveMultiple(hiddenAccount).Entities.Should().BeEmpty(
                    "the impersonated user must not have access to the connecting user's account");
                if (aggregate)
                {
                    var result = caller.RetrieveMultiple(new FetchExpression($"<fetch aggregate='true'><entity name='contact'><attribute name='contactid' alias='n' aggregate='count'/><filter><condition attribute='lastname' operator='eq' value='{prefix}'/></filter><link-entity name='account' from='accountid' to='parentcustomerid'/></entity></fetch>"));
                    var value = result.Entities.Single()["n"];
                    var count = Convert.ToInt32(value is AliasedValue aliased ? aliased.Value : value);
                    count.Should().Be(1, "only one joined account is readable by the configured restricted user");
                    return new LinkShape(count, false, []);
                }
                QueryBase query;
                if (fetch)
                    query = new FetchExpression($"<fetch returntotalrecordcount='true'><entity name='contact'><attribute name='contactid'/><filter><condition attribute='lastname' operator='eq' value='{prefix}'/></filter><link-entity name='account' from='accountid' to='parentcustomerid' alias='a' link-type='{(outer ? "outer" : "inner")}'><attribute name='accountid'/></link-entity></entity></fetch>");
                else
                {
                    var expression = new QueryExpression("contact") { ColumnSet = new ColumnSet("contactid"), PageInfo = new PagingInfo { ReturnTotalRecordCount = true } };
                    expression.Criteria.AddCondition("lastname", ConditionOperator.Equal, prefix);
                    var link = expression.AddLink("account", "parentcustomerid", "accountid", outer ? JoinOperator.LeftOuter : JoinOperator.Inner);
                    link.EntityAlias = "a";
                    link.Columns = new ColumnSet("accountid");
                    query = expression;
                }
                var resultRows = caller.RetrieveMultiple(query);
                resultRows.TotalRecordCount.Should().Be(outer ? 2 : 1);
                var linkedIds = resultRows.Entities.Where(e => e.Contains("a.accountid"))
                    .Select(e => (Guid)((AliasedValue)e["a.accountid"]).Value).ToArray();
                linkedIds.Should().Equal(new[] { rows[1].Id }, "the caller must not see the privileged caller's hidden account through a link");
                return new LinkShape(resultRows.TotalRecordCount, resultRows.MoreRecords, resultRows.Entities.Select(e => e.Id).OrderBy(id => id).ToArray());
            })
        };
        DataverseParityHarness.Execute(scenario);
    }

    private static LinkShape AsRestricted(IOrganizationService service, Guid userId, SimulatorOptions options, Func<IOrganizationService, LinkShape> act)
    {
        if (service is ServiceClient live)
        {
            using var caller = live.Clone();
            caller.CallerId = userId;
            caller.CallerId.Should().Be(userId, "the cloned client must retain the requested impersonation identity");
            return act(caller);
        }
        var simulation = service.Simulated();
        var factory = OrganisationServiceFactorySimulator.Create(simulation.Data(), simulation.Audit(), options)!;
        return act(factory.CreateOrganizationService(userId));
    }

    private sealed record LinkShape(int Count, bool MoreRecords, Guid[] Ids);
}
