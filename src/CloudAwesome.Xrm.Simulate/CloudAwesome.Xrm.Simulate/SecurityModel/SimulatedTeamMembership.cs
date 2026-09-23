namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedTeamMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }

    public SimulatedTeamMembership()
    {
    }

    public SimulatedTeamMembership(Guid teamId, Guid userId)
    {
        TeamId = teamId;
        UserId = userId;
    }
}
