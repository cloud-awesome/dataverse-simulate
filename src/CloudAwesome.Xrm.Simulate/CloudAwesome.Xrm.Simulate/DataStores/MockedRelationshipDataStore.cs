using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.DataStores;

internal sealed class MockedRelationshipDataStore
{
    private readonly HashSet<StoredRelationship> _relationships = new();

    internal IReadOnlyCollection<StoredRelationship> Get()
    {
        return _relationships.ToList();
    }

    internal IReadOnlyCollection<StoredRelationship> Get(EntityReference target, Relationship relationship)
    {
        return _relationships
            .Where(r =>
                r.TargetLogicalName == target.LogicalName &&
                r.TargetId == target.Id &&
                RelationshipMatches(r, relationship))
            .ToList();
    }

    internal void Associate( EntityReference target, Relationship relationship, IEnumerable<EntityReference> relatedEntities)
    {
        foreach (var relatedEntity in relatedEntities)
        {
            _relationships.Add(ToStoredRelationship(target, relationship, relatedEntity));
        }
    }

    internal void Disassociate(
        EntityReference target,
        Relationship relationship,
        IEnumerable<EntityReference> relatedEntities)
    {
        foreach (var relatedEntity in relatedEntities)
        {
            _relationships.Remove(ToStoredRelationship(target, relationship, relatedEntity));
        }
    }

    internal void Clear()
    {
        _relationships.Clear();
    }

    private static StoredRelationship ToStoredRelationship(
        EntityReference target,
        Relationship relationship,
        EntityReference relatedEntity)
    {
        return new StoredRelationship(
            relationship.SchemaName,
            relationship.PrimaryEntityRole,
            target.LogicalName,
            target.Id,
            relatedEntity.LogicalName,
            relatedEntity.Id);
    }

    private static bool RelationshipMatches(StoredRelationship stored, Relationship relationship)
    {
        return stored.SchemaName == relationship.SchemaName &&
               stored.PrimaryEntityRole == relationship.PrimaryEntityRole;
    }
}
