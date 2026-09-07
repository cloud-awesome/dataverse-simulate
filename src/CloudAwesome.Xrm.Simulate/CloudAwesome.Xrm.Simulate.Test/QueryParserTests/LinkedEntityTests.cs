using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture]
public class LinkedEntityTests
{
    private const string SourceEntityName = "ca_source";
    private const string SourceIdAttribute = "ca_sourceid";
    private const string SourceNameAttribute = "ca_name";
    private const string SourceLookupAttribute = "ca_linkedid";
    private const string SourceSecondLookupAttribute = "ca_secondlinkedid";
    private const string LinkedEntityName = "ca_linked";
    private const string LinkedIdAttribute = "ca_linkedid";
    private const string LinkedParentAttribute = "ca_parentid";
    private const string LinkedNameAttribute = "ca_name";
    private const string LinkedHiddenAttribute = "ca_hidden";
    private const string LinkedStatusAttribute = "ca_status";
    private const string SecondLinkedEntityName = "ca_secondlinked";
    private const string SecondLinkedIdAttribute = "ca_secondlinkedid";
    private const string SecondLinkedNameAttribute = "ca_name";
    private const string Alias = "linked";
    private const string SecondAlias = "second";

    private IOrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = _organizationService.Simulate();
    }

    [Test(Description = "Linked QueryExpression columns should be returned under alias.attribute and wrapped in AliasedValue so consumer code can safely type-check them.")]
    public void LinkedEntity_Should_Return_AliasedValue()
    {
        var businessUnitId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _organizationService.Simulated().Data().Add(
            new Team
            {
                Id = teamId,
                Name = "Delivery Team",
                BusinessUnitId = new EntityReference("businessunit", businessUnitId)
            });
        _organizationService.Simulated().Data().Add(
            new Role
            {
                Id = roleId,
                Name = "Basic User",
                BusinessUnitId = new EntityReference("businessunit", businessUnitId)
            });
        _organizationService.Simulated().Data().Add(
            new TeamRoles
            {
                Id = Guid.NewGuid(),
                TeamId = teamId,
                RoleId = roleId
            });

        var teamRolesQuery = new QueryExpression
        {
            EntityName = TeamRoles.EntityLogicalName,
            ColumnSet = new ColumnSet(true),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(TeamRoles.Fields.TeamId, ConditionOperator.Equal, teamId)
                }
            },
            LinkEntities =
            {
                new LinkEntity
                {
                    LinkFromEntityName = TeamRoles.EntityLogicalName,
                    LinkToEntityName = Role.EntityLogicalName,
                    LinkFromAttributeName = TeamRoles.Fields.RoleId,
                    LinkToAttributeName = Role.Fields.RoleId,
                    EntityAlias = "roleAlias",
                    Columns = new ColumnSet(Role.Fields.Name)
                }
            }
        };

        var teamRoles = _organizationService.RetrieveMultiple(teamRolesQuery).Entities;

        var roleNameAddress = $"roleAlias.{Role.Fields.Name}";
        foreach (var teamRole in teamRoles)
        {
            teamRole[roleNameAddress].Should().BeOfType<AliasedValue>()
                .Which.Value.Should().Be("Basic User");
        }
    }

    [Test(Description = "AliasedValue should include the linked entity logical name, linked attribute logical name, and the raw Dataverse value.")]
    public void LinkedEntity_Should_Preserve_AliasedValue_Metadata()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value");

        var result = RetrieveSingleLinkedResult(new ColumnSet(LinkedNameAttribute));
        var aliasedValue = result[$"{Alias}.{LinkedNameAttribute}"].Should().BeOfType<AliasedValue>().Subject;

        aliasedValue.EntityLogicalName.Should().Be(LinkedEntityName);
        aliasedValue.AttributeLogicalName.Should().Be(LinkedNameAttribute);
        aliasedValue.Value.Should().Be("Visible value");
    }

    [Test(Description = "AliasedValue.Value should preserve common SDK value shapes instead of coercing everything to strings.")]
    public void LinkedEntity_Should_Preserve_Common_Sdk_Value_Types_In_AliasedValue()
    {
        var linkedId = Guid.NewGuid();
        var entityReference = new EntityReference("account", Guid.NewGuid());
        var optionSetValue = new OptionSetValue(42);
        var money = new Money(123.45m);
        var dateTime = new DateTime(2026, 8, 28, 9, 30, 0, DateTimeKind.Utc);
        var rawGuid = Guid.NewGuid();

        AddSource(linkedId);
        var linked = AddLinked(linkedId, "Visible value");
        linked["ca_guid"] = rawGuid;
        linked["ca_lookup"] = entityReference;
        linked["ca_option"] = optionSetValue;
        linked["ca_money"] = money;
        linked["ca_boolean"] = true;
        linked["ca_datetime"] = dateTime;

        var result = RetrieveSingleLinkedResult(new ColumnSet(
            LinkedNameAttribute,
            "ca_guid",
            "ca_lookup",
            "ca_option",
            "ca_money",
            "ca_boolean",
            "ca_datetime"));

        AssertAliasedValue<string>(result, LinkedNameAttribute).Should().Be("Visible value");
        AssertAliasedValue<Guid>(result, "ca_guid").Should().Be(rawGuid);
        var aliasedReference = AssertAliasedValue<EntityReference>(result, "ca_lookup");
        aliasedReference.LogicalName.Should().Be(entityReference.LogicalName);
        aliasedReference.Id.Should().Be(entityReference.Id);
        AssertAliasedValue<OptionSetValue>(result, "ca_option").Value.Should().Be(42);
        AssertAliasedValue<Money>(result, "ca_money").Value.Should().Be(123.45m);
        AssertAliasedValue<bool>(result, "ca_boolean").Should().BeTrue();
        AssertAliasedValue<DateTime>(result, "ca_datetime").Should().Be(dateTime);
    }

    [Test(Description = "Explicit linked ColumnSet selections should return only those linked columns as alias.attribute values.")]
    public void LinkedEntity_Should_Only_Return_Selected_Linked_Columns()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value", hiddenValue: "Should not be returned");

        var result = RetrieveSingleLinkedResult(new ColumnSet(LinkedNameAttribute));

        result.Contains($"{Alias}.{LinkedNameAttribute}").Should().BeTrue();
        result.Contains($"{Alias}.{LinkedHiddenAttribute}").Should().BeFalse();
    }

    [Test(Description = "A link with ColumnSet(false) should still constrain matching rows but should not project linked attributes into the result.")]
    public void LinkedEntity_With_ColumnSetFalse_Should_Join_Without_Returning_Linked_Columns()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value", hiddenValue: "Should not be returned");

        var query = BuildLinkedQuery(new ColumnSet(SourceNameAttribute), new ColumnSet(false));
        var results = _organizationService.RetrieveMultiple(query).Entities;

        results.Should().ContainSingle();
        results[0].Attributes.Keys.Should().NotContain(key => key.StartsWith($"{Alias}.", StringComparison.Ordinal));
    }

    [Test(Description = "A link with ColumnSet(true) should project every populated linked attribute as an AliasedValue.")]
    public void LinkedEntity_With_AllColumns_Should_Return_All_Populated_Linked_Columns_As_AliasedValues()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value", hiddenValue: "Also visible when all columns are requested");

        var result = RetrieveSingleLinkedResult(new ColumnSet(true));

        AssertAliasedValue<string>(result, LinkedNameAttribute).Should().Be("Visible value");
        AssertAliasedValue<string>(result, LinkedHiddenAttribute).Should().Be("Also visible when all columns are requested");
        AssertAliasedValue<Guid>(result, LinkedIdAttribute).Should().Be(linkedId);
    }

    [Test(Description = "Dataverse can join on a base attribute even when that attribute is not included in the returned base ColumnSet.")]
    public void LinkedEntity_Should_Not_Require_Join_Column_In_Base_ColumnSet()
    {
        var linkedId = Guid.NewGuid();
        var source = AddSource(linkedId);
        AddLinked(linkedId, "Visible value");

        var query = BuildLinkedQuery(new ColumnSet(SourceNameAttribute), new ColumnSet(LinkedNameAttribute));
        var result = _organizationService.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result.Contains(SourceLookupAttribute).Should().BeFalse();
        result[SourceIdAttribute].Should().Be(source.Id);
        result[SourceNameAttribute].Should().Be("Source row");
        AssertAliasedValue<string>(result, LinkedNameAttribute).Should().Be("Visible value");
    }

    [Test(Description = "Dataverse can filter a linked entity on a column that is not projected by the linked ColumnSet.")]
    public void LinkedEntity_Should_Not_Require_Linked_Filter_Column_In_Linked_ColumnSet()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value", status: "active");

        var query = BuildLinkedQuery(new ColumnSet(SourceNameAttribute), new ColumnSet(LinkedNameAttribute));
        query.LinkEntities[0].LinkCriteria.AddCondition(LinkedStatusAttribute, ConditionOperator.Equal, "active");

        var result = _organizationService.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        AssertAliasedValue<string>(result, LinkedNameAttribute).Should().Be("Visible value");
        result.Contains($"{Alias}.{LinkedStatusAttribute}").Should().BeFalse();
    }

    [Test(Description = "Query-time linked aliases should not be written back into the simulated data store.")]
    public void LinkedEntity_Should_Not_Mutate_Stored_Base_Entity()
    {
        var sourceId = Guid.NewGuid();
        var linkedId = Guid.NewGuid();
        AddSource(linkedId, sourceId);
        AddLinked(linkedId, "Visible value");

        _organizationService.RetrieveMultiple(BuildLinkedQuery(new ColumnSet(true), new ColumnSet(LinkedNameAttribute)));

        var storedSource = _organizationService.Simulated().Data().Get(SourceEntityName, sourceId);
        storedSource.Contains($"{Alias}.{LinkedNameAttribute}").Should().BeFalse();
    }

    [Test(Description = "Linked query results should be cloned from the stored base row so callers cannot mutate the in-memory data store through a returned row.")]
    public void LinkedEntity_Should_Return_Cloned_Base_Entity_With_Attributes_And_FormattedValues()
    {
        var sourceId = Guid.NewGuid();
        var linkedId = Guid.NewGuid();
        var source = AddSource(linkedId, sourceId);
        source.FormattedValues[SourceNameAttribute] = "Source row formatted";
        AddLinked(linkedId, "Visible value");

        var result = _organizationService.RetrieveMultiple(
                BuildLinkedQuery(new ColumnSet(true), new ColumnSet(LinkedNameAttribute))).Entities
            .Should()
            .ContainSingle()
            .Subject;

        result.Should().NotBeSameAs(source);
        result.LogicalName.Should().Be(SourceEntityName);
        result.Id.Should().Be(sourceId);
        result[SourceNameAttribute].Should().Be("Source row");
        result.FormattedValues[SourceNameAttribute].Should().Be("Source row formatted");

        result[SourceNameAttribute] = "Changed by consumer";
        source[SourceNameAttribute].Should().Be("Source row");
    }

    [Test(Description = "An inner link should return one base result for each matching linked row, preserving each linked row's aliased value.")]
    public void Inner_LinkEntity_Should_Return_One_Result_Per_Matching_Linked_Record()
    {
        var parentId = Guid.NewGuid();
        var source = AddSource(Guid.NewGuid());
        source[SourceLookupAttribute] = parentId;
        AddLinked(Guid.NewGuid(), "First linked row", parentId: parentId);
        AddLinked(Guid.NewGuid(), "Second linked row", parentId: parentId);

        var query = BuildLinkedQuery(
            new ColumnSet(SourceNameAttribute),
            new ColumnSet(LinkedNameAttribute),
            SourceLookupAttribute,
            LinkedParentAttribute);

        var results = _organizationService.RetrieveMultiple(query).Entities;

        results.Should().HaveCount(2);
        results.Select(entity => AssertAliasedValue<string>(entity, LinkedNameAttribute))
            .Should().BeEquivalentTo("First linked row", "Second linked row");
    }

    [Test(Description = "A left outer link should keep the base row when no linked row matches and omit aliased linked attributes.")]
    public void LeftOuter_LinkEntity_Should_Return_Base_Row_When_No_Linked_Record_Matches()
    {
        AddSource(Guid.NewGuid());
        AddLinked(Guid.NewGuid(), "Non-matching linked row");

        var query = BuildLinkedQuery(new ColumnSet(SourceNameAttribute), new ColumnSet(LinkedNameAttribute));
        query.LinkEntities[0].JoinOperator = JoinOperator.LeftOuter;

        var result = _organizationService.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        result[SourceNameAttribute].Should().Be("Source row");
        result.Contains($"{Alias}.{LinkedNameAttribute}").Should().BeFalse();
    }

    [Test(Description = "An inner link should exclude the base row when no linked row matches.")]
    public void Inner_LinkEntity_Should_Exclude_Base_Row_When_No_Linked_Record_Matches()
    {
        AddSource(Guid.NewGuid());
        AddLinked(Guid.NewGuid(), "Non-matching linked row");

        var query = BuildLinkedQuery(new ColumnSet(SourceNameAttribute), new ColumnSet(LinkedNameAttribute));
        query.LinkEntities[0].JoinOperator = JoinOperator.Inner;

        _organizationService.RetrieveMultiple(query).Entities.Should().BeEmpty();
    }

    [Test(Description = "Unsupported QueryExpression join operators should fail clearly instead of falling back to inner join behavior.")]
    public void Unsupported_LinkEntity_JoinOperator_Should_Throw_NotSupportedException()
    {
        var linkedId = Guid.NewGuid();
        AddSource(linkedId);
        AddLinked(linkedId, "Visible value");

        var query = BuildLinkedQuery(new ColumnSet(SourceNameAttribute), new ColumnSet(LinkedNameAttribute));
        query.LinkEntities[0].JoinOperator = JoinOperator.Exists;

        var retrieve = () => _organizationService.RetrieveMultiple(query);

        retrieve.Should().Throw<NotSupportedException>()
            .WithMessage("*Exists*");
    }

    [Test(Description = "Multiple links from one base row should preserve independent aliases without overwriting each other.")]
    public void Multiple_LinkEntities_Should_Preserve_Separate_Aliased_Values()
    {
        var firstLinkedId = Guid.NewGuid();
        var secondLinkedId = Guid.NewGuid();
        var source = AddSource(firstLinkedId);
        source[SourceSecondLookupAttribute] = secondLinkedId;
        AddLinked(firstLinkedId, "First linked value");
        AddSecondLinked(secondLinkedId, "Second linked value");

        var query = BuildLinkedQuery(new ColumnSet(SourceNameAttribute), new ColumnSet(LinkedNameAttribute));
        query.LinkEntities.Add(new LinkEntity
        {
            LinkFromEntityName = SourceEntityName,
            LinkToEntityName = SecondLinkedEntityName,
            LinkFromAttributeName = SourceSecondLookupAttribute,
            LinkToAttributeName = SecondLinkedIdAttribute,
            EntityAlias = SecondAlias,
            Columns = new ColumnSet(SecondLinkedNameAttribute)
        });

        var result = _organizationService.RetrieveMultiple(query).Entities.Should().ContainSingle().Subject;

        AssertAliasedValue<string>(result, LinkedNameAttribute).Should().Be("First linked value");
        AssertAliasedValue<string>(result, SecondLinkedNameAttribute, SecondAlias).Should().Be("Second linked value");
    }

    [Test(Description = "Orders declared on a LinkEntity should sort by the linked column value.")]
    public void LinkedEntity_Order_Should_Sort_By_Linked_Column_Value()
    {
        var zuluLinkedId = Guid.NewGuid();
        var alphaLinkedId = Guid.NewGuid();
        AddSource(zuluLinkedId, name: "Zulu source");
        AddSource(alphaLinkedId, name: "Alpha source");
        AddLinked(zuluLinkedId, "Zulu");
        AddLinked(alphaLinkedId, "Alpha");

        var query = BuildLinkedQuery(new ColumnSet(true), new ColumnSet(LinkedNameAttribute));
        query.LinkEntities[0].Orders.Add(new OrderExpression(LinkedNameAttribute, OrderType.Ascending));

        var results = _organizationService.RetrieveMultiple(query).Entities;

        results.Select(entity => entity.GetAttributeValue<string>(SourceNameAttribute))
            .Should().ContainInOrder("Alpha source", "Zulu source");
    }

    private Entity RetrieveSingleLinkedResult(ColumnSet linkedColumns)
    {
        return _organizationService.RetrieveMultiple(BuildLinkedQuery(new ColumnSet(true), linkedColumns))
            .Entities
            .Should()
            .ContainSingle()
            .Subject;
    }

    private QueryExpression BuildLinkedQuery(ColumnSet baseColumns, ColumnSet linkedColumns,
        string linkFromAttributeName = SourceLookupAttribute,
        string linkToAttributeName = LinkedIdAttribute)
    {
        return new QueryExpression
        {
            EntityName = SourceEntityName,
            ColumnSet = baseColumns,
            LinkEntities =
            {
                new LinkEntity
                {
                    LinkFromEntityName = SourceEntityName,
                    LinkToEntityName = LinkedEntityName,
                    LinkFromAttributeName = linkFromAttributeName,
                    LinkToAttributeName = linkToAttributeName,
                    EntityAlias = Alias,
                    Columns = linkedColumns
                }
            }
        };
    }

    private Entity AddSource(Guid linkedId, Guid? id = null, string name = "Source row")
    {
        var sourceId = id ?? Guid.NewGuid();
        var source = new Entity(SourceEntityName) { Id = sourceId };
        source[SourceIdAttribute] = sourceId;
        source[SourceNameAttribute] = name;
        source[SourceLookupAttribute] = linkedId;

        _organizationService.Simulated().Data().Add(source);

        return source;
    }

    private Entity AddLinked(Guid linkedId, string name, string? hiddenValue = null,
        string? status = null, Guid? parentId = null)
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

        if (parentId.HasValue)
        {
            linked[LinkedParentAttribute] = parentId.Value;
        }

        _organizationService.Simulated().Data().Add(linked);

        return linked;
    }

    private void AddSecondLinked(Guid linkedId, string name)
    {
        var linked = new Entity(SecondLinkedEntityName) { Id = linkedId };
        linked[SecondLinkedIdAttribute] = linkedId;
        linked[SecondLinkedNameAttribute] = name;

        _organizationService.Simulated().Data().Add(linked);
    }

    private static T AssertAliasedValue<T>(Entity entity, string attributeName, string alias = Alias)
    {
        var aliasedValue = entity[$"{alias}.{attributeName}"].Should().BeOfType<AliasedValue>().Subject;
        aliasedValue.EntityLogicalName.Should().NotBeNullOrWhiteSpace();
        aliasedValue.AttributeLogicalName.Should().Be(attributeName);

        return aliasedValue.Value.Should().BeOfType<T>().Subject;
    }
}
