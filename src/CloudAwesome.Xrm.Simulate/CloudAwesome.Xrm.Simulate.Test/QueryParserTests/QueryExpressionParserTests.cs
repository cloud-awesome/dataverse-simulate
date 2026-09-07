using System;
using System.Collections.Generic;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;
using Contact = CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities.Contact;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture(Description = "N.B. Filters and LinkEntity currently only work if you've included the attributes in the ColumnSet")]
public class QueryExpressionParserTests
{
    private IOrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = _organizationService.Simulate();
    }

    [Test]
    public void Retrieve_Multiple_With_Equals_Operator_On_String_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var query = new QueryExpression
        {
            EntityName = Arthur.Contact().LogicalName,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("firstname", ConditionOperator.Equal,
                        "Arthur")
                }
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
        contacts.Entities.FirstOrDefault()?.Attributes["firstname"].Should().Be("Arthur");
    }
    
    [Test]
    public void Retrieve_Multiple_Via_OrgService_Execute_Method_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var query = new QueryExpression
        {
            EntityName = Arthur.Contact().LogicalName,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("firstname", ConditionOperator.Equal,
                        "Arthur")
                }
            }
        };

        var retrieveMultipleRequest = new RetrieveMultipleRequest { Query = query };
        var response = (RetrieveMultipleResponse) _organizationService.Execute(retrieveMultipleRequest);

        response.EntityCollection.Entities.Count.Should().Be(1);
        response.EntityCollection.Entities.FirstOrDefault()?.Attributes["firstname"].Should().Be("Arthur");
    }
    
    [Test]
    public void Retrieve_Multiple_With_NotEquals_Operator_On_String_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var query = new QueryExpression
        {
            EntityName = Arthur.Contact().LogicalName,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("firstname", ConditionOperator.NotEqual,
                        "Arthur")
                }
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(0);
    }
    
    [Test]
    public void Retrieve_Multiple_On_String_Returns_Valid_Columns()
    {
        // Arrange
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var query = new QueryExpression
        {
            EntityName = Arthur.Contact().LogicalName,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("firstname", ConditionOperator.Equal,
                        "Arthur")
                }
            },
            ColumnSet = new ColumnSet("firstname")
        };

        // Act
        var contacts = _organizationService.RetrieveMultiple(query);

        // Assert
        contacts.Entities.Count.Should().Be(1);
        contacts.Entities.FirstOrDefault()?.Attributes["firstname"].Should().Be("Arthur");
        contacts.Entities.FirstOrDefault()?.Attributes[Contact.PrimaryIdAttribute].Should().Be(Arthur.Contact().Id);
        
        var retrieveLastName = () => 
            (contacts.Entities.FirstOrDefault()?.Attributes["lastname"]);
        retrieveLastName.Should().Throw<KeyNotFoundException>();
    }

    [Test]
    public void Retrieve_Multiple_Returns_Primary_Id_Attribute_When_Not_In_ColumnSet()
    {
        var recordId = Guid.NewGuid();
        var record = new Entity("ca_primaryprojection") { Id = recordId };
        record["ca_primaryprojectionid"] = recordId;
        record["ca_name"] = "Primary projection";
        record["ca_hidden"] = "Not requested";

        _organizationService.Simulated().Data().Add(record);

        var query = new QueryExpression
        {
            EntityName = "ca_primaryprojection",
            ColumnSet = new ColumnSet("ca_name")
        };

        var result = _organizationService.RetrieveMultiple(query).Entities
            .Should()
            .ContainSingle()
            .Subject;

        result.Id.Should().Be(recordId);
        result["ca_primaryprojectionid"].Should().Be(recordId);
        result.Contains("ca_hidden").Should().BeFalse();
    }

    [Test]
    public void Retrieve_Multiple_On_String_Returns_Valid_Order()
    {
        // Arrange
        _organizationService.Simulated().Data().Add(Bruce.Contact());
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var query = new QueryExpression
        {
            EntityName = Arthur.Contact().LogicalName,
            Orders =
            {
                new OrderExpression("lastname", OrderType.Descending)
            }
        };
        
        // Act
        var contacts = _organizationService.RetrieveMultiple(query);
        
        // Assert
        contacts.Entities.Count.Should().Be(2);

        contacts.Entities[0].Attributes["firstname"].Should().Be(Bruce.Contact().Attributes["firstname"]);
        contacts.Entities[1].Attributes["firstname"].Should().Be(Arthur.Contact().Attributes["firstname"]);
    }

    [Test]
    public void Retrieve_Multiple_On_DateTime_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(Contact.Fields.Birthdate,
                        ConditionOperator.Equal, new DateTime(1984, 12, 14))
                }
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
        contacts.Entities.Cast<Contact>().FirstOrDefault()?
            .FirstName.Should().Be(Arthur.Contact().FirstName);
    }

    [Test]
    public void Retrieve_Multiple_On_EntityReference_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(Contact.Fields.ParentCustomerId,
                        ConditionOperator.Equal, Arthur.Contact().ParentCustomerId)
                }
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
        contacts.Entities.Cast<Contact>().FirstOrDefault()?
            .FirstName.Should().Be(Arthur.Contact().FirstName);
    }
    
    [Test]
    public void Retrieve_Multiple_On_OptionSet_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(Contact.Fields.GenderCode,
                        ConditionOperator.Equal, (int) Contact_GenderCode.Male)
                }
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
        contacts.Entities.Cast<Contact>().FirstOrDefault()?
            .FirstName.Should().Be(Arthur.Contact().FirstName);
    }
    
    [Test]
    public void Retrieve_Multiple_On_OptionSet_NotEqual_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(Contact.Fields.GenderCode,
                        ConditionOperator.NotEqual, (int) Contact_GenderCode.Male)
                }
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
        contacts.Entities.Cast<Contact>().FirstOrDefault()?
            .FirstName.Should().Be(Siobhan.Contact().FirstName);
    }
    
    [Test]
    public void Retrieve_Multiple_On_OptionSet_When_No_Results_Found_Returns_Valid_Results()
    {
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(Contact.Fields.GenderCode,
                        ConditionOperator.Equal, (int) Contact_GenderCode.Male)
                }
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(0);
    }

    [Test]
    public void Retrieve_Multiple_Supports_Multiple_Child_FilterExpressions_With_OR_Clauses()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());
        _organizationService.Simulated().Data().Add(Bruce.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());

        // Query for contacts where
        //      (status is active AND gender is male) AND (LastName is 'Nicholson' OR 'Nicholson-Gumula')
        // So should include Arthur and Daniel (gender and LastNames),
        //      and exclude Siobhan (gender) and Bruce (LastName)  
        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            Criteria = new FilterExpression
            {
                Filters =
                {
                    new FilterExpression(LogicalOperator.And)
                    {
                        Conditions =
                        {
                            new ConditionExpression(Contact.Fields.StatusCode, 
                                ConditionOperator.Equal, (int) Contact_StatusCode.Active),
                            new ConditionExpression(Contact.Fields.GenderCode,
                                ConditionOperator.Equal, (int) Contact_GenderCode.Male)
                        }
                    },
                    new FilterExpression(LogicalOperator.Or)
                    {
                        Conditions =
                        {
                            new ConditionExpression(Contact.Fields.LastName,
                                ConditionOperator.Equal, "Nicholson"),
                            new ConditionExpression(Contact.Fields.LastName,
                                ConditionOperator.Equal, "Nicholson-Gumula")
                        }
                    }
                }
            },
            Orders =
            {
                new OrderExpression(Contact.Fields.FirstName, OrderType.Ascending)
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(2);
    }

    [Test]
    public void Retrieve_Multiple_Supports_Basic_LinkEntities_Columns()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Arthur.Account());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            ColumnSet = new ColumnSet(Contact.Fields.FirstName, 
                Contact.Fields.LastName,
                Contact.Fields.ParentCustomerId),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(Contact.Fields.FirstName,
                        ConditionOperator.Equal, "Arthur"),
                    new ConditionExpression(Contact.Fields.LastName,
                        ConditionOperator.Equal, "Nicholson-Gumula")
                }
            },
            LinkEntities =
            {
                new LinkEntity
                {
                    LinkFromAttributeName = Contact.Fields.ParentCustomerId,
                    LinkFromEntityName = Contact.EntityLogicalName,
                    LinkToAttributeName = "Id",
                    LinkToEntityName = "account",
                    Columns = new ColumnSet("name"),
                    EntityAlias = "account"
                }
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(1);
    }
    
    [Test]
    [Ignore("TODO - To be implemented")]
    public void Retrieve_Multiple_Returns_Valid_Results_If_Filter_Attributes_Are_Not_In_ColumnSet()
    {
      
    }
    
    [Test]
    [Ignore("TODO - To be implemented")]
    public void Retrieve_Multiple_Returns_Valid_Results_If_LinkEntity_Attributes_Are_Not_In_ColumnSet()
    {
      
    }

    [Test]
    public void Retrieve_Multiple_With_TopCount_Returns_Correct_Number_Of_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Bruce.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var query = new QueryExpression
        {
            EntityName = Arthur.Contact().LogicalName,
            TopCount = 2
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(2);
    }
    
    [Test]
    public void Retrieve_Multiple_With_TopCount_Returns_All_Results_If_Too_Many()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Bruce.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var query = new QueryExpression
        {
            EntityName = Arthur.Contact().LogicalName,
            TopCount = 10
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.Count.Should().Be(4);
    } 
    
    [Test]
    public void Retrieve_Multiple_With_TopCount_Returns_Correctly_Ordered_Results()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());
        _organizationService.Simulated().Data().Add(Bruce.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var query = new QueryExpression
        {
            EntityName = Arthur.Contact().LogicalName,
            TopCount = 2,
            Orders =
            {
                new OrderExpression(Contact.Fields.FirstName, OrderType.Descending)
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query).Entities.Cast<Contact>().ToList();

        contacts.Count().Should().Be(2);
        contacts[0].FirstName.Should().Be(Siobhan.Contact().FirstName);
        contacts[1].FirstName.Should().Be(Daniel.Contact().FirstName);
    } 

    [Test]
    public void Retrieve_Multiple_Can_Order_By_Attribute_Not_Returned_In_ColumnSet()
    {
        var late = new Entity("ca_orderprojection") { Id = Guid.NewGuid() };
        late["ca_name"] = "Late";
        late["ca_sort"] = 2;

        var early = new Entity("ca_orderprojection") { Id = Guid.NewGuid() };
        early["ca_name"] = "Early";
        early["ca_sort"] = 1;

        _organizationService.Simulated().Data().Add(late);
        _organizationService.Simulated().Data().Add(early);

        var query = new QueryExpression
        {
            EntityName = "ca_orderprojection",
            ColumnSet = new ColumnSet("ca_name"),
            Orders =
            {
                new OrderExpression("ca_sort", OrderType.Ascending)
            }
        };

        var results = _organizationService.RetrieveMultiple(query).Entities;

        results.Select(entity => entity.GetAttributeValue<string>("ca_name"))
            .Should().ContainInOrder("Early", "Late");
        results.Should().OnlyContain(entity => !entity.Contains("ca_sort"));
    }
    
    [Test]
    public void Retrieve_Multiple_Accurately_Respects_Distinct_Equals_True()
    {
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            Distinct = true
        };
            
        var contacts = _organizationService.RetrieveMultiple(query).Entities.Cast<Contact>().ToList();

        contacts.Count().Should().Be(1);
    }

    [Test(Description = "QueryExpression distinct should compare the selected output columns rather than hidden source attributes or record identity.")]
    public void Retrieve_Multiple_Distinct_Uses_Selected_ColumnSet()
    {
        var first = new Entity("ca_distinctprojection") { Id = Guid.NewGuid() };
        first["ca_distinctprojectionid"] = first.Id;
        first["ca_name"] = "Duplicate value";
        first["ca_hidden"] = "First hidden value";

        var second = new Entity("ca_distinctprojection") { Id = Guid.NewGuid() };
        second["ca_distinctprojectionid"] = second.Id;
        second["ca_name"] = "Duplicate value";
        second["ca_hidden"] = "Second hidden value";

        _organizationService.Simulated().Data().Add(first);
        _organizationService.Simulated().Data().Add(second);

        var query = new QueryExpression
        {
            EntityName = "ca_distinctprojection",
            ColumnSet = new ColumnSet("ca_name"),
            Distinct = true
        };

        var results = _organizationService.RetrieveMultiple(query).Entities;

        results.Should().ContainSingle();
        results[0]["ca_name"].Should().Be("Duplicate value");
        results[0].Contains("ca_hidden").Should().BeFalse();
        results[0].Contains("ca_distinctprojectionid").Should().BeFalse();
    }
    
    [Test]
    public void Retrieve_Multiple_Accurately_Respects_Distinct_Equals_False()
    {
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        _organizationService.Simulated().Data().Add(Daniel.Contact());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            Distinct = false
        };
            
        var contacts = _organizationService.RetrieveMultiple(query).Entities.Cast<Contact>().ToList();

        contacts.Count().Should().Be(2);
    }

    [Test]
    public void ReturnTotalRecord_Is_Negative_One_If_Not_Requested()
    {
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.ToList().Count.Should().Be(2);
        contacts.TotalRecordCount.Should().Be(-1);
    }
    
    [Test]
    public void ReturnTotalRecord_Is_Correct_Value_If_Requested()
    {
        _organizationService.Simulated().Data().Add(Daniel.Contact());
        _organizationService.Simulated().Data().Add(Siobhan.Contact());

        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            PageInfo = new PagingInfo
            {
                ReturnTotalRecordCount = true
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.ToList().Count.Should().Be(2);
        contacts.TotalRecordCount.Should().Be(2);
        contacts.TotalRecordCountLimitExceeded.Should().Be(false);
    }
    
    [Test]
    public void ReturnTotalRecord_Maxes_Out_At_5000()
    {
        for (int i = 0; i < 5100; i++)
        {
            _organizationService.Simulated().Data().Add(new Contact(new Guid()));    
        }
        
        var query = new QueryExpression
        {
            EntityName = Contact.EntityLogicalName,
            PageInfo = new PagingInfo
            {
                ReturnTotalRecordCount = true
            }
        };

        var contacts = _organizationService.RetrieveMultiple(query);

        contacts.Entities.ToList().Count.Should().Be(5000);
        contacts.TotalRecordCount.Should().Be(5000);
        contacts.TotalRecordCountLimitExceeded.Should().Be(true);
    }
}
