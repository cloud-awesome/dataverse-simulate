namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedUser
{
    public Guid Id { get; set; }
    public Guid BusinessUnitId { get; set; }
    public string? FullName { get; set; }

    public SimulatedUser()
    {
    }

    public SimulatedUser(Guid id, Guid businessUnitId, string? fullName = null)
    {
        Id = id;
        BusinessUnitId = businessUnitId;
        FullName = fullName;
    }
}
