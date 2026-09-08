using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.DataStores;

internal sealed record SimulatedRelationship(
    string SchemaName,
    EntityRole? PrimaryEntityRole,
    string TargetLogicalName,
    Guid TargetId,
    string RelatedLogicalName,
    Guid RelatedId);
