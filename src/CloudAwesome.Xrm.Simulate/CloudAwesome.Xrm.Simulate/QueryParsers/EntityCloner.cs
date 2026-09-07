using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

internal static class EntityCloner
{
    public static Entity Clone(Entity entity)
    {
        var clone = CreateEmptyEntity(entity);
        clone.Id = entity.Id;
        clone.EntityState = entity.EntityState;
        clone.RowVersion = entity.RowVersion;

        foreach (var attribute in entity.Attributes)
        {
            clone.Attributes[attribute.Key] = attribute.Value;
        }

        foreach (var formattedValue in entity.FormattedValues)
        {
            clone.FormattedValues[formattedValue.Key] = formattedValue.Value;
        }

        foreach (var keyAttribute in entity.KeyAttributes)
        {
            clone.KeyAttributes[keyAttribute.Key] = keyAttribute.Value;
        }

        foreach (var relatedEntity in entity.RelatedEntities)
        {
            clone.RelatedEntities[relatedEntity.Key] = relatedEntity.Value;
        }

        return clone;
    }

    public static Entity Project(Entity entity, ColumnSet? columnSet, bool includePrimaryId = true)
    {
        if (columnSet == null || columnSet.AllColumns || columnSet.Columns.Count == 0)
        {
            return Clone(entity);
        }

        var projected = CreateEmptyEntity(entity);
        projected.Id = entity.Id;
        projected.EntityState = entity.EntityState;
        projected.RowVersion = entity.RowVersion;

        foreach (var column in columnSet.Columns)
        {
            CopyAttribute(entity, projected, column);
        }

        if (includePrimaryId)
        {
            foreach (var attribute in entity.Attributes.Where(attribute => IsPrimaryIdAttribute(entity, attribute)))
            {
                CopyAttribute(entity, projected, attribute.Key);
            }
        }

        foreach (var attribute in entity.Attributes.Where(attribute => attribute.Value is AliasedValue))
        {
            CopyAttribute(entity, projected, attribute.Key);
        }

        return projected;
    }

    private static void CopyAttribute(Entity source, Entity target, string attributeName)
    {
        target[attributeName] = source[attributeName];

        if (source.FormattedValues.TryGetValue(attributeName, out var formattedValue))
        {
            target.FormattedValues[attributeName] = formattedValue;
        }
    }

    private static bool IsPrimaryIdAttribute(Entity entity, KeyValuePair<string, object> attribute)
    {
        return attribute.Value is Guid id &&
               id == entity.Id &&
               attribute.Key.EndsWith("id", StringComparison.OrdinalIgnoreCase);
    }

    private static Entity CreateEmptyEntity(Entity entity)
    {
        if (entity.GetType() == typeof(Entity))
        {
            return new Entity(entity.LogicalName);
        }

        return (Entity)Activator.CreateInstance(entity.GetType())!;
    }
}
