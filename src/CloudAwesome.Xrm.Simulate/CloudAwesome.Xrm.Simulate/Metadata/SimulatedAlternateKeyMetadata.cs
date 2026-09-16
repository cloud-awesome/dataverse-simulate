namespace CloudAwesome.Xrm.Simulate.Metadata;

public sealed class SimulatedAlternateKeyMetadata
{
    internal SimulatedAlternateKeyMetadata(MetadataAlternateKeyDto dto)
    {
        KeyName = dto.KeyName ?? dto.Name ?? string.Empty;
        EntityLogicalName = dto.EntityLogicalName ?? string.Empty;
        AttributeLogicalNames = dto.AttributeLogicalNames ?? dto.Attributes ?? [];
        KeyStatus = dto.KeyStatus;
    }

    public string KeyName { get; }

    public string EntityLogicalName { get; }

    public IReadOnlyList<string> AttributeLogicalNames { get; }

    public string? KeyStatus { get; }
}
