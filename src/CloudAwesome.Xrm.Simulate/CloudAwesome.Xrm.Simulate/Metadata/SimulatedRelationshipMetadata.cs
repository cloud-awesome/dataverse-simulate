namespace CloudAwesome.Xrm.Simulate.Metadata;

public sealed class SimulatedRelationshipMetadata
{
    internal SimulatedRelationshipMetadata(MetadataRelationshipDto dto)
    {
        SchemaName = dto.SchemaName ?? string.Empty;
        RelationshipType = dto.RelationshipType ?? string.Empty;
        ReferencingEntity = dto.ReferencingEntity ?? string.Empty;
        ReferencingAttribute = dto.ReferencingAttribute ?? string.Empty;
        ReferencedEntity = dto.ReferencedEntity ?? string.Empty;
        ReferencedAttribute = dto.ReferencedAttribute ?? string.Empty;
        IntersectEntity = dto.IntersectEntity;
        EntityRole = dto.EntityRole;
    }

    public string SchemaName { get; }

    public string RelationshipType { get; }

    public string ReferencingEntity { get; }

    public string ReferencingAttribute { get; }

    public string ReferencedEntity { get; }

    public string ReferencedAttribute { get; }

    public string? IntersectEntity { get; }

    public string? EntityRole { get; }
}
