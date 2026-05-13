using System;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.DataServicesTests;

[TestFixture]
public class EntityDataServiceTests
{
    private IOrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = _organizationService.Simulate();
    }
    
    [Test]
    public void Initialise_Data_Store_Should_Correctly_Save_Entities()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        var contacts = _organizationService.Simulated().Data().Get(Arthur.Contact().LogicalName);
        contacts.Count.Should().Be(1);
    }
    
    [Test]
    public void Initialise_Data_Store_With_No_Accounts_Should_Return_Empty_List()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        var contacts = _organizationService.Simulated().Data().Get(Arthur.Account().LogicalName);
        contacts.Count.Should().Be(0);
    }

    [Test]
    public void Clearing_Data_Should_Reinitialise_The_Data_Store()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Reinitialise();

        var contacts = _organizationService.Simulated().Data().Get(Arthur.Contact().LogicalName);
        contacts.Count.Should().Be(0);
    }

    [Test]
    public void Initialise_Multiple_Entities_Should_Correctly_Save()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Arthur.Account());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var contacts = _organizationService.Simulated().Data().Get(Arthur.Contact().LogicalName);
        var accounts = _organizationService.Simulated().Data().Get(Arthur.Account().LogicalName);
        var leads = _organizationService.Simulated().Data().Get("lead");

        contacts.Count.Should().Be(2);
        accounts.Count.Should().Be(1);
        leads.Count.Should().Be(0);
    }

    [Test]
    public void Simulated_Data_Service_Can_Retrieve_Early_Bound_Entity_Record()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        
        var contact = _organizationService.Simulated().Data().Get<Contact>(Arthur.Contact().Id);
        
        contact.Should().NotBeNull();
        contact.Should().BeOfType<Contact>();
        contact.Id.Should().Be(Arthur.Contact().Id);
        contact.FirstName.Should().Be(Arthur.Contact().FirstName);
    }
    
    [Test]
    public void Simulated_Data_Service_Can_Retrieve_Early_Bound_Entity_Record_Only_If_User_Has_Simulated_Them()
    {
        _organizationService.Simulated().Data().Add(Arthur.Account());
        
        var getAccount = () => _organizationService.Simulated().Data().Get<Account>(Arthur.Account().Id);
        
        getAccount.Should().Throw<InvalidCastException>()
            .WithMessage("*Ensure you are using Early Bound Entities to use this function.");
    }

    [Test]
    public void Simulated_Data_Service_Can_Retrieve_Early_Bound_Entity_Types()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        
        var contacts = _organizationService.Simulated().Data().Get<Contact>();
        
        contacts.Count.Should().Be(3);
    }
    
    [Test]
    public void Simulated_Data_Service_Can_Retrieve_Early_Bound_Entity_Types_Only_If_User_Has_Simulated_Them()
    {
        _organizationService.Simulated().Data().Add(Arthur.Account());
        _organizationService.Simulated().Data().Add(Bruce.Account());
        _organizationService.Simulated().Data().Add(Siobhan.Account());
        
        var getAccounts = () => _organizationService.Simulated().Data().Get<Account>();
        
        getAccounts.Should().Throw<InvalidCastException>()
            .WithMessage("*Ensure you are using Early Bound Entities to use this function.");
    }
}