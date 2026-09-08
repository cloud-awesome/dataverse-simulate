using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate;

public sealed class SimulatedRelationship
{
    public required EntityReference Target { get; init; }

    public required Relationship Relationship { get; init; }

    public required EntityReferenceCollection RelatedEntities { get; init; }
}
