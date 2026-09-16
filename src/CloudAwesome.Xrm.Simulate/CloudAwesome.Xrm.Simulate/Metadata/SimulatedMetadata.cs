using System.Text.Json;

namespace CloudAwesome.Xrm.Simulate.Metadata;

public sealed class SimulatedMetadata
{
    public const string SupportedContractVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly Dictionary<string, SimulatedEntityMetadata> _entities;

    private SimulatedMetadata(string contractVersion, IReadOnlyList<SimulatedEntityMetadata> entities)
    {
        ContractVersion = contractVersion;
        Entities = entities;
        _entities = entities.ToDictionary(entity => entity.LogicalName, StringComparer.OrdinalIgnoreCase);
    }

    public string ContractVersion { get; }

    public IReadOnlyList<SimulatedEntityMetadata> Entities { get; }

    public static SimulatedMetadata Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Metadata file was not found.", fullPath);
        }

        var document = Deserialize<MetadataDocumentDto>(fullPath);
        ValidateContractVersion(document.ContractVersion, fullPath);

        var entityDtos = new List<MetadataEntityDto>();
        entityDtos.AddRange(document.Entities);

        foreach (var entityFile in document.EntityFiles)
        {
            if (string.IsNullOrWhiteSpace(entityFile.Path))
            {
                throw new SimulatedMetadataException("Metadata entityFiles entry is missing path.");
            }

            var entityPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath)!, entityFile.Path));
            if (!File.Exists(entityPath))
            {
                throw new FileNotFoundException("Metadata entity file was not found.", entityPath);
            }

            var entityDocument = Deserialize<MetadataEntityDocumentDto>(entityPath);
            ValidateContractVersion(entityDocument.ContractVersion, entityPath);

            if (entityDocument.Entity is null)
            {
                throw new SimulatedMetadataException($"Metadata entity file '{entityPath}' does not contain an entity.");
            }

            if (!string.IsNullOrWhiteSpace(entityFile.LogicalName)
                && !string.Equals(entityFile.LogicalName, entityDocument.Entity.LogicalName, StringComparison.OrdinalIgnoreCase))
            {
                throw new SimulatedMetadataException(
                    $"Metadata entity file '{entityPath}' contains entity '{entityDocument.Entity.LogicalName}' but index expected '{entityFile.LogicalName}'.");
            }

            entityDtos.Add(entityDocument.Entity);
        }

        if (entityDtos.Count == 0)
        {
            throw new SimulatedMetadataException("Metadata document does not define any entities.");
        }

        var entities = entityDtos.Select(entity => new SimulatedEntityMetadata(entity)).ToList();
        ValidateEntities(entities);

        return new SimulatedMetadata(document.ContractVersion!, entities);
    }

    public bool TryGetEntity(string logicalName, out SimulatedEntityMetadata entity)
    {
        return _entities.TryGetValue(logicalName, out entity!);
    }

    public SimulatedEntityMetadata GetEntity(string logicalName)
    {
        return TryGetEntity(logicalName, out var entity)
            ? entity
            : throw new SimulatedMetadataException($"Metadata does not define entity '{logicalName}'.");
    }

    private static T Deserialize<T>(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new SimulatedMetadataException($"Metadata file '{path}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new SimulatedMetadataException($"Metadata file '{path}' is not valid JSON: {ex.Message}");
        }
    }

    private static void ValidateContractVersion(string? contractVersion, string path)
    {
        if (string.IsNullOrWhiteSpace(contractVersion))
        {
            throw new SimulatedMetadataException($"Metadata file '{path}' does not define contractVersion.");
        }

        if (!string.Equals(contractVersion, SupportedContractVersion, StringComparison.Ordinal))
        {
            throw new SimulatedMetadataException(
                $"Metadata contractVersion '{contractVersion}' is not supported. Supported version is '{SupportedContractVersion}'.");
        }
    }

    private static void ValidateEntities(IReadOnlyList<SimulatedEntityMetadata> entities)
    {
        var duplicateEntity = entities
            .GroupBy(entity => entity.LogicalName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);

        if (duplicateEntity is not null)
        {
            throw new SimulatedMetadataException(
                string.IsNullOrWhiteSpace(duplicateEntity.Key)
                    ? "Metadata entity logicalName is required."
                    : $"Metadata defines entity '{duplicateEntity.Key}' more than once.");
        }

        var entityByName = entities.ToDictionary(
            entity => entity.LogicalName,
            StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            ValidateEntity(entity, entityByName);
        }
    }

    private static void ValidateEntity(
        SimulatedEntityMetadata entity,
        IReadOnlyDictionary<string, SimulatedEntityMetadata> entityByName)
    {
        if (string.IsNullOrWhiteSpace(entity.PrimaryIdAttribute))
        {
            throw new SimulatedMetadataException($"Metadata entity '{entity.LogicalName}' does not define primaryIdAttribute.");
        }

        if (string.IsNullOrWhiteSpace(entity.PrimaryNameAttribute))
        {
            throw new SimulatedMetadataException($"Metadata entity '{entity.LogicalName}' does not define primaryNameAttribute.");
        }

        var duplicateAttribute = entity.Attributes
            .GroupBy(attribute => attribute.LogicalName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);

        if (duplicateAttribute is not null)
        {
            throw new SimulatedMetadataException(
                string.IsNullOrWhiteSpace(duplicateAttribute.Key)
                    ? $"Metadata entity '{entity.LogicalName}' has an attribute without logicalName."
                    : $"Metadata entity '{entity.LogicalName}' defines attribute '{duplicateAttribute.Key}' more than once.");
        }

        entity.GetAttribute(entity.PrimaryIdAttribute);
        entity.GetAttribute(entity.PrimaryNameAttribute);

        foreach (var key in entity.AlternateKeys)
        {
            foreach (var attributeName in key.AttributeLogicalNames)
            {
                entity.GetAttribute(attributeName);
            }
        }

        foreach (var state in entity.StateStatus)
        {
            if (state.DefaultStatus is not null && !state.IsValidStatus(state.DefaultStatus.Value))
            {
                throw new SimulatedMetadataException(
                    $"Metadata entity '{entity.LogicalName}' state '{state.State}' has invalid defaultStatus '{state.DefaultStatus}'.");
            }
        }

        foreach (var relationship in entity.Relationships)
        {
            ValidateRelationship(entity, relationship);
        }
    }

    private static void ValidateRelationship(
        SimulatedEntityMetadata entity,
        SimulatedRelationshipMetadata relationship)
    {
        if (!string.Equals(entity.LogicalName, relationship.ReferencingEntity, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(entity.LogicalName, relationship.ReferencedEntity, StringComparison.OrdinalIgnoreCase))
        {
            throw new SimulatedMetadataException(
                $"Metadata relationship '{relationship.SchemaName}' is listed on entity '{entity.LogicalName}' but references '{relationship.ReferencingEntity}' and '{relationship.ReferencedEntity}'.");
        }
    }
}
