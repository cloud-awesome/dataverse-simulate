using System;
using System.Collections.Generic;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test;

[TestFixture]
public class OrganisationServiceSimulatorTests
{
    private readonly IOrganizationService _organizationService = null!;
    
    [Test]
    public void Simulate_Org_Service_Returns_Mocked_Instance()
    {
        var orgService = _organizationService.Simulate();
        orgService.Should().NotBeNull();
    }

    [Test]
    public void Simulate_Org_Service_Should_Initiate_Data_Store()
    {
        var orgService = _organizationService.Simulate();
        orgService.Simulated().Data().Should().NotBeNull();
    }

    [Test]
    public void Initialising_With_An_Authenticated_User_Should_Store_Reference()
    {
        var options = new SimulatorOptions
        {
            AuthenticatedUser = new Entity("systemuser", Guid.NewGuid())
            {
                Attributes =
                {
                    ["fullname"] = "Gemma Armstrong"
                }
            }
        };

        var orgService = _organizationService.Simulate(options);

        orgService.Simulated().Data().AuthenticatedUser.Should().NotBeNull();
        orgService.Simulated().Data().AuthenticatedUser.Id.Should().Be(options.AuthenticatedUser.Id);
    }

    [Test]
    public void Initialising_With_An_Authenticated_User_Should_Add_To_User_Data_Store()
    {
        var options = new SimulatorOptions
        {
            AuthenticatedUser = new Entity("systemuser", Guid.NewGuid())
            {
                Attributes =
                {
                    ["fullname"] = "Gemma Armstrong"
                }
            }
        };

        var orgService = _organizationService.Simulate(options);
        
        var users = orgService.Simulated().Data().Get("systemuser");
        users.Count.Should().Be(1);
        users.FirstOrDefault()!.Attributes["fullname"].Should().Be("Gemma Armstrong");
    }

    [Test]
    public void Simulating_Service_With_PreInitialised_Data_Is_Correctly_Initialised()
    {
        var options = new SimulatorOptions
        {
            InitialiseData = new Dictionary<string, List<Entity>>
            {
                {
                    Contact.EntityLogicalName,
                    [
                        Arthur.Contact(),
                        Bruce.Contact()
                    ]
                },
                {
                    "account",
                    [
                        Arthur.Account()
                    ]
                }
            }
        };

        var orgService = _organizationService.Simulate(options);
        var contacts = orgService.Simulated().Data().Get("contact");
        var accounts = orgService.Simulated().Data().Get("account");
        var leads = orgService.Simulated().Data().Get("lead");

        contacts.Count.Should().Be(2);
        accounts.Count.Should().Be(1);
        leads.Count.Should().Be(0);
    }

    [Test]
    public void Simulating_Service_With_PreInitialised_Relationships_Is_Correctly_Initialised()
    {
        var relationship = new Relationship(Account.Fields.Account_Primary_Contact);
        var options = new SimulatorOptions
        {
            InitialiseData = new Dictionary<string, List<Entity>>
            {
                {
                    Contact.EntityLogicalName,
                    [Arthur.Contact()]
                },
                {
                    "account",
                    [Arthur.Account()]
                }
            },
            InitialiseRelationships =
            [
                new SimulatedRelationship
                {
                    Target = Arthur.Contact().ToEntityReference(),
                    Relationship = relationship,
                    RelatedEntities =
                    [
                        Arthur.Account().ToEntityReference()
                    ]
                }
            ]
        };

        var orgService = _organizationService.Simulate(options);

        orgService.Simulated().Data()
            .GetRelationships(Arthur.Contact().ToEntityReference(), relationship)
            .Should().ContainSingle();
    }

    [Test]
    public void Simulating_Service_With_PreInitialised_Relationships_Should_Throw_If_Record_Does_Not_Exist()
    {
        var options = new SimulatorOptions
        {
            InitialiseData = new Dictionary<string, List<Entity>>
            {
                {
                    Contact.EntityLogicalName,
                    [Arthur.Contact()]
                }
            },
            InitialiseRelationships =
            [
                new SimulatedRelationship
                {
                    Target = Arthur.Contact().ToEntityReference(),
                    Relationship = new Relationship(Account.Fields.Account_Primary_Contact),
                    RelatedEntities =
                    [
                        Arthur.Account().ToEntityReference()
                    ]
                }
            ]
        };

        var simulate = () => _organizationService.Simulate(options);

        simulate.Should().Throw<Exception>();
    }
}
