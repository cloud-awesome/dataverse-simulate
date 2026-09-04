using System.Collections.Generic;
using System.Linq;
using CloudAwesome.Xrm.Simulate.QueryParsers;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture(Description = "N.B. Filters and LinkEntity currently only work if you've included the attributes in the ColumnSet")]
public class FetchExpressionParserTests
{
    private IOrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = _organizationService.Simulate();
    }
    
    [Test]
    public void Basic_FetchXml_String_Is_Correctly_Converted_To_Query_Expression()
    {
        var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                       <entity name=""contact"">
                        <attribute name=""LastName"" />
                        <attribute name=""FirstName"" />
                        <order attribute=""FirstName"" descending=""false"" />
                        <filter type=""and"">
                          <condition attribute=""LastName"" operator=""eq"" value=""Nicholson"" />
                        </filter>
                      </entity>
                    </fetch>";

        var qe = FetchExpressionParser.ConvertFetchXmlToQueryExpression(fetch);

        qe.Should().NotBeNull();
        qe.EntityName.Should().Be(Contact.EntityLogicalName);
        qe.ColumnSet.Columns.Count.Should().Be(2);
        qe.Criteria.Should().NotBeNull();
        qe.Criteria.Conditions.Count.Should().Be(1);
        qe.Orders.Count.Should().Be(1);
    }

    [Test]
    public void FetchXml_String_With_LinkEntity_Is_Converted_To_Query_Expressions()
    {
      var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"">
                      <entity name=""contact"">
                        <attribute name=""LastName"" />
                        <attribute name=""FirstName"" />
                        <filter type=""and"">
                          <condition attribute=""LastName"" operator=""eq"" value=""Nicholson-Gumula"" />
                        </filter>
                        <link-entity name=""account"" from=""Id"" to=""parentcustomerid"" visible=""false"" link-type=""outer"" alias=""account"">
                          <attribute name=""name"" />
                        </link-entity>                
                      </entity>
                    </fetch>";

      var qe = FetchExpressionParser.ConvertFetchXmlToQueryExpression(fetch);

      qe.Should().NotBeNull();
      qe.LinkEntities.Count.Should().Be(1);
      qe.LinkEntities.FirstOrDefault()?.EntityAlias.Should().Be("account");
      qe.LinkEntities.FirstOrDefault()?.LinkFromEntityName.Should().Be("contact");
      qe.LinkEntities.FirstOrDefault()?.LinkToEntityName.Should().Be("account");
      qe.LinkEntities.FirstOrDefault()?.Columns.Columns.Count.Should().Be(1);
    }
    
    [Test]
    public void Basic_Fetch_Returns_Correct_Records()
    { 
        _organizationService.Simulated().Data().Add(Siobhan.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        // Should not be returned in query
        _organizationService.Simulated().Data().Add(Arthur.Contact()); 
        
        var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                       <entity name=""contact"">
                        <attribute name=""lastname"" />
                        <attribute name=""firstname"" />
                        <filter type=""and"">
                          <condition attribute=""lastname"" operator=""eq"" value=""Nicholson"" />
                        </filter>
                      </entity>
                    </fetch>";

        var query = new FetchExpression { Query = fetch };
        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(2);
    }

    [Test]
    public void Basic_Fetch_With_No_Results_Returns_Empty_Dataset()
    {
        var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                         <entity name=""contact"">
                          <attribute name=""LastName"" />
                          <attribute name=""FirstName"" />
                          <filter type=""and"">
                            <condition attribute=""LastName"" operator=""eq"" value=""Nicholson"" />
                          </filter>
                        </entity>
                      </fetch>";

        var query = new FetchExpression { Query = fetch };
        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(0);
    }
    
    [Test]
    public void Basic_NotEquals_Fetch_Returns_Correct_Records()
    {
        _organizationService.Simulated().Data().Add(Bruce.Contact());
        // Should not be returned in query
        _organizationService.Simulated().Data().Add(Siobhan.Contact());
        
        var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                       <entity name=""contact"">
                        <attribute name=""lastname"" />
                        <attribute name=""firstname"" />
                        <filter type=""and"">
                          <condition attribute=""lastname"" operator=""ne"" value=""Nicholson"" />
                        </filter>
                      </entity>
                    </fetch>";

        var query = new FetchExpression { Query = fetch };
        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
    }
    
    [Test]
    public void Basic_Fetch_Returns_Correct_Columns()
    {
        _organizationService.Simulated().Data().Add(Siobhan.Contact());
        _organizationService.Simulated().Data().Add(Arthur.Contact()); 
          
        var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                         <entity name=""contact"">
                          <attribute name=""lastname"" />
                          <attribute name=""firstname"" />
                          <filter type=""and"">
                            <condition attribute=""lastname"" operator=""eq"" value=""Nicholson"" />
                          </filter>
                        </entity>
                      </fetch>";

        var query = new FetchExpression { Query = fetch };
        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);

        contacts.Entities.FirstOrDefault()?.Attributes[Contact.Fields.FirstName].Should().Be(Siobhan.Contact().FirstName);
        contacts.Entities.FirstOrDefault()?.Attributes[Contact.Fields.LastName].Should().Be(Siobhan.Contact().LastName);

        var sut = () => contacts.Entities.FirstOrDefault()?.Attributes[Contact.Fields.Birthdate];
        sut.Should().Throw<KeyNotFoundException>();
    }

    [Test]
    public void Basic_Fetch_Query_Returns_Correct_Order()
    {
        _organizationService.Simulated().Data().Add(Siobhan.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact()); 
        
        var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                        <entity name=""contact"">
                          <attribute name=""lastname"" />
                          <attribute name=""firstname"" />
                          <order attribute=""firstname"" descending=""false"" />
                          <filter type=""and"">
                            <condition attribute=""lastname"" operator=""eq"" value=""Nicholson"" />
                          </filter>
                        </entity>
                      </fetch>";
        
        var query = new FetchExpression { Query = fetch };
        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(2);
        contacts.Entities[0].Attributes[Contact.Fields.FirstName].Should().Be(Daniel.Contact().FirstName);
        contacts.Entities[1].Attributes[Contact.Fields.FirstName].Should().Be(Siobhan.Contact().FirstName);
    }

    [Test]
    public void Fetch_Query_With_Linked_Entity_Returns_Correct_Related_Columns()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Arthur.Account());
        
        var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"">
                        <entity name=""contact"">
                          <attribute name=""lastname"" />
                          <attribute name=""firstname"" />
                          <attribute name=""parentcustomerid"" />
                          <filter type=""and"">
                            <condition attribute=""lastname"" operator=""eq"" value=""Nicholson-Gumula"" />
                          </filter>
                          <link-entity name=""account"" to=""Id"" from=""parentcustomerid"" visible=""false"" link-type=""outer"" alias=""account"">
                            <attribute name=""name"" />
                            <attribute name=""Id"" />
                          </link-entity>                
                        </entity>
                      </fetch>";
        
        var query = new FetchExpression { Query = fetch };
        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
        contacts.Entities.FirstOrDefault()?.Attributes["account.name"]
          .Should().BeOfType<AliasedValue>()
          .Which.Value.Should().Be(Arthur.Account().Attributes["name"]);
    }

    [Test]
    public void Fetch_Query_With_Linked_Filters_Returned_Dataset()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Arthur.Account());
        _organizationService.Simulated().Data().Add(Daniel.Contact()); // Should not be returned in results
        
        var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"">
                        <entity name=""contact"">
                          <attribute name=""lastname"" />
                          <attribute name=""firstname"" />
                          <attribute name=""parentcustomerid"" />
                          <order attribute=""firstname"" descending=""false"" />
                          <link-entity name=""account"" to=""Id"" from=""parentcustomerid"" visible=""false"" link-type=""inner"" alias=""account"">
                            <attribute name=""name"" />
                            <attribute name=""Id"" />
                            <filter type=""and"">
                              <condition attribute=""name"" operator=""eq"" value=""Cloud Awesome Limited"" />
                            </filter>
                          </link-entity>
                        </entity>
                      </fetch>";
        
        var query = new FetchExpression { Query = fetch };
        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
    }
    
    [Test]
    public void Fetch_Query_Returns_Top_x_Results()
    {
      _organizationService.Simulated().Data().Add(Arthur.Contact());
      _organizationService.Simulated().Data().Add(Bruce.Contact());
      _organizationService.Simulated().Data().Add(Daniel.Contact());
      _organizationService.Simulated().Data().Add(Siobhan.Contact());
        
      var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"" top=""2"">
                        <entity name=""contact"">
                          <attribute name=""lastname"" />
                          <attribute name=""firstname"" />
                          <order attribute=""firstname"" descending=""false"" />
                        </entity>
                      </fetch>";
        
      var query = new FetchExpression { Query = fetch };
      var contacts = _organizationService.RetrieveMultiple(query);

      contacts.Entities.Count.Should().Be(2);
    }
    
    [Test]
    public void Fetch_Query_With_Top_x_Returns_Correct_Results()
    {
      _organizationService.Simulated().Data().Add(Arthur.Contact());
      _organizationService.Simulated().Data().Add(Bruce.Contact());
      _organizationService.Simulated().Data().Add(Daniel.Contact());
      _organizationService.Simulated().Data().Add(Siobhan.Contact());
        
      var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"" top=""2"">
                        <entity name=""contact"">
                          <order attribute=""firstname"" descending=""true"" />
                        </entity>
                      </fetch>";
        
      var query = new FetchExpression { Query = fetch };
      var contacts = _organizationService.RetrieveMultiple(query).Entities.Cast<Contact>().ToList();

      contacts.Count().Should().Be(2);
      contacts[0].FirstName.Should().Be(Siobhan.Contact().FirstName);
      contacts[1].FirstName.Should().Be(Daniel.Contact().FirstName);
    }

    [Test]
    public void Fetch_Query_Accurately_Respects_Distinct_Equals_True()
    {
      _organizationService.Simulated().Data().Add(Daniel.Contact());
      _organizationService.Simulated().Data().Add(Daniel.Contact());
        
      var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""true"">
                        <entity name=""contact"">
                          <order attribute=""firstname"" descending=""true"" />
                        </entity>
                      </fetch>";
      
      var query = new FetchExpression { Query = fetch };
      var contacts = _organizationService.RetrieveMultiple(query).Entities.Cast<Contact>().ToList();

      contacts.Count().Should().Be(1);
    }
    
    [Test]
    public void Fetch_Query_Accurately_Respects_Distinct_Equals_False()
    {
      _organizationService.Simulated().Data().Add(Daniel.Contact());
      _organizationService.Simulated().Data().Add(Daniel.Contact());
        
      var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""false"">
                        <entity name=""contact"">
                          <order attribute=""FirstName"" descending=""true"" />
                        </entity>
                      </fetch>";
        
      var query = new FetchExpression { Query = fetch };
      var contacts = _organizationService.RetrieveMultiple(query).Entities.Cast<Contact>().ToList();

      contacts.Count().Should().Be(2);
    }
    
    [Test]
    public void Fetch_Query_Defaults_Distinct_To_False_If_Not_Provided()
    {
      _organizationService.Simulated().Data().Add(Daniel.Contact());
      _organizationService.Simulated().Data().Add(Daniel.Contact());
        
      var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"">
                        <entity name=""contact"">
                          <order attribute=""FirstName"" descending=""true"" />
                        </entity>
                      </fetch>";
        
      var query = new FetchExpression { Query = fetch };
      var contacts = _organizationService.RetrieveMultiple(query).Entities.Cast<Contact>().ToList();

      contacts.Count().Should().Be(2);
    }
    
    [Test]
    [TestCase(true, 1)]
    [TestCase(false, 2)]
    public void Fetch_Query_With_Linked_Filters_Respects_Distinct(bool distinct, int expectedNumberOfResults)
    {
      _organizationService.Simulated().Data().Add(Arthur.Contact());
      _organizationService.Simulated().Data().Add(Arthur.Contact()); 
      _organizationService.Simulated().Data().Add(Arthur.Account());
      _organizationService.Simulated().Data().Add(Daniel.Contact()); // Should not be returned in results
        
      var fetch = $@"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"" distinct=""{distinct}"">
                        <entity name=""contact"">
                          <attribute name=""lastname"" />
                          <attribute name=""firstname"" />
                          <attribute name=""parentcustomerid"" />
                          <order attribute=""firstname"" descending=""false"" />
                          <link-entity name=""account"" to=""Id"" from=""parentcustomerid"" visible=""false"" link-type=""inner"" alias=""account"">
                            <attribute name=""name"" />
                            <attribute name=""Id"" />
                            <filter type=""and"">
                              <condition attribute=""name"" operator=""eq"" value=""Cloud Awesome Limited"" />
                            </filter>
                          </link-entity>
                        </entity>
                      </fetch>";
        
      var query = new FetchExpression { Query = fetch };
      var contacts = _organizationService.RetrieveMultiple(query);

      contacts.Entities.Count.Should().Be(expectedNumberOfResults);
    }
    
    #region Functionality To Be Implemented
    
    [Test]
    [Ignore("TODO - To be implemented")]
    public void Fetch_Query_Returns_Valid_Results_If_Filter_Attributes_Are_Not_In_ColumnSet()
    {
      
    }
    
    [Test]
    [Ignore("TODO - To be implemented")]
    public void Fetch_Query_Returns_Valid_Results_If_LinkEntity_Attributes_Are_Not_In_ColumnSet()
    {
      
    }

    #endregion
}
