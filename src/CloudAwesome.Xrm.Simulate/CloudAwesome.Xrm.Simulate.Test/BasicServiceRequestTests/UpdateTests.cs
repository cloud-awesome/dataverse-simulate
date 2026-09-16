using System;
using System.Collections.Generic;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.BasicServiceRequestTests;

[TestFixture]
public class UpdateTests
{
    private const string AccountLogicalName = "account";
    private const string NameAttribute = "name";
    private const string AccountNumberAttribute = "accountnumber";

    private IOrganizationService _organizationService = null!;
    private Guid _accountId;

    [SetUp]
    public void SetUp()
    {
        _accountId = Guid.NewGuid();
        _organizationService = _organizationService.Simulate();
        _organizationService.Simulated().Data().Add(new Entity(AccountLogicalName, _accountId)
        {
            Attributes =
            {
                [NameAttribute] = "Original Account",
                [AccountNumberAttribute] = "A-001"
            }
        });
    }

    [Test]
    public void Update_Existing_Record_Persists_Incoming_Attributes()
    {
        _organizationService.Update(new Entity(AccountLogicalName, _accountId)
        {
            [NameAttribute] = "Updated Account"
        });

        var storedAccount = _organizationService.Simulated().Data().Get(AccountLogicalName, _accountId);

        storedAccount[NameAttribute].Should().Be("Updated Account");
        storedAccount[AccountNumberAttribute].Should().Be("A-001");
        storedAccount["modifiedon"].Should().Be(_organizationService.Simulated().Data().SystemTime);
    }

    [Test]
    public void Update_Method_Implements_Injected_Custom_Update_Processor_Method()
    {
        var processorType = new ProcessorType(AccountLogicalName, ProcessorMessage.Update);
        var options = new SimulatorOptions
        {
            EntityProcessors = new Dictionary<ProcessorType, IEntityProcessor>
            {
                { processorType, new AccountOnUpdateProcessor() }
            }
        };
        var orgService = _organizationService.Simulate(options);
        orgService.Simulated().Data().Add(new Entity(AccountLogicalName, _accountId)
        {
            [NameAttribute] = "Original Account"
        });

        orgService.Update(new Entity(AccountLogicalName, _accountId)
        {
            [NameAttribute] = "Updated Account"
        });

        var storedAccount = orgService.Simulated().Data().Get(AccountLogicalName, _accountId);

        storedAccount[NameAttribute].Should().Be("Updated Account - processed");
    }

    [Test]
    public void Update_Missing_Record_Throws_Dataverse_Not_Found_Fault()
    {
        var missingAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var updateMissingAccount = () => _organizationService.Update(new Entity(AccountLogicalName, missingAccountId)
        {
            [NameAttribute] = "Updated Account"
        });

        var exception = updateMissingAccount.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .WithMessage("Entity 'account' With Id = aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa Does Not Exist")
            .Which;

        exception.Detail.ErrorCode.Should().Be(-2147220969);
        exception.Detail.Message.Should().Be("Entity 'account' With Id = aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa Does Not Exist");
    }

    private sealed class AccountOnUpdateProcessor : IEntityProcessor
    {
        public Entity Process(Entity entity)
        {
            entity[NameAttribute] = $"{entity.GetAttributeValue<string>(NameAttribute)} - processed";
            return entity;
        }
    }
}
