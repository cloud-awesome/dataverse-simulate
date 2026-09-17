using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedPrincipalObjectAccess
{
    public EntityReference Target { get; set; } = new(string.Empty, Guid.Empty);
    public EntityReference Principal { get; set; } = new("systemuser", Guid.Empty);
    public AccessRights AccessRights { get; set; }

    public SimulatedPrincipalObjectAccess()
    {
    }

    public SimulatedPrincipalObjectAccess(
        EntityReference target,
        EntityReference principal,
        AccessRights accessRights)
    {
        Target = target;
        Principal = principal;
        AccessRights = accessRights;
    }
}
