namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedTeam
{
    public Guid Id { get; set; }
    public Guid BusinessUnitId { get; set; }
    public string? Name { get; set; }
    public SimulatedTeamType TeamType { get; set; } = SimulatedTeamType.Owner;

    public SimulatedTeam()
    {
    }

    public SimulatedTeam(
        Guid id,
        Guid businessUnitId,
        string? name = null,
        SimulatedTeamType teamType = SimulatedTeamType.Owner)
    {
        Id = id;
        BusinessUnitId = businessUnitId;
        Name = name;
        TeamType = teamType;
    }
}
