using System;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture]
[Category("Parity")]
[Category("ParitySmoke")]
[Category("Exceptions")]
public sealed class DataverseExceptionParityTests : IntegrationBaseFixture
{
    private const string AccountLogicalName = "account";
    private const string AccountNameAttribute = "name";
    private static readonly Guid MissingAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Test]
    public void Retrieve_Missing_Record_Should_Match_Live_Dataverse_Fault()
    {
        AssertMissingRecordExceptionParity(
            nameof(Retrieve_Missing_Record_Should_Match_Live_Dataverse_Fault),
            service => service.Retrieve(AccountLogicalName, MissingAccountId, new ColumnSet(true)));
    }

    [Test]
    public void RetrieveRequest_Missing_Record_Should_Match_Live_Dataverse_Fault()
    {
        AssertMissingRecordExceptionParity(
            nameof(RetrieveRequest_Missing_Record_Should_Match_Live_Dataverse_Fault),
            service => service.Execute(new RetrieveRequest
            {
                Target = MissingAccountReference(),
                ColumnSet = new ColumnSet(true)
            }));
    }

    [Test]
    public void Update_Missing_Record_Should_Match_Live_Dataverse_Fault()
    {
        AssertMissingRecordExceptionParity(
            nameof(Update_Missing_Record_Should_Match_Live_Dataverse_Fault),
            service => service.Update(MissingAccount()));
    }

    [Test]
    public void UpdateRequest_Missing_Record_Should_Match_Live_Dataverse_Fault()
    {
        AssertMissingRecordExceptionParity(
            nameof(UpdateRequest_Missing_Record_Should_Match_Live_Dataverse_Fault),
            service => service.Execute(new UpdateRequest
            {
                Target = MissingAccount()
            }));
    }

    [Test]
    public void Delete_Missing_Record_Should_Match_Live_Dataverse_Fault()
    {
        AssertMissingRecordExceptionParity(
            nameof(Delete_Missing_Record_Should_Match_Live_Dataverse_Fault),
            service => service.Delete(AccountLogicalName, MissingAccountId));
    }

    [Test]
    public void DeleteRequest_Missing_Record_Should_Match_Live_Dataverse_Fault()
    {
        AssertMissingRecordExceptionParity(
            nameof(DeleteRequest_Missing_Record_Should_Match_Live_Dataverse_Fault),
            service => service.Execute(new DeleteRequest
            {
                Target = MissingAccountReference()
            }));
    }

    private static void AssertMissingRecordExceptionParity(
        string scenarioName,
        Action<IOrganizationService> act)
    {
        var scenario = new DataverseParityScenario<DataverseFaultSnapshot>
        {
            Name = scenarioName,
            Act = service => CaptureFault(service, act)
        };

        DataverseParityHarness.Execute(scenario);
    }

    private static DataverseFaultSnapshot CaptureFault(
        IOrganizationService service,
        Action<IOrganizationService> act)
    {
        try
        {
            act(service);
        }
        catch (Exception exception)
        {
            return DataverseFaultSnapshot.From(exception);
        }

        throw new AssertionException("Operation unexpectedly succeeded.");
    }

    private static Entity MissingAccount()
    {
        return new Entity(AccountLogicalName, MissingAccountId)
        {
            [AccountNameAttribute] = "Missing Account"
        };
    }

    private static EntityReference MissingAccountReference()
    {
        return new EntityReference(AccountLogicalName, MissingAccountId);
    }

    private sealed record DataverseFaultSnapshot(
        string ExceptionType,
        string Message,
        string? FaultType,
        int? ErrorCode,
        string? FaultMessage)
    {
        public static DataverseFaultSnapshot From(Exception exception)
        {
            if (exception is FaultException<OrganizationServiceFault> faultException)
            {
                return new DataverseFaultSnapshot(
                    exception.GetType().FullName!,
                    exception.Message,
                    faultException.Detail.GetType().FullName,
                    faultException.Detail.ErrorCode,
                    faultException.Detail.Message);
            }

            return new DataverseFaultSnapshot(
                exception.GetType().FullName!,
                exception.Message,
                null,
                null,
                null);
        }
    }
}
