using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using System.Reflection;

namespace CloudAwesome.Xrm.Simulate.Metadata;

internal static class SdkMetadataProjection
{
    internal static EntityMetadata ToSdkEntityMetadata(
        SimulatedEntityMetadata entity,
        EntityFilters filters = EntityFilters.All)
    {
        var metadata = new EntityMetadata
        {
            LogicalName = entity.LogicalName,
            SchemaName = entity.SchemaName,
            IsActivity = entity.IsActivity,
            OwnershipType = ParseOwnershipType(entity.OwnershipType)
        };

        if (filters.HasFlag(EntityFilters.Attributes) || filters.HasFlag(EntityFilters.All))
        {
            SetSdkProperty(
                metadata,
                nameof(EntityMetadata.Attributes),
                entity.Attributes.Select(ToSdkAttributeMetadata).ToArray());
        }

        if (filters.HasFlag(EntityFilters.Relationships) || filters.HasFlag(EntityFilters.All))
        {
            SetSdkProperty(
                metadata,
                nameof(EntityMetadata.ManyToOneRelationships),
                entity.Relationships
                .Where(IsManyToOne)
                .Select(ToOneToManyRelationshipMetadata)
                .ToArray());

            SetSdkProperty(
                metadata,
                nameof(EntityMetadata.OneToManyRelationships),
                entity.Relationships
                .Where(IsOneToMany)
                .Select(ToOneToManyRelationshipMetadata)
                .ToArray());

            SetSdkProperty(
                metadata,
                nameof(EntityMetadata.ManyToManyRelationships),
                entity.Relationships
                .Where(IsManyToMany)
                .Select(ToManyToManyRelationshipMetadata)
                .ToArray());
        }

        return metadata;
    }

    internal static AttributeMetadata ToSdkAttributeMetadata(SimulatedAttributeMetadata attribute)
    {
        var metadata = CreateAttributeMetadata(attribute);

        metadata.LogicalName = attribute.LogicalName;
        metadata.SchemaName = attribute.SchemaName;
        metadata.RequiredLevel = new AttributeRequiredLevelManagedProperty(ParseRequiredLevel(attribute.RequiredLevel));
        SetSdkProperty(metadata, nameof(AttributeMetadata.IsPrimaryId), attribute.IsPrimaryId);
        SetSdkProperty(metadata, nameof(AttributeMetadata.IsPrimaryName), attribute.IsPrimaryName);
        SetSdkProperty(metadata, nameof(AttributeMetadata.IsValidForCreate), attribute.IsValidForCreate);
        SetSdkProperty(metadata, nameof(AttributeMetadata.IsValidForUpdate), attribute.IsValidForUpdate);
        SetSdkProperty(metadata, nameof(AttributeMetadata.IsValidForRead), attribute.IsValidForRead);
        metadata.IsSecured = attribute.IsSecured;

        return metadata;
    }

    internal static RelationshipMetadataBase ToSdkRelationshipMetadata(SimulatedRelationshipMetadata relationship)
    {
        return IsManyToMany(relationship)
            ? ToManyToManyRelationshipMetadata(relationship)
            : ToOneToManyRelationshipMetadata(relationship);
    }

    private static AttributeMetadata CreateAttributeMetadata(SimulatedAttributeMetadata attribute)
    {
        return attribute.AttributeType switch
        {
            "String" => new StringAttributeMetadata
            {
                MaxLength = attribute.MaxLength,
                FormatName = ParseStringFormat(attribute.Format)
            },
            "Memo" => new MemoAttributeMetadata
            {
                MaxLength = attribute.MaxLength
            },
            "Integer" => new IntegerAttributeMetadata
            {
                MinValue = ToInt(attribute.MinValue),
                MaxValue = ToInt(attribute.MaxValue)
            },
            "BigInt" => new BigIntAttributeMetadata(),
            "Decimal" => new DecimalAttributeMetadata
            {
                MinValue = attribute.MinValue,
                MaxValue = attribute.MaxValue,
                Precision = attribute.Precision
            },
            "Double" => new DoubleAttributeMetadata
            {
                MinValue = ToDouble(attribute.MinValue),
                MaxValue = ToDouble(attribute.MaxValue),
                Precision = attribute.Precision
            },
            "Money" => new MoneyAttributeMetadata
            {
                MinValue = ToDouble(attribute.MinValue),
                MaxValue = ToDouble(attribute.MaxValue),
                Precision = attribute.Precision
            },
            "Lookup" or "Customer" or "Owner" => new LookupAttributeMetadata
            {
                Targets = attribute.LookupTargets.ToArray()
            },
            "Picklist" => new PicklistAttributeMetadata
            {
                OptionSet = ToOptionSet(attribute.Options)
            },
            "MultiSelectPicklist" => new MultiSelectPicklistAttributeMetadata
            {
                OptionSet = ToOptionSet(attribute.Options)
            },
            "State" => new StateAttributeMetadata
            {
                OptionSet = ToStateOptionSet(attribute.Options)
            },
            "Status" => new StatusAttributeMetadata
            {
                OptionSet = ToStatusOptionSet(attribute.Options)
            },
            "Boolean" => new BooleanAttributeMetadata(),
            "DateTime" => new DateTimeAttributeMetadata(),
            "Uniqueidentifier" => new UniqueIdentifierAttributeMetadata(),
            _ => new AttributeMetadata()
        };
    }

    private static OptionSetMetadata ToOptionSet(IReadOnlyList<SimulatedOptionMetadata> options)
    {
        return new OptionSetMetadata(new OptionMetadataCollection(options.Select(ToOptionMetadata).ToList()));
    }

    private static OptionSetMetadata ToStateOptionSet(IReadOnlyList<SimulatedOptionMetadata> options)
    {
        return new OptionSetMetadata(new OptionMetadataCollection(options.Select(option =>
        {
            var metadata = new StateOptionMetadata
            {
                Value = option.Value,
                Label = ToLabel(option.Label),
                DefaultStatus = option.State
            };

            return metadata;
        }).Cast<OptionMetadata>().ToList()));
    }

    private static OptionSetMetadata ToStatusOptionSet(IReadOnlyList<SimulatedOptionMetadata> options)
    {
        return new OptionSetMetadata(new OptionMetadataCollection(options.Select(option =>
        {
            var metadata = new StatusOptionMetadata(option.Value, option.State)
            {
                Label = ToLabel(option.Label)
            };

            return metadata;
        }).Cast<OptionMetadata>().ToList()));
    }

    private static OptionMetadata ToOptionMetadata(SimulatedOptionMetadata option)
    {
        return new OptionMetadata(ToLabel(option.Label), option.Value);
    }

    private static Label ToLabel(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? new Label()
            : new Label(text, 1033);
    }

    private static OneToManyRelationshipMetadata ToOneToManyRelationshipMetadata(SimulatedRelationshipMetadata relationship)
    {
        return new OneToManyRelationshipMetadata
        {
            SchemaName = relationship.SchemaName,
            ReferencingEntity = relationship.ReferencingEntity,
            ReferencingAttribute = relationship.ReferencingAttribute,
            ReferencedEntity = relationship.ReferencedEntity,
            ReferencedAttribute = relationship.ReferencedAttribute
        };
    }

    private static ManyToManyRelationshipMetadata ToManyToManyRelationshipMetadata(SimulatedRelationshipMetadata relationship)
    {
        return new ManyToManyRelationshipMetadata
        {
            SchemaName = relationship.SchemaName,
            Entity1LogicalName = relationship.ReferencingEntity,
            Entity1IntersectAttribute = relationship.ReferencingAttribute,
            Entity2LogicalName = relationship.ReferencedEntity,
            Entity2IntersectAttribute = relationship.ReferencedAttribute,
            IntersectEntityName = relationship.IntersectEntity
        };
    }

    private static bool IsManyToOne(SimulatedRelationshipMetadata relationship)
    {
        return string.Equals(relationship.RelationshipType, "many-to-one", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOneToMany(SimulatedRelationshipMetadata relationship)
    {
        return string.Equals(relationship.RelationshipType, "one-to-many", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManyToMany(SimulatedRelationshipMetadata relationship)
    {
        return string.Equals(relationship.RelationshipType, "many-to-many", StringComparison.OrdinalIgnoreCase);
    }

    private static OwnershipTypes? ParseOwnershipType(string ownershipType)
    {
        return Enum.TryParse<OwnershipTypes>(ownershipType, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static AttributeRequiredLevel ParseRequiredLevel(string requiredLevel)
    {
        return Enum.TryParse<AttributeRequiredLevel>(requiredLevel, ignoreCase: true, out var parsed)
            ? parsed
            : AttributeRequiredLevel.None;
    }

    private static StringFormatName? ParseStringFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return null;
        }

        return format.ToLowerInvariant() switch
        {
            "email" => StringFormatName.Email,
            "phone" => StringFormatName.Phone,
            "text" => StringFormatName.Text,
            "textarea" => StringFormatName.TextArea,
            "url" => StringFormatName.Url,
            _ => null
        };
    }

    private static void SetSdkProperty<TTarget>(TTarget target, string propertyName, object? value)
    {
        var property = typeof(TTarget).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property?.SetMethod is not null)
        {
            property.SetValue(target, value);
            return;
        }

        var backingField = typeof(TTarget).GetField(
            $"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        backingField?.SetValue(target, value);
    }

    private static int? ToInt(decimal? value)
    {
        return value is null ? null : Convert.ToInt32(value.Value);
    }

    private static double? ToDouble(decimal? value)
    {
        return value is null ? null : Convert.ToDouble(value.Value);
    }
}
