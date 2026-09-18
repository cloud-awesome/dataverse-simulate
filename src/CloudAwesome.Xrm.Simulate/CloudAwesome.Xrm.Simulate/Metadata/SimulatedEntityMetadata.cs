namespace CloudAwesome.Xrm.Simulate.Metadata;

public sealed class SimulatedEntityMetadata
{
    private readonly Dictionary<string, SimulatedAttributeMetadata> _attributes;
    private readonly Dictionary<int, SimulatedStateMetadata> _stateStatus;

    internal SimulatedEntityMetadata(MetadataEntityDto dto)
    {
        LogicalName = dto.LogicalName ?? string.Empty;
        SchemaName = dto.SchemaName ?? string.Empty;
        CollectionLogicalName = dto.CollectionLogicalName;
        CollectionSchemaName = dto.CollectionSchemaName;
        PrimaryIdAttribute = dto.PrimaryIdAttribute ?? string.Empty;
        PrimaryNameAttribute = dto.PrimaryNameAttribute ?? string.Empty;
        OwnershipType = dto.OwnershipType ?? string.Empty;
        IsActivity = dto.IsActivity;
        IsIntersect = dto.IsIntersect;
        IsValidForQueue = dto.IsValidForQueue;
        ValidMessages = dto.ValidMessages ?? [];
        Attributes = dto.Attributes.Select(attribute => new SimulatedAttributeMetadata(attribute)).ToList();
        StateStatus = dto.StateStatus.Select(state => new SimulatedStateMetadata(state)).ToList();
        AlternateKeys = dto.AlternateKeys.Select(key => new SimulatedAlternateKeyMetadata(key)).ToList();
        Relationships = dto.Relationships.Select(relationship => new SimulatedRelationshipMetadata(relationship)).ToList();

        _attributes = Attributes.ToDictionary(
            attribute => attribute.LogicalName,
            StringComparer.OrdinalIgnoreCase);
        _stateStatus = StateStatus.ToDictionary(state => state.State);
    }

    public string LogicalName { get; }

    public string SchemaName { get; }

    public string? CollectionLogicalName { get; }

    public string? CollectionSchemaName { get; }

    public string PrimaryIdAttribute { get; }

    public string PrimaryNameAttribute { get; }

    public string OwnershipType { get; }

    public bool IsActivity { get; }

    public bool IsIntersect { get; }

    public bool? IsValidForQueue { get; }

    public IReadOnlyList<string> ValidMessages { get; }

    public IReadOnlyList<SimulatedAttributeMetadata> Attributes { get; }

    public IReadOnlyList<SimulatedStateMetadata> StateStatus { get; }

    public IReadOnlyList<SimulatedAlternateKeyMetadata> AlternateKeys { get; }

    public IReadOnlyList<SimulatedRelationshipMetadata> Relationships { get; }

    public bool TryGetAttribute(string logicalName, out SimulatedAttributeMetadata attribute)
    {
        return _attributes.TryGetValue(logicalName, out attribute!);
    }

    public SimulatedAttributeMetadata GetAttribute(string logicalName)
    {
        return TryGetAttribute(logicalName, out var attribute)
            ? attribute
            : throw new SimulatedMetadataException(
                $"Metadata for entity '{LogicalName}' does not define attribute '{logicalName}'.");
    }

    public SimulatedStateMetadata? GetState(int state)
    {
        return _stateStatus.GetValueOrDefault(state);
    }

    public bool IsValidStateStatusPair(int state, int status)
    {
        return GetState(state)?.IsValidStatus(status) == true;
    }
}
