using Microsoft.Xrm.Sdk;

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

    public Entity ToEntity()
    {
        var entity = new Entity("team", Id)
        {
            ["teamid"] = Id,
            ["businessunitid"] = new EntityReference("businessunit", BusinessUnitId),
            ["teamtype"] = new OptionSetValue(TeamType == SimulatedTeamType.Owner ? 0 : 1)
        };

        if (!string.IsNullOrWhiteSpace(Name))
        {
            entity["name"] = Name;
        }

        return entity;
    }
}
