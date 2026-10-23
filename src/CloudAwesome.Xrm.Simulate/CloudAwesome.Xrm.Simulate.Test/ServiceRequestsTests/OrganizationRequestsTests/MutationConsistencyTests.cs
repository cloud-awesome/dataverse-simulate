using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.DataStores;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.OrganizationRequestsTests;

[TestFixture]
public sealed class MutationConsistencyTests
{
    private static IEnumerable<TestCaseData> MutationCases()
    {
        foreach (var operation in new[] { "Create", "Update", "Delete", "Associate", "Disassociate", "Assign", "UpsertCreate", "UpsertUpdate" })
        foreach (var sdk in operation is "Assign" or "UpsertCreate" or "UpsertUpdate" ? new[] { true } : new[] { false, true })
        foreach (var fails in new[] { false, true })
            yield return new TestCaseData(operation, sdk, fails);
    }

    [TestCaseSource(nameof(MutationCases))]
    public void Mutation_Entries_Audit_Once_On_Success_And_Never_On_Failure(string operation, bool sdk, bool fails)
    {
        var message = operation.StartsWith("Upsert") ? "Upsert" : operation;
        var options = new SimulatorOptions();
        if (fails) options.FakeServiceFailureSettings = new FakeServiceFailureSettings
        {
            RequestFailureSettings = [new RequestFailureSetting(message) { Exception = Fault("injected") }]
        };
        var service = Create(options);
        var id = Guid.NewGuid();
        var related = new Entity("contact", Guid.NewGuid());
        var data = service.Simulated().Data();
        if (operation is not ("Create" or "UpsertCreate")) data.Add(Account(id));
        data.Add(related);
        var refs = new EntityReferenceCollection { related.ToEntityReference() };
        var relationship = new Relationship("account_contacts");
        var target = Account(id, "changed");
        Action act = operation switch
        {
            "Create" => () => { if (sdk) service.Execute(new CreateRequest { Target = target }); else service.Create(target); },
            "Update" => () => { if (sdk) service.Execute(new UpdateRequest { Target = target }); else service.Update(target); },
            "Delete" => () => { if (sdk) service.Execute(new DeleteRequest { Target = target.ToEntityReference() }); else service.Delete("account", id); },
            "Associate" => () => { if (sdk) service.Execute(new AssociateRequest { Target = target.ToEntityReference(), Relationship = relationship, RelatedEntities = refs }); else service.Associate("account", id, relationship, refs); },
            "Disassociate" => () => { if (sdk) service.Execute(new DisassociateRequest { Target = target.ToEntityReference(), Relationship = relationship, RelatedEntities = refs }); else service.Disassociate("account", id, relationship, refs); },
            "Assign" => () => service.Execute(new AssignRequest { Target = target.ToEntityReference(), Assignee = data.AuthenticatedUser }),
            _ => () => service.Execute(new UpsertRequest { Target = target })
        };
        if (fails)
        {
            act.Should().Throw<FaultException<OrganizationServiceFault>>();
            service.Simulated().Audit().Get().Should().BeEmpty();
            data.Get("account").Should().HaveCount(operation is "Create" or "UpsertCreate" ? 0 : 1);
            if (operation is not ("Create" or "UpsertCreate")) data.Get("account", id).GetAttributeValue<string>("name").Should().Be("original");
        }
        else
        {
            act();
            var audit = service.Simulated().Audit().Get().Should().ContainSingle().Subject;
            audit.Message.Should().Be(message);
            audit.EntityLogicalName.Should().Be("account");
            audit.Id.Should().Be(id);
        }
    }

    [TestCase("expression", false)]
    [TestCase("expression", true)]
    [TestCase("fetch", false)]
    [TestCase("fetch", true)]
    [TestCase("attribute", false)]
    [TestCase("attribute", true)]
    public void RetrieveMultiple_Direct_And_Execute_Log_One_Query_Event(string kind, bool sdk)
    {
        var service = Create();
        QueryBase query = kind switch
        {
            "fetch" => new FetchExpression("<fetch><entity name='account'><attribute name='name'/></entity></fetch>"),
            "attribute" => new QueryByAttribute("account") { ColumnSet = new ColumnSet("name") },
            _ => new QueryExpression("account") { ColumnSet = new ColumnSet("name") }
        };
        if (sdk) service.Execute(new RetrieveMultipleRequest { Query = query }); else service.RetrieveMultiple(query);
        var audit = service.Simulated().Audit().Get().Should().ContainSingle().Subject;
        audit.Message.Should().Be("RetrieveMultiple");
        audit.EntityLogicalName.Should().Be("account");
        audit.Id.Should().BeNull();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Failed_Query_Does_Not_Audit(bool sdk)
    {
        var service = Create();
        var query = new QueryExpression("account") { TopCount = 1, PageInfo = new PagingInfo { Count = 1, PageNumber = 1 } };
        Action act = () => { if (sdk) service.Execute(new RetrieveMultipleRequest { Query = query }); else service.RetrieveMultiple(query); };
        act.Should().Throw<FaultException<OrganizationServiceFault>>();
        service.Simulated().Audit().Get().Should().BeEmpty();
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Transaction_Fault_Identifies_Request_And_Restores_Values_And_Audit(int failureIndex)
    {
        var service = Create();
        var id = service.Create(Account(Guid.NewGuid()));
        var priorAudit = service.Simulated().Audit().Get().Single();
        var requests = new OrganizationRequestCollection();
        for (var index = 0; index < 3; index++) requests.Add(new UpdateRequest { Target = Account(index == failureIndex ? Guid.NewGuid() : id, "changed") });
        Action act = () => service.Execute(new ExecuteTransactionRequest { Requests = requests, ReturnResponses = true });
        var fault = act.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.Should().BeOfType<ExecuteTransactionFault>().Subject;
        fault.FaultedRequestIndex.Should().Be(failureIndex);
        fault.ErrorCode.Should().Be(-2147220969);
        service.Simulated().Data().Get("account", id).GetAttributeValue<string>("name").Should().Be("original");
        service.Simulated().Audit().Get().Should().Equal(priorAudit);
    }

    [TestCase(true)]
    [TestCase(false)]
    [TestCase(null)]
    public void Transaction_Responses_Are_Ordered_And_Default_To_Enabled(bool? returnResponses)
    {
        var service = Create();
        var id = Guid.NewGuid();
        var request = new ExecuteTransactionRequest { Requests = [new CreateRequest { Target = Account(id) }, new UpdateRequest { Target = Account(id, "changed") }] };
        if (returnResponses.HasValue) request.ReturnResponses = returnResponses.Value;
        var response = (ExecuteTransactionResponse)service.Execute(request);
        response.ResponseName.Should().Be("ExecuteTransaction");
        response.Results.Keys.Should().Equal("Responses");
        response.Responses.Should().HaveCount(returnResponses == false ? 0 : 2);
        if (returnResponses != false) response.Responses.Select(item => item.ResponseName).Should().Equal("Create", "Update");
        service.Simulated().Audit().Get().Select(item => item.Message).Should().Equal("Create", "Update");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Nested_Transactions_Are_Rejected_Before_Any_Inner_Request_Runs(bool multiple)
    {
        var service = Create();
        OrganizationRequest nested = multiple ? new ExecuteMultipleRequest { Requests = [] } : new ExecuteTransactionRequest { Requests = [] };
        Action act = () => service.Execute(new ExecuteTransactionRequest { Requests = [new CreateRequest { Target = Account(Guid.NewGuid()) }, nested] });
        act.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(-2147220715);
        service.Simulated().Data().Get("account").Should().BeEmpty();
        service.Simulated().Audit().Get().Should().BeEmpty();
    }

    [Test]
    public void Transaction_Preserves_Fault_Details_And_Rolls_Back_Arbitrary_Handler_Failures()
    {
        var service = Create();
        var id = Guid.NewGuid();
        var original = new OrganizationServiceFault { ErrorCode = -99, Message = "specific failure", ActivityId = Guid.NewGuid(), HelpLink = "https://example.test/help", TraceText = "trace", InnerFault = new OrganizationServiceFault { ErrorCode = -98 } };
        original.ErrorDetails["detail"] = "preserved";
        Exception injected = new FaultException<OrganizationServiceFault>(original);
        service.Simulated().CustomOrgRequests().Add<FailRequest>((_, _) => throw injected);
        Action act = () => service.Execute(new ExecuteTransactionRequest { Requests = [new CreateRequest { Target = Account(id) }, new FailRequest()] });
        var fault = act.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.Should().BeOfType<ExecuteTransactionFault>().Subject;
        fault.FaultedRequestIndex.Should().Be(1);
        fault.ErrorCode.Should().Be(original.ErrorCode);
        fault.Message.Should().Be(original.Message);
        fault.ActivityId.Should().Be(original.ActivityId);
        fault.HelpLink.Should().Be(original.HelpLink);
        fault.TraceText.Should().Be(original.TraceText);
        fault.ErrorDetails.Should().BeEquivalentTo(original.ErrorDetails);
        fault.InnerFault.Should().BeSameAs(original.InnerFault);
        service.Simulated().Data().Get("account").Should().BeEmpty();
        service.Simulated().Audit().Get().Should().BeEmpty();

        injected = new InvalidOperationException("custom failure");
        act.Should().Throw<InvalidOperationException>().WithMessage("custom failure");
        service.Simulated().Data().Get("account").Should().BeEmpty();
        service.Simulated().Audit().Get().Should().BeEmpty();
    }

    [Test]
    public void Transaction_Restores_Deleted_Rows_And_Relationships()
    {
        var service = Create();
        var account = Account(Guid.NewGuid());
        var contact = new Entity("contact", Guid.NewGuid());
        service.Simulated().Data().Add(account);
        service.Simulated().Data().Add(contact);
        Action act = () => service.Execute(new ExecuteTransactionRequest { Requests = [
            new AssociateRequest { Target = account.ToEntityReference(), Relationship = new Relationship("account_contacts"), RelatedEntities = [contact.ToEntityReference()] },
            new DeleteRequest { Target = account.ToEntityReference() },
            new UpdateRequest { Target = Account(Guid.NewGuid()) }
        ] });
        act.Should().Throw<FaultException<OrganizationServiceFault>>();
        service.Simulated().Data().Get("account", account.Id).GetAttributeValue<string>("name").Should().Be("original");
        service.Simulated().Data().GetRelationships(account.ToEntityReference(), new Relationship("account_contacts")).Should().BeEmpty();
        service.Simulated().Audit().Get().Should().BeEmpty();
    }

    private sealed class FailRequest : OrganizationRequest { public FailRequest() => RequestName = "Fail"; }
    private static FaultException<OrganizationServiceFault> Fault(string message) => new(new OrganizationServiceFault { ErrorCode = -99, Message = message });
    private static Entity Account(Guid id, string name = "original") => new("account", id) { ["name"] = name };
    private static IOrganizationService Create(SimulatorOptions? options = null) => ((IOrganizationService)null!).Simulate(options);
}
