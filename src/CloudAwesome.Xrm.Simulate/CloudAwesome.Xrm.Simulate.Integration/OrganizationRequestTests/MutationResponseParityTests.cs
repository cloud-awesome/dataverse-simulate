using System;
using System.IO;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using CloudAwesome.Xrm.Simulate.Metadata;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture, Category("Parity"), Category("MutationConsistency")]
public sealed class MutationResponseParityTests : IntegrationBaseFixture
{
    [TestCase(true)]
    [TestCase(false)]
    [TestCase(null)]
    public void Transaction_Response_Settings_And_Order_Match(bool? returnResponses)
    {
        var id = Guid.NewGuid();
        DataverseParityHarness.Execute(new DataverseParityScenario<ResponseShape>
        {
            Name = $"Transaction_response_{returnResponses}",
            ArrangeLive = context => { context.Service.Create(Account(id)); context.Cleanup.TrackForDelete("account", id); },
            ArrangeSimulated = context => context.Simulation.Data().Add(Account(id)),
            Act = service =>
            {
                var request = new ExecuteTransactionRequest { Requests = [new UpdateRequest { Target = Account(id, "changed") }, new RetrieveRequest { Target = new EntityReference("account", id), ColumnSet = new ColumnSet("name") }] };
                if (returnResponses.HasValue) request.ReturnResponses = returnResponses.Value;
                var response = (ExecuteTransactionResponse)service.Execute(request);
                response.Responses.Should().HaveCount(returnResponses == false ? 0 : 2);
                return new ResponseShape(response.ResponseName, response.Results.Keys.Order().ToArray(), response.Responses.Select(item => item.ResponseName).ToArray(), Name(service, id));
            }
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Transaction_Fault_Type_Index_Message_And_Rollback_Match(int failureIndex)
    {
        var id = Guid.NewGuid();
        var missing = Guid.NewGuid();
        DataverseParityHarness.Execute(new DataverseParityScenario<TransactionFailure>
        {
            Name = $"Transaction_fault_{failureIndex}",
            ArrangeLive = context => { context.Service.Create(Account(id)); context.Cleanup.TrackForDelete("account", id); },
            ArrangeSimulated = context => context.Simulation.Data().Add(Account(id)),
            Act = service =>
            {
                var requests = new OrganizationRequestCollection();
                for (var i = 0; i < 3; i++) requests.Add(new UpdateRequest { Target = Account(i == failureIndex ? missing : id, "changed") });
                var fault = Capture(() => service.Execute(new ExecuteTransactionRequest { ReturnResponses = true, Requests = requests }));
                fault.Should().BeOfType<ExecuteTransactionFault>();
                ((ExecuteTransactionFault)fault).FaultedRequestIndex.Should().Be(failureIndex);
                return new TransactionFailure(fault.GetType().Name, fault.ErrorCode, fault.Message, ((ExecuteTransactionFault)fault).FaultedRequestIndex, Name(service, id));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Nested_Transaction_Batches_Are_Rejected_Before_Any_Mutation(bool multiple)
    {
        var id = Guid.NewGuid();
        DataverseParityHarness.Execute(new DataverseParityScenario<TransactionFailure>
        {
            Name = $"Transaction_nested_{multiple}",
            ArrangeLive = context => { context.Service.Create(Account(id)); context.Cleanup.TrackForDelete("account", id); },
            ArrangeSimulated = context => context.Simulation.Data().Add(Account(id)),
            Act = service =>
            {
                OrganizationRequest nested = multiple ? new ExecuteMultipleRequest { Requests = [], Settings = new ExecuteMultipleSettings() } : new ExecuteTransactionRequest { Requests = [] };
                var fault = Capture(() => service.Execute(new ExecuteTransactionRequest { Requests = [new UpdateRequest { Target = Account(id, "changed") }, nested] }));
                return new TransactionFailure(fault.GetType().Name, fault.ErrorCode, fault.Message, -1, Name(service, id));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Multiple_Response_Settings_Fault_Indexes_And_Partial_Commit_Match(bool continueOnError, bool returnResponses)
    {
        var id = Guid.NewGuid();
        var missing = Guid.NewGuid();
        DataverseParityHarness.Execute(new DataverseParityScenario<MultipleShape>
        {
            Name = $"Multiple_{continueOnError}_{returnResponses}",
            ArrangeLive = context => { context.Service.Create(Account(id)); context.Cleanup.TrackForDelete("account", id); },
            ArrangeSimulated = context => context.Simulation.Data().Add(Account(id)),
            Act = service =>
            {
                var response = (ExecuteMultipleResponse)service.Execute(new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings { ContinueOnError = continueOnError, ReturnResponses = returnResponses },
                    Requests = [new UpdateRequest { Target = Account(id, "first") }, new UpdateRequest { Target = Account(missing) }, new UpdateRequest { Target = Account(id, "last") }]
                });
                response.IsFaulted.Should().BeTrue();
                return new MultipleShape(response.ResponseName, response.Results.Keys.Order().ToArray(), response.Responses.Select(item => $"{item.RequestIndex}:{item.Response?.ResponseName}:{item.Fault?.ErrorCode}").ToArray(), Name(service, id));
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void Upsert_Primary_Id_Response_And_Request_Isolation_Match(bool exists, bool keyAttribute)
    {
        var id = Guid.NewGuid();
        DataverseParityHarness.Execute(new DataverseParityScenario<UpsertShape>
        {
            Name = $"Upsert_primary_{exists}_{keyAttribute}",
            ArrangeLive = context => { if (exists) context.Service.Create(Account(id)); context.Cleanup.TrackForDelete("account", id); },
            ArrangeSimulated = context => { if (exists) context.Simulation.Data().Add(Account(id)); },
            Act = service =>
            {
                var target = keyAttribute ? new Entity("account", "accountid", id) : new Entity("account", id);
                target["name"] = "upserted";
                var response = (UpsertResponse)service.Execute(new UpsertRequest { Target = target });
                response.RecordCreated.Should().Be(!exists);
                response.Target.Id.Should().Be(id);
                return new UpsertShape(response.ResponseName, response.Results.Keys.Order().ToArray(), response.RecordCreated, response.Target.LogicalName, response.Target.KeyAttributes.Count, response.Target.Name, target.Id, target.Attributes.Count, Name(service, id));
            }
        });
    }

    [Test]
    public void Upsert_Existing_Row_With_Undeclared_Key_Is_Rejected()
    {
        var id = Guid.NewGuid();
        var uniqueName = $"CA mutation {Guid.NewGuid():N}";
        DataverseParityHarness.Execute(new DataverseParityScenario<int>
        {
            Name = nameof(Upsert_Existing_Row_With_Undeclared_Key_Is_Rejected),
            ArrangeLive = context => { context.Service.Create(Account(id, uniqueName)); context.Cleanup.TrackForDelete("account", id); },
            CreateSimulatorOptions = _ => new SimulatorOptions { Metadata = SimulatedMetadata.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "mutation-metadata.json")) },
            ArrangeSimulated = context => context.Simulation.Data().Add(Account(id, uniqueName)),
            Act = service => Capture(() => service.Execute(new UpsertRequest { Target = new Entity("account", "name", uniqueName) { ["description"] = "must fail" } })).ErrorCode
        });
    }

    private static OrganizationServiceFault Capture(Action action)
    {
        try { action(); }
        catch (FaultException<OrganizationServiceFault> exception) { return exception.Detail; }
        throw new AssertionException("Expected an organization service fault.");
    }

    private static Entity Account(Guid id, string name = "CA mutation original") => new("account", id) { ["name"] = name };
    private static string? Name(IOrganizationService service, Guid id) => service.Retrieve("account", id, new ColumnSet("name")).GetAttributeValue<string>("name");
    private sealed record ResponseShape(string ResponseName, string[] Keys, string[] Responses, string? StoredName);
    private sealed record TransactionFailure(string FaultType, int Code, string Message, int Index, string? StoredName);
    private sealed record MultipleShape(string ResponseName, string[] Keys, string[] Items, string? StoredName);
    private sealed record UpsertShape(string ResponseName, string[] Keys, bool Created, string TargetTable, int TargetKeys, string? TargetName, Guid RequestId, int RequestAttributes, string? StoredName);
}
