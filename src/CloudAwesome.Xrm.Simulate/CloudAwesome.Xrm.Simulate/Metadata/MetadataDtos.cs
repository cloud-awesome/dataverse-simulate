using System.Text.Json.Serialization;
using System.Text.Json;

namespace CloudAwesome.Xrm.Simulate.Metadata;

internal sealed class MetadataDocumentDto
{
    public string? ContractVersion { get; set; }

    public List<MetadataEntityDto> Entities { get; set; } = [];

    public List<MetadataEntityFileDto> EntityFiles { get; set; } = [];
}

internal sealed class MetadataEntityDocumentDto
{
    public string? ContractVersion { get; set; }

    public MetadataEntityDto? Entity { get; set; }
}

internal sealed class MetadataEntityFileDto
{
    public string? LogicalName { get; set; }

    public string? Path { get; set; }
}

internal sealed class MetadataEntityDto
{
    public string? LogicalName { get; set; }

    public string? SchemaName { get; set; }

    public string? CollectionLogicalName { get; set; }

    public string? CollectionSchemaName { get; set; }

    public string? PrimaryIdAttribute { get; set; }

    public string? PrimaryNameAttribute { get; set; }

    public string? OwnershipType { get; set; }

    public bool IsActivity { get; set; }

    public bool IsIntersect { get; set; }

    public List<string> ValidMessages { get; set; } = [];

    public List<MetadataAttributeDto> Attributes { get; set; } = [];

    public List<MetadataStateStatusDto> StateStatus { get; set; } = [];

    public List<MetadataAlternateKeyDto> AlternateKeys { get; set; } = [];

    public List<MetadataRelationshipDto> Relationships { get; set; } = [];
}

internal sealed class MetadataAttributeDto
{
    public string? LogicalName { get; set; }

    public string? SchemaName { get; set; }

    public string? AttributeType { get; set; }

    public string? AttributeTypeName { get; set; }

    public string? RequiredLevel { get; set; }

    public bool IsPrimaryId { get; set; }

    public bool IsPrimaryName { get; set; }

    public bool? IsValidForCreate { get; set; }

    public bool? IsValidForUpdate { get; set; }

    public bool? IsValidForRead { get; set; }

    public bool IsSecured { get; set; }

    public List<string> LookupTargets { get; set; } = [];

    public int? MaxLength { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public int? Precision { get; set; }

    public JsonElement? DefaultValue { get; set; }

    public string? Format { get; set; }

    public List<MetadataOptionDto> Options { get; set; } = [];
}

internal sealed class MetadataOptionDto
{
    public int Value { get; set; }

    public string? Label { get; set; }

    public int? State { get; set; }
}

internal sealed class MetadataStateStatusDto
{
    public int State { get; set; }

    public string? StateLabel { get; set; }

    public int? DefaultStatus { get; set; }

    public List<MetadataStatusDto> Statuses { get; set; } = [];
}

internal sealed class MetadataStatusDto
{
    public int Value { get; set; }

    public string? Label { get; set; }
}

internal sealed class MetadataAlternateKeyDto
{
    public string? KeyName { get; set; }

    public string? Name { get; set; }

    public string? EntityLogicalName { get; set; }

    public List<string>? AttributeLogicalNames { get; set; }

    [JsonPropertyName("attributes")]
    public List<string>? Attributes { get; set; }

    public string? KeyStatus { get; set; }
}

internal sealed class MetadataRelationshipDto
{
    public string? SchemaName { get; set; }

    public string? RelationshipType { get; set; }

    public string? ReferencingEntity { get; set; }

    public string? ReferencingAttribute { get; set; }

    public string? ReferencedEntity { get; set; }

    public string? ReferencedAttribute { get; set; }

    public string? IntersectEntity { get; set; }

    public string? EntityRole { get; set; }
}
