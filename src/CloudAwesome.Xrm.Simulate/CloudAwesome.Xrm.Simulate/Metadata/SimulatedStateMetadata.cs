namespace CloudAwesome.Xrm.Simulate.Metadata;

public sealed class SimulatedStateMetadata
{
    private readonly Dictionary<int, SimulatedStatusMetadata> _statuses;

    internal SimulatedStateMetadata(MetadataStateStatusDto dto)
    {
        State = dto.State;
        StateLabel = dto.StateLabel;
        DefaultStatus = dto.DefaultStatus;
        Statuses = dto.Statuses.Select(status => new SimulatedStatusMetadata(status)).ToList();
        _statuses = Statuses.ToDictionary(status => status.Value);
    }

    public int State { get; }

    public string? StateLabel { get; }

    public int? DefaultStatus { get; }

    public IReadOnlyList<SimulatedStatusMetadata> Statuses { get; }

    public bool IsValidStatus(int status)
    {
        return _statuses.ContainsKey(status);
    }
}
