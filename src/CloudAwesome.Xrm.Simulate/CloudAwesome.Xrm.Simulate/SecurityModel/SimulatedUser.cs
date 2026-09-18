using Microsoft.Xrm.Sdk;

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

    public Entity ToEntity()
    {
        var entity = new Entity("systemuser", Id)
        {
            ["businessunitid"] = new EntityReference("businessunit", BusinessUnitId)
        };

        if (!string.IsNullOrWhiteSpace(FullName))
        {
            entity["fullname"] = FullName;
        }

        return entity;
    }
}
