using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedRoleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoleName { get; set; } = string.Empty;
    public EntityReference Principal { get; set; } = new("systemuser", Guid.Empty);

    public SimulatedRoleAssignment()
    {
    }

    public SimulatedRoleAssignment(string roleName, EntityReference principal)
    {
        RoleName = roleName;
        Principal = principal;
    }
}
