using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedBusinessUnit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentBusinessUnitId { get; set; }

    public SimulatedBusinessUnit()
    {
    }

    public SimulatedBusinessUnit(Guid id, string name, Guid? parentBusinessUnitId = null)
    {
        Id = id;
        Name = name;
        ParentBusinessUnitId = parentBusinessUnitId;
    }

    public Entity ToEntity()
    {
        var entity = new Entity("businessunit", Id)
        {
            ["businessunitid"] = Id,
            ["name"] = Name
        };

        if (ParentBusinessUnitId is { } parentBusinessUnitId)
        {
            entity["parentbusinessunitid"] = new EntityReference("businessunit", parentBusinessUnitId);
        }

        return entity;
    }
}
