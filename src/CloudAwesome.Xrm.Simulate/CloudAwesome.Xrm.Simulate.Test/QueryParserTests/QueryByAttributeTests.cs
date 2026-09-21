using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture]
public class QueryByAttributeTests
{
    private IOrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = _organizationService.Simulate();
    }
    
    [Test]
    public void Query_By_Attribute_Returns_Valid_Single_Result()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var query = new QueryByAttribute(Contact.EntityLogicalName)
        {
            Attributes = { Contact.Fields.LastName },
            Values = { Arthur.Contact().LastName }
        };
        
        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
        contacts.Entities.FirstOrDefault()?.Attributes["firstname"].Should().Be("Arthur");
    }

    [Test]
    public void Query_By_Attribute_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Siobhan.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());

        var query = new QueryByAttribute(Contact.EntityLogicalName)
        {
            Attributes = { Contact.Fields.LastName },
            Values = { Siobhan.Contact().LastName }
        };

        var contacts = _organizationService.RetrieveMultiple(query);
        
        contacts.Entities.Count.Should().Be(2);
    }
    
    [Test]
    public void Query_By_Attribute_With_Multiple_Conditions_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Siobhan.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());

        var query = new QueryByAttribute(Contact.EntityLogicalName)
        {
            Attributes = { Contact.Fields.LastName, Contact.Fields.FirstName },
            Values = { Siobhan.Contact().LastName, Siobhan.Contact().FirstName }
        };

        var contacts = _organizationService.RetrieveMultiple(query);
        
        contacts.Entities.Count.Should().Be(1);
    }

    [Test]
    public void Query_By_Attribute_With_TopCount_Returns_Correct_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Bruce.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var query = new QueryByAttribute(Contact.EntityLogicalName)
        {
            TopCount = 2,
            Orders =
            {
                new OrderExpression(Contact.Fields.FirstName, OrderType.Descending)
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query).Entities.Cast<Contact>().ToList();

        contacts.Count.Should().Be(2);
        contacts[0].FirstName.Should().Be(Siobhan.Contact().FirstName);
        contacts[1].FirstName.Should().Be(Daniel.Contact().FirstName);
    }

    [Test]
    public void Query_By_Attribute_Applies_PageInfo_And_Metadata()
    {
        for (int i = 1; i <= 5; i++)
        {
            var entity = new Entity("ca_querybyattributepaged")
            {
                Id = Guid.NewGuid(),
                Attributes =
                {
                    ["ca_name"] = $"Record {i}",
                    ["ca_bucket"] = "same",
                    ["ca_sort"] = i
                }
            };

            _organizationService.Simulated().Data().Add(entity);
        }

        var query = new QueryByAttribute("ca_querybyattributepaged")
        {
            ColumnSet = new ColumnSet("ca_name"),
            Attributes = { "ca_bucket" },
            Values = { "same" },
            PageInfo = new PagingInfo
            {
                PageNumber = 2,
                Count = 2,
                ReturnTotalRecordCount = true
            },
            Orders =
            {
                new OrderExpression("ca_sort", OrderType.Ascending)
            }
        };

        var results = _organizationService.RetrieveMultiple(query);

        results.Entities
            .Select(entity => entity.GetAttributeValue<string>("ca_name"))
            .Should()
            .Equal("Record 3", "Record 4");
        results.MoreRecords.Should().BeTrue();
        results.PagingCookie.Should().NotBeNullOrWhiteSpace();
        results.TotalRecordCount.Should().Be(5);
    }
}
