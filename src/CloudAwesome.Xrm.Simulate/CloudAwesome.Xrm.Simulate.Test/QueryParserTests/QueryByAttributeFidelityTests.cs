using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture]
public class QueryByAttributeFidelityTests
{
    private const string EntityName = "ca_querybyattribute";
    private const string NameAttribute = "ca_name";
    private const string CategoryAttribute = "ca_category";
    private const string StatusAttribute = "ca_status";
    private const string LookupAttribute = "ca_lookup";
    private const string MoneyAttribute = "ca_money";
    private const string DateAttribute = "ca_date";

    private IOrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = _organizationService.Simulate();
    }

    [Test(Description = "QueryByAttribute should filter on an attribute even when that attribute is not included in the returned ColumnSet.")]
    public void QueryByAttribute_Should_Filter_On_Attribute_Not_Returned_In_ColumnSet()
    {
        AddRecord("Visible name", category: "matched");

        var query = new QueryByAttribute(EntityName)
        {
            ColumnSet = new ColumnSet(NameAttribute),
            Attributes = { CategoryAttribute },
            Values = { "matched" }
        };

        var result = _organizationService.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result[NameAttribute].Should().Be("Visible name");
        result.Contains(CategoryAttribute).Should().BeFalse();
    }

    [Test(Description = "QueryByAttribute should apply every attribute/value pair as an AND condition.")]
    public void QueryByAttribute_Should_Apply_Attribute_Value_Pairs_As_And_Conditions()
    {
        AddRecord("Expected row", category: "matched", status: new OptionSetValue(1));
        AddRecord("Wrong category", category: "other", status: new OptionSetValue(1));
        AddRecord("Wrong status", category: "matched", status: new OptionSetValue(2));

        var query = new QueryByAttribute(EntityName)
        {
            ColumnSet = new ColumnSet(NameAttribute),
            Attributes = { CategoryAttribute, StatusAttribute },
            Values = { "matched", 1 }
        };

        var result = _organizationService.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result[NameAttribute].Should().Be("Expected row");
    }

    [Test(Description = "QueryByAttribute should preserve selected SDK value types on returned base attributes.")]
    public void QueryByAttribute_Should_Preserve_Selected_Sdk_Attribute_Types()
    {
        var lookup = new EntityReference("account", Guid.NewGuid());
        var date = new DateTime(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
        AddRecord("Expected row", category: "matched", status: new OptionSetValue(3),
            lookup: lookup, money: new Money(456.78m), date: date);

        var query = new QueryByAttribute(EntityName)
        {
            ColumnSet = new ColumnSet(StatusAttribute, LookupAttribute, MoneyAttribute, DateAttribute),
            Attributes = { CategoryAttribute },
            Values = { "matched" }
        };

        var result = _organizationService.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result[StatusAttribute].Should().BeOfType<OptionSetValue>().Which.Value.Should().Be(3);
        result[LookupAttribute].Should().BeOfType<EntityReference>().Which.Id.Should().Be(lookup.Id);
        result[MoneyAttribute].Should().BeOfType<Money>().Which.Value.Should().Be(456.78m);
        result[DateAttribute].Should().BeOfType<DateTime>().Subject.Should().Be(date);
    }

    [Test(Description = "QueryByAttribute should support lookup equality using the referenced row ID, matching common Dataverse lookup query usage.")]
    public void QueryByAttribute_Should_Filter_EntityReference_By_Guid()
    {
        var accountId = Guid.NewGuid();
        AddRecord("Expected row", lookup: new EntityReference("account", accountId));
        AddRecord("Wrong row", lookup: new EntityReference("account", Guid.NewGuid()));

        var query = new QueryByAttribute(EntityName)
        {
            ColumnSet = new ColumnSet(NameAttribute),
            Attributes = { LookupAttribute },
            Values = { accountId }
        };

        var result = _organizationService.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result[NameAttribute].Should().Be("Expected row");
    }

    [Test(Description = "QueryByAttribute should support option set equality using the integer option value.")]
    public void QueryByAttribute_Should_Filter_OptionSetValue_By_Integer()
    {
        AddRecord("Expected row", status: new OptionSetValue(10));
        AddRecord("Wrong row", status: new OptionSetValue(20));

        var query = new QueryByAttribute(EntityName)
        {
            ColumnSet = new ColumnSet(NameAttribute),
            Attributes = { StatusAttribute },
            Values = { 10 }
        };

        var result = _organizationService.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result[NameAttribute].Should().Be("Expected row");
    }

    [Test(Description = "QueryByAttribute should return an empty EntityCollection when the requested entity has no simulated rows.")]
    public void QueryByAttribute_Should_Return_Empty_Collection_When_Entity_Has_No_Data()
    {
        var query = new QueryByAttribute(EntityName)
        {
            ColumnSet = new ColumnSet(NameAttribute),
            Attributes = { CategoryAttribute },
            Values = { "matched" }
        };

        var results = _organizationService.RetrieveMultiple(query);

        results.Entities.Should().BeEmpty();
    }

    [Test(Description = "QueryByAttribute should apply ordering before TopCount so the returned window is deterministic.")]
    public void QueryByAttribute_Should_Apply_Order_Before_TopCount()
    {
        AddRecord("Charlie");
        AddRecord("Alpha");
        AddRecord("Bravo");

        var query = new QueryByAttribute(EntityName)
        {
            ColumnSet = new ColumnSet(NameAttribute),
            TopCount = 2,
            Orders = { new OrderExpression(NameAttribute, OrderType.Ascending) }
        };

        var results = _organizationService.RetrieveMultiple(query).Entities;

        results.Select(entity => entity.GetAttributeValue<string>(NameAttribute))
            .Should().ContainInOrder("Alpha", "Bravo");
    }

    private void AddRecord(string name, string? category = null, OptionSetValue? status = null,
        EntityReference? lookup = null, Money? money = null, DateTime? date = null)
    {
        var entity = new Entity(EntityName) { Id = Guid.NewGuid() };
        entity[NameAttribute] = name;

        if (category is not null)
        {
            entity[CategoryAttribute] = category;
        }

        if (status is not null)
        {
            entity[StatusAttribute] = status;
        }

        if (lookup is not null)
        {
            entity[LookupAttribute] = lookup;
        }

        if (money is not null)
        {
            entity[MoneyAttribute] = money;
        }

        if (date.HasValue)
        {
            entity[DateAttribute] = date.Value;
        }

        _organizationService.Simulated().Data().Add(entity);
    }
}
