using System;
using System.IO;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Metadata;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;
using Contact = CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities.Contact;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture]
public sealed class QueryInputOutputSemanticsTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Empty_ColumnSet_Returns_Only_Primary_Id(bool queryByAttribute)
    {
        var service = CreateService();
        var row = Row("Alpha");
        row["ca_otherid"] = row.Id;
        service.Simulated().Data().Add(row);
        QueryBase query = queryByAttribute
            ? new QueryByAttribute("ca_row")
            {
                ColumnSet = new ColumnSet(false), Attributes = { "ca_name" }, Values = { "Alpha" }
            }
            : new QueryExpression("ca_row") { ColumnSet = new ColumnSet(false) };

        var result = service.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result.Id.Should().Be(row.Id);
        result.Attributes.Keys.Should().Equal("ca_rowid");
        result["ca_rowid"].Should().Be(row.Id);
        row.Contains("ca_rowid").Should().BeFalse("projection must not modify stored rows");
    }

    [TestCase("QueryExpression")]
    [TestCase("QueryByAttribute")]
    [TestCase("FetchXML")]
    public void Projection_Uses_Metadata_Primary_Id_And_Does_Not_Expose_Other_Guids(string queryForm)
    {
        var service = CreateService(withMetadata: true);
        var row = Row("Alpha");
        row["ca_relatedid"] = row.Id;
        service.Simulated().Data().Add(row);
        QueryBase query = queryForm switch
        {
            "QueryByAttribute" => new QueryByAttribute("ca_row")
            {
                ColumnSet = new ColumnSet("ca_name"), Attributes = { "ca_name" }, Values = { "Alpha" }
            },
            "FetchXML" => new FetchExpression(
                "<fetch><entity name='ca_row'><attribute name='ca_name'/></entity></fetch>"),
            _ => new QueryExpression("ca_row") { ColumnSet = new ColumnSet("ca_name") }
        };

        var result = service.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result.Attributes.Keys.Should().BeEquivalentTo("ca_key", "ca_name");
        result.Id.Should().Be(row.Id);
        result["ca_key"].Should().Be(row.Id);
        result.Contains("ca_rowid").Should().BeFalse();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Distinct_Selected_Columns_Remove_Source_Identity_Including_Early_Bound(bool earlyBound)
    {
        var service = CreateService();
        foreach (var unused in Enumerable.Range(0, 2))
        {
            Entity row = earlyBound ? new Contact() : new Entity("contact");
            row.Id = Guid.NewGuid();
            row["contactid"] = row.Id;
            row["firstname"] = "Alpha";
            service.Simulated().Data().Add(row);
        }

        var query = new QueryExpression("contact") { ColumnSet = new ColumnSet("firstname"), Distinct = true };
        var result = service.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result.Id.Should().BeEmpty();
        result.Attributes.Keys.Should().Equal("firstname");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Distinct_Explicit_Primary_Id_Preserves_Identity(bool allColumns)
    {
        var service = CreateService(withMetadata: true);
        var row = Row("Alpha");
        service.Simulated().Data().Add(row);
        var query = new QueryExpression("ca_row")
        {
            ColumnSet = allColumns ? new ColumnSet(true) : new ColumnSet("ca_key"), Distinct = true
        };

        var result = service.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result.Id.Should().Be(row.Id);
        result["ca_key"].Should().Be(row.Id);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Distinct_Empty_ColumnSet_Returns_Primary_Ids(bool withMetadata)
    {
        var service = CreateService(withMetadata);
        var first = Row("Alpha");
        var second = Row("Alpha");
        service.Simulated().Data().Add(first);
        service.Simulated().Data().Add(second);

        var query = new QueryExpression("ca_row")
        {
            ColumnSet = new ColumnSet(false), Distinct = true
        };
        var results = service.RetrieveMultiple(query).Entities;
        var primaryIdAttribute = withMetadata ? "ca_key" : "ca_rowid";

        results.Select(entity => entity.Id).Should().BeEquivalentTo(new[] { first.Id, second.Id });
        foreach (var result in results)
        {
            result.Attributes.Keys.Should().Equal(primaryIdAttribute);
            result[primaryIdAttribute].Should().Be(result.Id);
        }
        query.ColumnSet.Columns.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Distinct_Empty_Base_ColumnSet_Includes_Primary_Id_With_Links(bool selectLinkedName)
    {
        var service = CreateService();
        var first = Row("Alpha");
        var second = Row("Alpha");
        first["ca_rowid"] = first.Id;
        second["ca_rowid"] = second.Id;
        service.Simulated().Data().Add(first);
        service.Simulated().Data().Add(second);
        var query = new QueryExpression("ca_row")
        {
            ColumnSet = new ColumnSet(false), Distinct = true,
            LinkEntities =
            {
                new LinkEntity("ca_row", "ca_row", "ca_rowid", "ca_rowid", JoinOperator.Inner)
                {
                    EntityAlias = "linked",
                    Columns = selectLinkedName ? new ColumnSet("ca_name") : new ColumnSet(false)
                }
            }
        };

        var results = service.RetrieveMultiple(query).Entities;

        results.Select(entity => entity.Id).Should().BeEquivalentTo(new[] { first.Id, second.Id });
        foreach (var result in results)
        {
            result["ca_rowid"].Should().Be(result.Id);
            if (selectLinkedName)
            {
                result.Attributes.Keys.Should().BeEquivalentTo("ca_rowid", "linked.ca_name");
                result.GetAttributeValue<AliasedValue>("linked.ca_name").Value.Should().Be("Alpha");
            }
            else result.Attributes.Keys.Should().Equal("ca_rowid");
        }
    }

    [TestCase("")]
    [TestCase("<all-attributes/>")]
    public void FetchXml_Default_And_All_Attributes_Return_All_Base_Columns(string columns)
    {
        var service = CreateService();
        var row = Row("Alpha");
        service.Simulated().Data().Add(row);

        var result = service.RetrieveMultiple(new FetchExpression(
                $"<fetch><entity name='ca_row'>{columns}</entity></fetch>"))
            .Entities.Should().ContainSingle().Subject;

        result.Attributes.Keys.Should().BeEquivalentTo("ca_rowid", "ca_name", "ca_hidden");
        result.Id.Should().Be(row.Id);
    }

    [TestCase(
        "<filter type='or'><condition attribute='ca_name' operator='eq' value='Alpha'/><condition attribute='ca_name' operator='eq' value='Beta'/></filter>")]
    [TestCase(
        "<filter><condition attribute='ca_hidden' operator='eq' value='keep'/><filter type='or'><condition attribute='ca_name' operator='eq' value='Alpha'/><filter><condition attribute='ca_name' operator='eq' value='Beta'/></filter></filter></filter>")]
    [TestCase(
        "<filter type='or'><condition attribute='ca_name' operator='eq' value='Alpha'/><condition attribute='ca_name' operator='eq' value='Beta'/><condition attribute='ca_name' operator='eq' value='Other'/></filter><filter><condition attribute='ca_hidden' operator='eq' value='keep'/></filter>")]
    public void FetchXml_Preserves_Logical_Filter_Structure(string filter)
    {
        var service = CreateService();
        service.Simulated().Data().Add(Row("Alpha"));
        service.Simulated().Data().Add(Row("Beta"));
        service.Simulated().Data().Add(Row("Other", "discard"));
        var results = service.RetrieveMultiple(new FetchExpression(
            $"<fetch><entity name='ca_row'><attribute name='ca_name'/>{filter}</entity></fetch>"));

        results.Entities.Select(entity => entity.GetAttributeValue<string>("ca_name"))
            .Should().BeEquivalentTo("Alpha", "Beta");
        results.Entities.Should().OnlyContain(entity => !entity.Contains("ca_hidden"));
    }

    [Test]
    public void FetchXml_Linked_Filters_Preserve_Nested_Or_And_All_Attributes()
    {
        var service = CreateService();
        var parent = Row("Parent");
        parent["ca_rowid"] = parent.Id;
        service.Simulated().Data().Add(parent);
        foreach (var name in new[] { "Alpha", "Beta", "Other" })
        {
            service.Simulated().Data().Add(new Entity("ca_related")
            {
                Id = Guid.NewGuid(), ["ca_parentid"] = parent.Id, ["ca_name"] = name
            });
        }

        var results = service.RetrieveMultiple(new FetchExpression("""
                                                                   <fetch><entity name='ca_row'><attribute name='ca_name'/>
                                                                     <link-entity name='ca_related' from='ca_parentid' to='ca_rowid' alias='r'>
                                                                       <all-attributes/>
                                                                       <filter><filter type='or'>
                                                                         <condition attribute='ca_name' operator='eq' value='Alpha'/>
                                                                         <condition attribute='ca_name' operator='eq' value='Beta'/>
                                                                       </filter></filter>
                                                                     </link-entity>
                                                                   </entity></fetch>
                                                                   """));

        results.Entities.Select(entity => ((AliasedValue)entity["r.ca_name"]).Value)
            .Should().BeEquivalentTo(new object[] { "Alpha", "Beta" });
    }

    [TestCase("<condition attribute='ca_name' operator='equal' value='Alpha'/>")]
    [TestCase("<condition attribute='ca_name' operator='unknown' value='Alpha'/>")]
    [TestCase("<condition attribute='ca_name' operator='in'><value>Alpha</value></condition>")]
    [TestCase("<condition attribute='ca_name' operator='eq'><value>Alpha</value></condition>")]
    [TestCase("<condition attribute='ca_name' operator='eq' valueof='ca_hidden'/>")]
    [TestCase("<condition attribute='ca_name' operator='eq' entityname='r' value='Alpha'/>")]
    public void Unsupported_FetchXml_Conditions_Are_Rejected_Even_Without_Data(string condition)
    {
        var service = CreateService();
        var act = () => service.RetrieveMultiple(new FetchExpression(
            $"<fetch><entity name='ca_row'><filter>{condition}</filter></entity></fetch>"));

        act.Should().Throw<NotSupportedException>().WithMessage("FetchXML*");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("<fetch>")]
    [TestCase("<query><entity name='ca_row'/></query>")]
    [TestCase("<fetch/>")]
    [TestCase("<fetch><entity/></fetch>")]
    [TestCase("<fetch><entity name='ca_row'/><entity name='ca_other'/></fetch>")]
    [TestCase(
        "<fetch><entity name='ca_row'><filter><condition attribute='ca_name' operator='eq'/></filter></entity></fetch>")]
    [TestCase(
        "<fetch><entity name='ca_row'><filter><condition attribute='ca_name' operator='null' value='Alpha'/></filter></entity></fetch>")]
    public void Malformed_FetchXml_Has_An_Explicit_Input_Error(string? xml)
    {
        var service = CreateService();
        var act = () => service.RetrieveMultiple(new FetchExpression(xml));

        act.Should().Throw<ArgumentException>();
    }

    [TestCase("<entity name='unknown'/>")]
    [TestCase("<entity name='ca_row'><attribute name='unknown'/></entity>")]
    [TestCase("<entity name='ca_row'><attribute name='ca_secret'/></entity>")]
    [TestCase(
        "<entity name='ca_row'><filter><filter type='or'><condition attribute='unknown' operator='eq' value='Alpha'/></filter></filter></entity>")]
    [TestCase("<entity name='ca_row'><order attribute='unknown'/></entity>")]
    [TestCase("<entity name='ca_row'><link-entity name='unknown' from='id' to='ca_key'/></entity>")]
    [TestCase(
        "<entity name='ca_row'><link-entity name='ca_related' from='ca_relatedid' to='ca_relatedid'><attribute name='unknown'/></link-entity></entity>")]
    public void FetchXml_Validates_Metadata_Before_Execution(string entity)
    {
        var service = CreateService(withMetadata: true);
        var act = () => service.RetrieveMultiple(new FetchExpression($"<fetch>{entity}</fetch>"));

        act.Should().Throw<SimulatedMetadataException>();
    }

    [Test]
    public void Aggregate_Projection_And_Metadata_Validation_Use_Source_Columns_And_Order_Aliases()
    {
        var service = CreateService(withMetadata: true);
        var row = Row("Alpha");
        row["ca_amount"] = 3;
        service.Simulated().Data().Add(row);

        var result = service.RetrieveMultiple(new FetchExpression("""
                                                                  <fetch aggregate='true'><entity name='ca_row'>
                                                                    <attribute name='ca_amount' alias='total' aggregate='sum'/>
                                                                    <order alias='total'/>
                                                                  </entity></fetch>
                                                                  """)).Entities.Should().ContainSingle().Subject;

        result.Attributes.Keys.Should().Equal("total");
        result.GetAttributeValue<AliasedValue>("total").Value.Should().Be(3);
        var invalid = () => service.RetrieveMultiple(new FetchExpression("""
                                                                         <fetch aggregate='true'><entity name='ca_row'>
                                                                           <attribute name='unknown' alias='total' aggregate='sum'/>
                                                                         </entity></fetch>
                                                                         """));
        invalid.Should().Throw<SimulatedMetadataException>();
    }

    [Test]
    public void FetchXml_Equality_Preserves_Literal_Percent_And_Null_Has_No_Values()
    {
        var service = CreateService();
        service.Simulated().Data().Add(Row("100%"));
        service.Simulated().Data().Add(Row("100*"));
        var result = service.RetrieveMultiple(new FetchExpression("""
                                                                  <fetch><entity name='ca_row'><attribute name='ca_name'/><filter>
                                                                    <condition attribute='ca_name' operator='eq' value='100%'/>
                                                                    <condition attribute='missing' operator='null'/>
                                                                  </filter></entity></fetch>
                                                                  """)).Entities.Should().ContainSingle().Subject;

        result["ca_name"].Should().Be("100%");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Returned_Sdk_Values_Are_Isolated_From_Stored_Data(bool allColumns)
    {
        var service = CreateService();
        var row = Row("Alpha");
        row["ca_money"] = new Money(12m);
        row["ca_option"] = new OptionSetValue(1);
        row["ca_options"] = new OptionSetValueCollection { new(2) };
        row["ca_reference"] = new EntityReference("account", Guid.NewGuid()) { Name = "Original" };
        row["ca_bytes"] = new byte[] { 1, 2 };
        row["r.ca_money"] = new AliasedValue("ca_related", "ca_money", new Money(24m));
        row.KeyAttributes["ca_key"] = new Money(5m);
        var child = new Entity("ca_child") { ["ca_money"] = new Money(6m) };
        child["ca_parent"] = row; // SDK graphs may contain a reference back to a parent.
        row.RelatedEntities[new Relationship("ca_children")] = new EntityCollection(new[] { child });
        service.Simulated().Data().Add(row);

        var result = service.RetrieveMultiple(new QueryExpression("ca_row")
        {
            ColumnSet = allColumns
                ? new ColumnSet(true)
                : new ColumnSet(
                    "ca_money", "ca_option", "ca_options", "ca_reference", "ca_bytes")
        }).Entities.Should().ContainSingle().Subject;

        result.GetAttributeValue<Money>("ca_money").Value = 99m;
        result.GetAttributeValue<OptionSetValue>("ca_option").Value = 99;
        result.GetAttributeValue<OptionSetValueCollection>("ca_options")[0].Value = 99;
        result.GetAttributeValue<EntityReference>("ca_reference").Name = "Changed";
        result.GetAttributeValue<byte[]>("ca_bytes")[0] = 99;
        ((Money)((AliasedValue)result["r.ca_money"]).Value).Value = 99m;
        if (allColumns)
        {
            ((Money)result.KeyAttributes["ca_key"]).Value = 99m;
            var returnedChild = result.RelatedEntities[new Relationship("ca_children")].Entities[0];
            returnedChild.GetAttributeValue<Money>("ca_money").Value = 99m;
            returnedChild.GetAttributeValue<Entity>("ca_parent").Should().BeSameAs(result);
        }

        row.GetAttributeValue<Money>("ca_money").Value.Should().Be(12m);
        row.GetAttributeValue<OptionSetValue>("ca_option").Value.Should().Be(1);
        row.GetAttributeValue<OptionSetValueCollection>("ca_options")[0].Value.Should().Be(2);
        row.GetAttributeValue<EntityReference>("ca_reference").Name.Should().Be("Original");
        row.GetAttributeValue<byte[]>("ca_bytes")[0].Should().Be(1);
        ((Money)((AliasedValue)row["r.ca_money"]).Value).Value.Should().Be(24m);
        ((Money)row.KeyAttributes["ca_key"]).Value.Should().Be(5m);
        child.GetAttributeValue<Money>("ca_money").Value.Should().Be(6m);
    }

    private static IOrganizationService CreateService(bool withMetadata = false)
    {
        IOrganizationService service = null!;
        return service.Simulate(withMetadata
            ? new SimulatorOptions
            {
                Metadata = SimulatedMetadata.Load(Path.Combine(TestContext.CurrentContext.TestDirectory,
                    "TestMetadata", "query-semantics-metadata.json"))
            }
            : null);
    }

    private static Entity Row(string name, string hidden = "keep") => new("ca_row")
    {
        Id = Guid.NewGuid(), ["ca_name"] = name, ["ca_hidden"] = hidden
    };
}
