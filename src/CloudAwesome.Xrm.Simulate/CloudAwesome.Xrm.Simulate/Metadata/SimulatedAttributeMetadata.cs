namespace CloudAwesome.Xrm.Simulate.Metadata;

public sealed class SimulatedAttributeMetadata
{
    private readonly Dictionary<int, SimulatedOptionMetadata> _options;

    internal SimulatedAttributeMetadata(MetadataAttributeDto dto)
    {
        LogicalName = dto.LogicalName ?? string.Empty;
        SchemaName = dto.SchemaName ?? string.Empty;
        AttributeType = dto.AttributeType ?? string.Empty;
        AttributeTypeName = dto.AttributeTypeName ?? string.Empty;
        RequiredLevel = dto.RequiredLevel ?? string.Empty;
        IsPrimaryId = dto.IsPrimaryId;
        IsPrimaryName = dto.IsPrimaryName;
        IsValidForCreate = dto.IsValidForCreate;
        IsValidForUpdate = dto.IsValidForUpdate;
        IsValidForRead = dto.IsValidForRead;
        IsSecured = dto.IsSecured;
        LookupTargets = dto.LookupTargets ?? [];
        MaxLength = dto.MaxLength;
        MinValue = dto.MinValue;
        MaxValue = dto.MaxValue;
        Precision = dto.Precision;
        DefaultValue = TryGetInt(dto.DefaultValue);
        Format = dto.Format;
        Options = dto.Options.Select(option => new SimulatedOptionMetadata(option)).ToList();
        _options = Options.ToDictionary(option => option.Value);
    }

    public string LogicalName { get; }

    public string SchemaName { get; }

    public string AttributeType { get; }

    public string AttributeTypeName { get; }

    public string RequiredLevel { get; }

    public bool IsPrimaryId { get; }

    public bool IsPrimaryName { get; }

    public bool? IsValidForCreate { get; }

    public bool? IsValidForUpdate { get; }

    public bool? IsValidForRead { get; }

    public bool IsSecured { get; }

    public IReadOnlyList<string> LookupTargets { get; }

    public int? MaxLength { get; }

    public decimal? MinValue { get; }

    public decimal? MaxValue { get; }

    public int? Precision { get; }

    public int? DefaultValue { get; }

    public string? Format { get; }

    public IReadOnlyList<SimulatedOptionMetadata> Options { get; }

    public bool HasOption(int value)
    {
        return _options.ContainsKey(value);
    }

    public SimulatedOptionMetadata? GetOption(int value)
    {
        return _options.GetValueOrDefault(value);
    }

    private static int? TryGetInt(System.Text.Json.JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        return element.Value.ValueKind == System.Text.Json.JsonValueKind.Number
               && element.Value.TryGetInt32(out var value)
            ? value
            : null;
    }
}
