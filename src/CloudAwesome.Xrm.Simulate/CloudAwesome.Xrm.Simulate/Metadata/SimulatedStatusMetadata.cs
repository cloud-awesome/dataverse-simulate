namespace CloudAwesome.Xrm.Simulate.Metadata;

public sealed class SimulatedStatusMetadata
{
    internal SimulatedStatusMetadata(MetadataStatusDto dto)
    {
        Value = dto.Value;
        Label = dto.Label;
    }

    public int Value { get; }

    public string? Label { get; }
}
