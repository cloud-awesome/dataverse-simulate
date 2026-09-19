using System;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture]
[Category("Parity")]
[Category("OrganizationRequests")]
public sealed class CoreOrganizationRequestParityTests : IntegrationBaseFixture
{
    private const string AccountLogicalName = "account";
    private const string AccountIdAttribute = "accountid";
    private const string AccountNameAttribute = "name";
    private const string AccountStateAttribute = "statecode";
    private const string AccountStatusAttribute = "statuscode";
    private static readonly Guid NonEmptyGuidMarker = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MissingAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Test]
    public void UpsertRequest_Create_Response_Should_Match_Live_Dataverse()
    {
        var accountId = Guid.NewGuid();

        var scenario = new DataverseParityScenario<UpsertSnapshot>
        {
            Name = nameof(UpsertRequest_Create_Response_Should_Match_Live_Dataverse),
            Act = service =>
            {
                var response = (UpsertResponse)service.Execute(new UpsertRequest
                {
                    Target = Account(accountId, "Upsert created")
                });

                return new UpsertSnapshot(
                    response.ResponseName,
                    response.RecordCreated,
                    response.Target?.Id ?? Guid.Empty);
            },
            AfterLiveAct = (context, _) => context.Cleanup.TrackForDelete(AccountLogicalName, accountId),
            Normalize = result => result with
            {
                TargetId = result.TargetId == Guid.Empty ? Guid.Empty : NonEmptyGuidMarker
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void SetStateRequest_Should_Update_State_And_Status_Like_Live_Dataverse()
    {
        var accountId = Guid.Empty;

        var scenario = new DataverseParityScenario<StateSnapshot>
        {
            Name = nameof(SetStateRequest_Should_Update_State_And_Status_Like_Live_Dataverse),
            ArrangeLive = context =>
            {
                accountId = context.Service.Create(Account(Guid.Empty, "SetState account"));
                context.Cleanup.TrackForDelete(AccountLogicalName, accountId);
            },
            ArrangeSimulated = context => context.Simulation.Data().Add(Account(accountId, "SetState account")),
            Act = service =>
            {
                service.Execute(new SetStateRequest
                {
                    EntityMoniker = new EntityReference(AccountLogicalName, accountId),
                    State = new OptionSetValue(1),
                    Status = new OptionSetValue(2)
                });

                var stored = service.Retrieve(
                    AccountLogicalName,
                    accountId,
                    new ColumnSet(AccountStateAttribute, AccountStatusAttribute));

                return new StateSnapshot(
                    stored.GetAttributeValue<OptionSetValue>(AccountStateAttribute)?.Value,
                    stored.GetAttributeValue<OptionSetValue>(AccountStatusAttribute)?.Value);
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void ExecuteMultipleRequest_Fault_Response_Should_Match_Live_Dataverse()
    {
        var accountId = Guid.Empty;

        var scenario = new DataverseParityScenario<ExecuteMultipleSnapshot>
        {
            Name = nameof(ExecuteMultipleRequest_Fault_Response_Should_Match_Live_Dataverse),
            ArrangeLive = context =>
            {
                accountId = context.Service.Create(Account(Guid.Empty, "Original batch account"));
                context.Cleanup.TrackForDelete(AccountLogicalName, accountId);
            },
            ArrangeSimulated = context => context.Simulation.Data().Add(Account(accountId, "Original batch account")),
            Act = service =>
            {
                var response = (ExecuteMultipleResponse)service.Execute(new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = true,
                        ReturnResponses = true
                    },
                    Requests =
                    [
                        new UpdateRequest
                        {
                            Target = Account(accountId, "Updated batch account")
                        },
                        new UpdateRequest
                        {
                            Target = Account(MissingAccountId, "Missing batch account")
                        }
                    ]
                });

                return new ExecuteMultipleSnapshot(
                    response.ResponseName,
                    response.IsFaulted,
                    response.Responses.Select(item => new ExecuteMultipleItemSnapshot(
                        item.RequestIndex,
                        item.Response?.ResponseName,
                        item.Fault?.ErrorCode)).ToArray(),
                    RetrieveAccountName(service, accountId));
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void ExecuteTransactionRequest_Should_Roll_Back_Like_Live_Dataverse()
    {
        var accountId = Guid.Empty;

        var scenario = new DataverseParityScenario<TransactionRollbackSnapshot>
        {
            Name = nameof(ExecuteTransactionRequest_Should_Roll_Back_Like_Live_Dataverse),
            ArrangeLive = context =>
            {
                accountId = context.Service.Create(Account(Guid.Empty, "Original transaction account"));
                context.Cleanup.TrackForDelete(AccountLogicalName, accountId);
            },
            ArrangeSimulated = context => context.Simulation.Data().Add(Account(accountId, "Original transaction account")),
            Act = service =>
            {
                var faultCode = CaptureFaultCode(() => service.Execute(new ExecuteTransactionRequest
                {
                    ReturnResponses = true,
                    Requests =
                    [
                        new UpdateRequest
                        {
                            Target = Account(accountId, "Should roll back")
                        },
                        new UpdateRequest
                        {
                            Target = Account(MissingAccountId, "Missing transaction account")
                        }
                    ]
                }));

                return new TransactionRollbackSnapshot(faultCode, RetrieveAccountName(service, accountId));
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    private static int CaptureFaultCode(Action act)
    {
        try
        {
            act();
        }
        catch (FaultException<OrganizationServiceFault> ex)
        {
            return ex.Detail.ErrorCode;
        }

        throw new AssertionException("Operation unexpectedly succeeded.");
    }

    private static string? RetrieveAccountName(IOrganizationService service, Guid accountId)
    {
        return service.Retrieve(AccountLogicalName, accountId, new ColumnSet(AccountNameAttribute))
            .GetAttributeValue<string>(AccountNameAttribute);
    }

    private static Entity Account(Guid id, string name)
    {
        var account = new Entity(AccountLogicalName, id)
        {
            [AccountNameAttribute] = name
        };

        if (id != Guid.Empty)
        {
            account[AccountIdAttribute] = id;
        }

        return account;
    }

    private sealed record UpsertSnapshot(string? ResponseName, bool RecordCreated, Guid TargetId);

    private sealed record StateSnapshot(int? State, int? Status);

    private sealed record ExecuteMultipleSnapshot(
        string? ResponseName,
        bool IsFaulted,
        ExecuteMultipleItemSnapshot[] Items,
        string? AccountName);

    private sealed record ExecuteMultipleItemSnapshot(int RequestIndex, string? ResponseName, int? FaultCode);

    private sealed record TransactionRollbackSnapshot(int FaultCode, string? AccountName);
}
