namespace CloudAwesome.Xrm.Simulate.Metadata;

public sealed class SimulatedOptionMetadata
{
    internal SimulatedOptionMetadata(MetadataOptionDto dto)
    {
        Value = dto.Value;
        Label = dto.Label;
        State = dto.State;
    }

    public int Value { get; }

    public string? Label { get; }

    public int? State { get; }
}
