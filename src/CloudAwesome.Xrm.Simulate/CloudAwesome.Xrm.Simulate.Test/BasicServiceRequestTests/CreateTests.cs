using System;
using System.Collections.Generic;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.EntityProcessors;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.BasicServiceRequestTests;

[TestFixture]
public class CreateTests
{
    private IOrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = _organizationService.Simulate();
    }
    
    [Test]
    public void Create_Contact_Saves_Record_To_Data_Store()
    {
        var contactId = _organizationService.Create(Arthur.Contact());
        contactId.Should().NotBeEmpty();

        var contacts = _organizationService.Simulated().Data().Get(Arthur.Contact().LogicalName);
        contacts.Count.Should().Be(1);

        contacts.FirstOrDefault()?.Id.Should().Be(contactId);
    }

    [Test]
    public void Calling_Create_Method_Via_Execute_Method_Should_Save_To_Data_Store()
    {
        var createRequest = new CreateRequest
        {
            Target = Arthur.Contact()
        };

        var createResponse = (CreateResponse) _organizationService.Execute(createRequest);
        createResponse.id.Should().NotBeEmpty();
        createResponse.ResponseName.Should().Be("Create");
        
        var contacts = _organizationService.Simulated().Data().Get(Arthur.Contact().LogicalName);
        contacts.Count.Should().Be(1);

        contacts.FirstOrDefault()?.Id.Should().Be(createResponse.id);
    }

    [Test]
    public void Create_Record_With_Duplicate_Id_Should_Throw()
    {
        var contactId = Guid.NewGuid();
        _organizationService.Create(new Entity(Contact.EntityLogicalName)
        {
            [Contact.PrimaryIdAttribute] = contactId,
            [Contact.Fields.FirstName] = "Original"
        });

        var createDuplicate = () => _organizationService.Create(new Entity(Contact.EntityLogicalName)
        {
            [Contact.PrimaryIdAttribute] = contactId,
            [Contact.Fields.FirstName] = "Duplicate"
        });

        createDuplicate.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"A record with id '{contactId}' already exists for entity '{Contact.EntityLogicalName}'.");
    }

    [Test]
    public void Create_Record_With_Duplicate_Entity_Id_Should_Throw()
    {
        var contactId = Guid.NewGuid();
        _organizationService.Create(new Entity(Contact.EntityLogicalName, contactId)
        {
            [Contact.Fields.FirstName] = "Original"
        });

        var createDuplicate = () => _organizationService.Create(new Entity(Contact.EntityLogicalName, contactId)
        {
            [Contact.Fields.FirstName] = "Duplicate"
        });

        createDuplicate.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"A record with id '{contactId}' already exists for entity '{Contact.EntityLogicalName}'.");
    }

    [Test]
    public void Create_Record_With_Same_Id_For_Different_Entity_Should_Save_Record()
    {
        var id = Guid.NewGuid();
        _organizationService.Create(new Entity(Contact.EntityLogicalName)
        {
            [Contact.PrimaryIdAttribute] = id
        });

        var accountId = _organizationService.Create(new Entity(Account.EntityLogicalName)
        {
            [Account.PrimaryIdAttribute] = id
        });

        accountId.Should().Be(id);
        _organizationService.Simulated().Data().Get(Contact.EntityLogicalName).Should().ContainSingle();
        _organizationService.Simulated().Data().Get(Account.EntityLogicalName).Should().ContainSingle();
    }

    [Test]
    public void Calling_Create_Method_Via_Execute_Method_With_Duplicate_Id_Should_Throw()
    {
        var contactId = Guid.NewGuid();
        _organizationService.Create(new Entity(Contact.EntityLogicalName)
        {
            [Contact.PrimaryIdAttribute] = contactId
        });

        var createRequest = new CreateRequest
        {
            Target = new Entity(Contact.EntityLogicalName)
            {
                [Contact.PrimaryIdAttribute] = contactId
            }
        };

        var createDuplicate = () => _organizationService.Execute(createRequest);

        createDuplicate.Should()
            .Throw<InvalidOperationException>()
            .WithMessage($"A record with id '{contactId}' already exists for entity '{Contact.EntityLogicalName}'.");
    }

    [Test]
    public void Create_Contact_Sets_System_DateTime_Metadata()
    {
        var mockSystemTime = new MockSystemTime(new DateTime(2020, 8, 16));
        var options = new SimulatorOptions
        {
            ClockSimulator = mockSystemTime
        };
        
        var orgService = _organizationService.Simulate(options);

        var contactId = orgService.Create(Arthur.Contact());
        var contact = orgService.Simulated().Data().Get(Arthur.Contact().LogicalName).FirstOrDefault();

        contact.Attributes["createdon"].Should().Be(mockSystemTime.Now);
        contact.Attributes["modifiedon"].Should().Be(mockSystemTime.Now);
    }

    [Test]
    public void Create_Contact_Sets_System_User_Metadata()
    {
        var options = new SimulatorOptions
        {
            AuthenticatedUser = new Entity("systemuser", Guid.NewGuid())
            {
                Attributes =
                {
                    ["fullname"] = "Lynda Archer"
                }
            }
        };
        var authenticatedUser = options.AuthenticatedUser.ToEntityReference();
        var orgService = _organizationService.Simulate(options);
        
        var contactId = orgService.Create(Arthur.Contact());
        var contact = orgService.Simulated().Data().Get(Arthur.Contact().LogicalName).FirstOrDefault();

        contact.Should().NotBeNull();
        
        contact!.Attributes["createdby"].Should().Be(authenticatedUser);
        contact.Attributes["modifiedby"].Should().Be(authenticatedUser);
        contact.Attributes["ownerid"].Should().Be(authenticatedUser);

        contact.Attributes["createdby"].Should().BeOfType<EntityReference>();
        var retrievedContact = (EntityReference) contact.Attributes["createdby"];
        retrievedContact.Id.Should().Be(authenticatedUser.Id);
    }

    [Test]
    public void Create_Method_Implements_Injected_Custom_Processor_Method()
    {
        var contactOnCreateProcessorType = new ProcessorType(Contact.EntityLogicalName, ProcessorMessage.Create);
        var options = new SimulatorOptions
        {
            EntityProcessors = new Dictionary<ProcessorType, IEntityProcessor>
            {
                { contactOnCreateProcessorType, new ContactOnCreateProcessor() }
            }
        };
        var orgService = _organizationService.Simulate(options);

        orgService.Create(Arthur.Contact());

        var contacts = orgService.Simulated().Data().Get(Contact.EntityLogicalName);

        contacts.Count.Should().Be(1);

        var contact = contacts.Cast<Contact>().FirstOrDefault()!;
        contact.CreditOnHold.Should().Be(true);
        
        contact.EmployeeId.Should().NotBeNull();
        contact.EmployeeId.Length.Should().Be(5);
        
        contact.LastName.Should().Be(Arthur.Contact().LastName.ToUpper());
    }
}
