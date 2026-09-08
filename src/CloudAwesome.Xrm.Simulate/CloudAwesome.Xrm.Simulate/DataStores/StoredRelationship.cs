using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.DataStores;

internal sealed record StoredRelationship(
    string SchemaName,
    EntityRole? PrimaryEntityRole,
    string TargetLogicalName,
    Guid TargetId,
    string RelatedLogicalName,
    Guid RelatedId);
