using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

internal static class EntityCloner
{
    public static Entity Clone(Entity entity)
        => Clone(entity, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));

    private static Entity Clone(Entity entity, Dictionary<object, object> copies)
    {
        if (copies.TryGetValue(entity, out var existing)) return (Entity)existing;
        var clone = CreateEmptyEntity(entity);
        copies[entity] = clone;
        clone.Id = entity.Id;
        clone.EntityState = entity.EntityState;
        clone.RowVersion = entity.RowVersion;

        foreach (var attribute in entity.Attributes)
        {
            clone.Attributes[attribute.Key] = CloneValue(attribute.Value, copies)!;
        }

        foreach (var formattedValue in entity.FormattedValues)
        {
            clone.FormattedValues[formattedValue.Key] = formattedValue.Value;
        }

        foreach (var keyAttribute in entity.KeyAttributes)
        {
            clone.KeyAttributes[keyAttribute.Key] = CloneValue(keyAttribute.Value, copies)!;
        }

        foreach (var relatedEntity in entity.RelatedEntities)
        {
            var relationship = new Relationship(relatedEntity.Key.SchemaName)
            {
                PrimaryEntityRole = relatedEntity.Key.PrimaryEntityRole
            };
            clone.RelatedEntities[relationship] = (EntityCollection)CloneValue(relatedEntity.Value, copies)!;
        }

        return clone;
    }

    private static object? CloneValue(object? value, Dictionary<object, object> copies)
    {
        if (value is null) return null;
        if (copies.TryGetValue(value, out var existing)) return existing;

        switch (value)
        {
            case Entity entity:
                return Clone(entity, copies);
            case EntityReference reference:
                var referenceCopy = new EntityReference(reference.LogicalName, reference.Id)
                {
                    Name = reference.Name,
                    RowVersion = reference.RowVersion
                };
                copies[value] = referenceCopy;
                foreach (var key in reference.KeyAttributes)
                    referenceCopy.KeyAttributes[key.Key] = CloneValue(key.Value, copies)!;
                return referenceCopy;
            case Money money:
                var moneyCopy = new Money(money.Value);
                copies[value] = moneyCopy;
                return moneyCopy;
            case OptionSetValue option:
                var optionCopy = new OptionSetValue(option.Value);
                copies[value] = optionCopy;
                return optionCopy;
            case OptionSetValueCollection options:
                var optionsCopy = new OptionSetValueCollection();
                copies[value] = optionsCopy;
                foreach (var item in options)
                    optionsCopy.Add((OptionSetValue)CloneValue(item, copies)!);
                return optionsCopy;
            case AliasedValue alias:
                var aliasCopy = new AliasedValue(alias.EntityLogicalName, alias.AttributeLogicalName,
                    CloneValue(alias.Value, copies));
                copies[value] = aliasCopy;
                return aliasCopy;
            case EntityCollection collection:
                var collectionCopy = new EntityCollection
                {
                    EntityName = collection.EntityName,
                    MoreRecords = collection.MoreRecords,
                    PagingCookie = collection.PagingCookie,
                    TotalRecordCount = collection.TotalRecordCount,
                    TotalRecordCountLimitExceeded = collection.TotalRecordCountLimitExceeded,
                    MinActiveRowVersion = collection.MinActiveRowVersion
                };
                copies[value] = collectionCopy;
                foreach (var item in collection.Entities) collectionCopy.Entities.Add(Clone(item, copies));
                return collectionCopy;
            case byte[] bytes:
                var bytesCopy = bytes.ToArray();
                copies[value] = bytesCopy;
                return bytesCopy;
            default:
                return value;
        }
    }

    public static Entity Project(Entity entity, ColumnSet? columnSet, bool includePrimaryId = true)
        => Project(entity, columnSet, includePrimaryId, null);

    internal static Entity Project(Entity entity, ColumnSet? columnSet, bool includePrimaryId,
        string? primaryIdAttribute)
    {
        primaryIdAttribute ??= $"{entity.LogicalName}id";
        var isAggregate = columnSet?.AttributeExpressions.Any(expression =>
            expression.HasGroupBy || expression.AggregateType != XrmAggregateType.None) == true;
        if (columnSet?.AllColumns == true || isAggregate)
        {
            var clone = Clone(entity);
            if (!isAggregate)
            {
                clone[primaryIdAttribute] = entity.Id;
            }

            return clone;
        }

        var projected = CreateEmptyEntity(entity);
        projected.EntityState = entity.EntityState;
        projected.RowVersion = entity.RowVersion;

        foreach (var column in columnSet?.Columns.AsEnumerable() ?? Enumerable.Empty<string>())
        {
            CopyAttribute(entity, projected, column);
        }

        if (includePrimaryId || columnSet?.Columns.Contains(primaryIdAttribute) == true)
        {
            CopyAttribute(entity, projected, primaryIdAttribute);
            projected[primaryIdAttribute] = entity.Id;
        }

        // Distinct rows have no source identity unless the primary id was selected.
        if (projected.Attributes.TryGetValue(primaryIdAttribute, out var id) && id is Guid primaryId)
        {
            projected.Id = primaryId;
        }

        foreach (var attribute in entity.Attributes.Where(attribute => attribute.Value is AliasedValue))
        {
            CopyAttribute(entity, projected, attribute.Key);
        }

        return projected;
    }

    private static void CopyAttribute(Entity source, Entity target, string attributeName)
    {
        if (!source.Attributes.TryGetValue(attributeName, out var value))
        {
            return;
        }

        target[attributeName] = value;

        if (source.FormattedValues.TryGetValue(attributeName, out var formattedValue))
        {
            target.FormattedValues[attributeName] = formattedValue;
        }
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
