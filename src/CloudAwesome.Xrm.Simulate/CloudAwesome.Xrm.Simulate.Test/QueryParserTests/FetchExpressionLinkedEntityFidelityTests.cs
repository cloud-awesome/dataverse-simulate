using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.QueryParsers;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture]
public class FetchExpressionLinkedEntityFidelityTests
{
    private const string SourceEntityName = "ca_source";
    private const string SourceNameAttribute = "ca_name";
    private const string SourceLookupAttribute = "ca_linkedid";
    private const string LinkedEntityName = "ca_linked";
    private const string LinkedIdAttribute = "ca_linkedid";
    private const string LinkedNameAttribute = "ca_name";
    private const string LinkedHiddenAttribute = "ca_hidden";
    private const string LinkedStatusAttribute = "ca_status";
    private const string Alias = "linked";

    private IOrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = _organizationService.Simulate();
    }

    [Test(Description = "FetchXML linked attributes should be returned under alias.attribute and wrapped in AliasedValue via the shared QueryExpression path.")]
    public void FetchExpression_LinkedEntity_Should_Return_AliasedValue()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value");

        var result = _organizationService.RetrieveMultiple(new FetchExpression(LinkedFetchXml())).Entities
            .Should()
            .ContainSingle()
            .Subject;

        AssertAliasedValue<string>(result, LinkedNameAttribute).Should().Be("Visible value");
    }

    [Test(Description = "FetchXML linked AliasedValue metadata should reflect the linked entity and linked attribute, not the base entity.")]
    public void FetchExpression_LinkedEntity_Should_Preserve_AliasedValue_Metadata()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value");

        var result = _organizationService.RetrieveMultiple(new FetchExpression(LinkedFetchXml())).Entities
            .Should()
            .ContainSingle()
            .Subject;
        var aliasedValue = result[$"{Alias}.{LinkedNameAttribute}"].Should().BeOfType<AliasedValue>().Subject;

        aliasedValue.EntityLogicalName.Should().Be(LinkedEntityName);
        aliasedValue.AttributeLogicalName.Should().Be(LinkedNameAttribute);
        aliasedValue.Value.Should().Be("Visible value");
    }

    [Test(Description = "FetchXML should project only the linked attributes listed under link-entity.")]
    public void FetchExpression_LinkedEntity_Should_Only_Return_Selected_Linked_Columns()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value", hiddenValue: "Should not be returned");

        var result = _organizationService.RetrieveMultiple(new FetchExpression(LinkedFetchXml())).Entities
            .Should()
            .ContainSingle()
            .Subject;

        result.Contains($"{Alias}.{LinkedNameAttribute}").Should().BeTrue();
        result.Contains($"{Alias}.{LinkedHiddenAttribute}").Should().BeFalse();
    }

    [Test(Description = "FetchXML linked filters should be able to use attributes that are not selected for output.")]
    public void FetchExpression_LinkedEntity_Should_Filter_On_Unselected_Linked_Column()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value", status: "active");

        var result = _organizationService.RetrieveMultiple(new FetchExpression(LinkedFetchXml(linkedFilter: true))).Entities
            .Should()
            .ContainSingle()
            .Subject;

        AssertAliasedValue<string>(result, LinkedNameAttribute).Should().Be("Visible value");
        result.Contains($"{Alias}.{LinkedStatusAttribute}").Should().BeFalse();
    }

    [Test(Description = "FetchXML linked joins should not require the base join attribute to be selected for output.")]
    public void FetchExpression_LinkedEntity_Should_Not_Require_Join_Column_In_Base_Attributes()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value");

        var result = _organizationService.RetrieveMultiple(
                new FetchExpression(LinkedFetchXml(includeJoinAttribute: false))).Entities
            .Should()
            .ContainSingle()
            .Subject;

        result.Contains(SourceLookupAttribute).Should().BeFalse();
        AssertAliasedValue<string>(result, LinkedNameAttribute).Should().Be("Visible value");
    }

    [Test(Description = "FetchXML link-type='outer' should preserve the base row when there is no matching linked row.")]
    public void FetchExpression_Outer_Link_Should_Return_Base_Row_When_No_Linked_Record_Matches()
    {
        AddSource(Guid.NewGuid());
        AddLinked(Guid.NewGuid(), "Non-matching linked row");

        var result = _organizationService.RetrieveMultiple(new FetchExpression(LinkedFetchXml(linkType: "outer"))).Entities
            .Should()
            .ContainSingle()
            .Subject;

        result[SourceNameAttribute].Should().Be("Source row");
        result.Contains($"{Alias}.{LinkedNameAttribute}").Should().BeFalse();
    }

    [Test(Description = "FetchXML link-type='inner' should exclude the base row when there is no matching linked row.")]
    public void FetchExpression_Inner_Link_Should_Exclude_Base_Row_When_No_Linked_Record_Matches()
    {
        AddSource(Guid.NewGuid());
        AddLinked(Guid.NewGuid(), "Non-matching linked row");

        _organizationService.RetrieveMultiple(new FetchExpression(LinkedFetchXml(linkType: "inner"))).Entities
            .Should()
            .BeEmpty();
    }

    [Test(Description = "FetchXML conversion should map link-type='outer' to QueryExpression JoinOperator.LeftOuter.")]
    public void FetchExpression_Conversion_Should_Map_Outer_Link_To_LeftOuter_JoinOperator()
    {
        var queryExpression = FetchExpressionParser.ConvertFetchXmlToQueryExpression(LinkedFetchXml(linkType: "outer"));

        queryExpression.LinkEntities.Should().ContainSingle()
            .Subject.JoinOperator.Should().Be(JoinOperator.LeftOuter);
    }

    [Test(Description = "FetchXML conversion should map link-type='inner' to QueryExpression JoinOperator.Inner.")]
    public void FetchExpression_Conversion_Should_Map_Inner_Link_To_Inner_JoinOperator()
    {
        var queryExpression = FetchExpressionParser.ConvertFetchXmlToQueryExpression(LinkedFetchXml(linkType: "inner"));

        queryExpression.LinkEntities.Should().ContainSingle()
            .Subject.JoinOperator.Should().Be(JoinOperator.Inner);
    }

    private static string LinkedFetchXml(string linkType = "inner", bool linkedFilter = false,
        bool includeJoinAttribute = true)
    {
        var filter = linkedFilter
            ? $"""
              <filter type='and'>
                <condition attribute='{LinkedStatusAttribute}' operator='eq' value='active' />
              </filter>
              """
            : string.Empty;
        var sourceJoinAttribute = includeJoinAttribute
            ? $"<attribute name='{SourceLookupAttribute}' />"
            : string.Empty;

        return $"""
                <fetch version='1.0' mapping='logical'>
                  <entity name='{SourceEntityName}'>
                    <attribute name='{SourceNameAttribute}' />
                    {sourceJoinAttribute}
                    <link-entity name='{LinkedEntityName}' from='{LinkedIdAttribute}' to='{SourceLookupAttribute}' link-type='{linkType}' alias='{Alias}'>
                      <attribute name='{LinkedNameAttribute}' />
                      {filter}
                    </link-entity>
                  </entity>
                </fetch>
                """;
    }

    private void AddSource(Guid linkedId)
    {
        var source = new Entity(SourceEntityName) { Id = Guid.NewGuid() };
        source[SourceNameAttribute] = "Source row";
        source[SourceLookupAttribute] = linkedId;

        _organizationService.Simulated().Data().Add(source);
    }

    private void AddLinked(Guid linkedId, string name, string? hiddenValue = null, string? status = null)
    {
        var linked = new Entity(LinkedEntityName) { Id = linkedId };
        linked[LinkedIdAttribute] = linkedId;
        linked[LinkedNameAttribute] = name;

        if (hiddenValue is not null)
        {
            linked[LinkedHiddenAttribute] = hiddenValue;
        }

        if (status is not null)
        {
            linked[LinkedStatusAttribute] = status;
        }

        _organizationService.Simulated().Data().Add(linked);
    }

    private static T AssertAliasedValue<T>(Entity entity, string attributeName)
    {
        var aliasedValue = entity[$"{Alias}.{attributeName}"].Should().BeOfType<AliasedValue>().Subject;
        aliasedValue.EntityLogicalName.Should().Be(LinkedEntityName);
        aliasedValue.AttributeLogicalName.Should().Be(attributeName);

        return aliasedValue.Value.Should().BeOfType<T>().Subject;
    }
}
