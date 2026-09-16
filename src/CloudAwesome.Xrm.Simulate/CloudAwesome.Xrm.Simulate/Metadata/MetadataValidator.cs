using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.Metadata;

internal static class MetadataValidator
{
    internal static SimulatedEntityMetadata? ValidateCreate(Entity entity, ISimulatorOptions? options)
    {
        var metadata = options?.Metadata;
        if (metadata is null)
        {
            return null;
        }

        var entityMetadata = metadata.GetEntity(entity.LogicalName);
        ValidateAttributes(entityMetadata, entity.Attributes, AttributeOperation.Create);
        ValidateRequiredCreateAttributes(entity, entityMetadata);
        ValidateStateStatus(entity, entityMetadata, allowDefaulting: false);

        return entityMetadata;
    }

    internal static void ApplyCreateDefaults(Entity entity, SimulatedEntityMetadata? entityMetadata)
    {
        if (entityMetadata is null)
        {
            return;
        }

        ApplyStateStatusDefaults(entity, entityMetadata);
    }

    internal static void ValidateUpdate(Entity entity, ISimulatorOptions? options)
    {
        var metadata = options?.Metadata;
        if (metadata is null)
        {
            return;
        }

        var entityMetadata = metadata.GetEntity(entity.LogicalName);
        ValidateAttributes(entityMetadata, entity.Attributes, AttributeOperation.Update);
        ValidateStateStatus(entity, entityMetadata, allowDefaulting: false);
    }

    internal static SimulatedEntityMetadata? ValidateRetrieve(
        string entityName,
        ColumnSet columnSet,
        ISimulatorOptions? options)
    {
        var metadata = options?.Metadata;
        if (metadata is null)
        {
            return null;
        }

        var entityMetadata = metadata.GetEntity(entityName);
        ValidateColumnSet(entityMetadata, columnSet);
        return entityMetadata;
    }

    internal static void ValidateQuery(QueryExpression query, ISimulatorOptions? options)
    {
        var metadata = options?.Metadata;
        if (metadata is null)
        {
            return;
        }

        var entityMetadata = metadata.GetEntity(query.EntityName);
        ValidateColumnSet(entityMetadata, query.ColumnSet);
        ValidateFilter(entityMetadata, query.Criteria);

        foreach (var order in query.Orders)
        {
            ValidateReadableAttribute(entityMetadata, order.AttributeName);
        }

        foreach (var link in query.LinkEntities)
        {
            ValidateLink(metadata, entityMetadata, link);
        }
    }

    internal static void ValidateQuery(QueryByAttribute query, ISimulatorOptions? options)
    {
        var metadata = options?.Metadata;
        if (metadata is null)
        {
            return;
        }

        var entityMetadata = metadata.GetEntity(query.EntityName);
        ValidateColumnSet(entityMetadata, query.ColumnSet);

        foreach (var attributeName in query.Attributes)
        {
            ValidateReadableAttribute(entityMetadata, attributeName);
        }
    }

    private static void ValidateAttributes(
        SimulatedEntityMetadata entityMetadata,
        AttributeCollection attributes,
        AttributeOperation operation)
    {
        foreach (var attribute in attributes)
        {
            var attributeMetadata = entityMetadata.GetAttribute(attribute.Key);
            ValidateOperation(attributeMetadata, entityMetadata, operation);
            ValidateValue(entityMetadata, attributeMetadata, attribute.Value);
        }
    }

    private static void ValidateRequiredCreateAttributes(Entity entity, SimulatedEntityMetadata entityMetadata)
    {
        foreach (var attribute in entityMetadata.Attributes)
        {
            if (attribute.IsPrimaryId)
            {
                continue;
            }

            if (!IsRequiredOnCreate(attribute))
            {
                continue;
            }

            if (!entity.Attributes.Contains(attribute.LogicalName) || entity[attribute.LogicalName] is null)
            {
                throw new SimulatedMetadataException(
                    $"Attribute '{entityMetadata.LogicalName}.{attribute.LogicalName}' is required for create.");
            }
        }
    }

    private static bool IsRequiredOnCreate(SimulatedAttributeMetadata attribute)
    {
        return attribute.IsValidForCreate == true
               && string.Equals(attribute.RequiredLevel, "ApplicationRequired", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateOperation(
        SimulatedAttributeMetadata attribute,
        SimulatedEntityMetadata entityMetadata,
        AttributeOperation operation)
    {
        var isValid = operation switch
        {
            AttributeOperation.Create => attribute.IsValidForCreate,
            AttributeOperation.Update => attribute.IsValidForUpdate,
            _ => true
        };

        if (isValid == false)
        {
            throw new SimulatedMetadataException(
                $"Attribute '{entityMetadata.LogicalName}.{attribute.LogicalName}' is not valid for {operation.ToString().ToLowerInvariant()}.");
        }
    }

    private static void ValidateColumnSet(SimulatedEntityMetadata entityMetadata, ColumnSet? columnSet)
    {
        if (columnSet is null || columnSet.AllColumns)
        {
            return;
        }

        foreach (var column in columnSet.Columns)
        {
            ValidateReadableAttribute(entityMetadata, column);
        }
    }

    private static void ValidateReadableAttribute(SimulatedEntityMetadata entityMetadata, string attributeName)
    {
        var attributeMetadata = entityMetadata.GetAttribute(attributeName);
        if (attributeMetadata.IsValidForRead == false)
        {
            throw new SimulatedMetadataException(
                $"Attribute '{entityMetadata.LogicalName}.{attributeMetadata.LogicalName}' is not valid for read.");
        }
    }

    private static void ValidateFilter(SimulatedEntityMetadata entityMetadata, FilterExpression? filter)
    {
        if (filter is null)
        {
            return;
        }

        foreach (var condition in filter.Conditions)
        {
            ValidateReadableAttribute(entityMetadata, condition.AttributeName);
        }

        foreach (var childFilter in filter.Filters)
        {
            ValidateFilter(entityMetadata, childFilter);
        }
    }

    private static void ValidateLink(
        SimulatedMetadata metadata,
        SimulatedEntityMetadata fromEntity,
        LinkEntity link)
    {
        fromEntity.GetAttribute(link.LinkFromAttributeName);
        var toEntity = metadata.GetEntity(link.LinkToEntityName);
        toEntity.GetAttribute(link.LinkToAttributeName);
        ValidateColumnSet(toEntity, link.Columns);
        ValidateFilter(toEntity, link.LinkCriteria);

        foreach (var order in link.Orders)
        {
            ValidateReadableAttribute(toEntity, order.AttributeName);
        }

        foreach (var childLink in link.LinkEntities)
        {
            ValidateLink(metadata, toEntity, childLink);
        }
    }

    private static void ValidateValue(
        SimulatedEntityMetadata entityMetadata,
        SimulatedAttributeMetadata attributeMetadata,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        switch (attributeMetadata.AttributeType)
        {
            case "String":
            case "Memo":
                ValidateMaxLength(entityMetadata, attributeMetadata, value);
                break;
            case "Integer":
            case "BigInt":
            case "Decimal":
            case "Double":
            case "Money":
                ValidateNumericRange(entityMetadata, attributeMetadata, value);
                ValidatePrecision(entityMetadata, attributeMetadata, value);
                break;
            case "Lookup":
            case "Customer":
            case "Owner":
                ValidateLookupTarget(entityMetadata, attributeMetadata, value);
                break;
            case "Picklist":
            case "Status":
            case "State":
                ValidateOption(entityMetadata, attributeMetadata, value);
                break;
            case "MultiSelectPicklist":
                ValidateMultiSelectOption(entityMetadata, attributeMetadata, value);
                break;
        }
    }

    private static void ValidateMaxLength(
        SimulatedEntityMetadata entityMetadata,
        SimulatedAttributeMetadata attributeMetadata,
        object value)
    {
        if (attributeMetadata.MaxLength is null || value is not string stringValue)
        {
            return;
        }

        if (stringValue.Length > attributeMetadata.MaxLength.Value)
        {
            throw new SimulatedMetadataException(
                $"Attribute '{entityMetadata.LogicalName}.{attributeMetadata.LogicalName}' exceeds max length {attributeMetadata.MaxLength.Value}.");
        }
    }

    private static void ValidateNumericRange(
        SimulatedEntityMetadata entityMetadata,
        SimulatedAttributeMetadata attributeMetadata,
        object value)
    {
        var numericValue = GetDecimalValue(value);
        if (numericValue is null)
        {
            return;
        }

        if (attributeMetadata.MinValue is not null && numericValue.Value < attributeMetadata.MinValue.Value)
        {
            throw new SimulatedMetadataException(
                $"Attribute '{entityMetadata.LogicalName}.{attributeMetadata.LogicalName}' is below minimum value {attributeMetadata.MinValue.Value}.");
        }

        if (attributeMetadata.MaxValue is not null && numericValue.Value > attributeMetadata.MaxValue.Value)
        {
            throw new SimulatedMetadataException(
                $"Attribute '{entityMetadata.LogicalName}.{attributeMetadata.LogicalName}' exceeds maximum value {attributeMetadata.MaxValue.Value}.");
        }
    }

    private static void ValidatePrecision(
        SimulatedEntityMetadata entityMetadata,
        SimulatedAttributeMetadata attributeMetadata,
        object value)
    {
        if (attributeMetadata.Precision is null)
        {
            return;
        }

        var numericValue = GetDecimalValue(value);
        if (numericValue is null)
        {
            return;
        }

        var decimals = BitConverter.GetBytes(decimal.GetBits(numericValue.Value)[3])[2];
        if (decimals > attributeMetadata.Precision.Value)
        {
            throw new SimulatedMetadataException(
                $"Attribute '{entityMetadata.LogicalName}.{attributeMetadata.LogicalName}' exceeds precision {attributeMetadata.Precision.Value}.");
        }
    }

    private static decimal? GetDecimalValue(object value)
    {
        return value switch
        {
            Money money => money.Value,
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue),
            float floatValue => Convert.ToDecimal(floatValue),
            int intValue => intValue,
            long longValue => longValue,
            _ => null
        };
    }

    private static void ValidateLookupTarget(
        SimulatedEntityMetadata entityMetadata,
        SimulatedAttributeMetadata attributeMetadata,
        object value)
    {
        if (attributeMetadata.LookupTargets.Count == 0 || value is not EntityReference reference)
        {
            return;
        }

        if (!attributeMetadata.LookupTargets.Contains(reference.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            throw new SimulatedMetadataException(
                $"Attribute '{entityMetadata.LogicalName}.{attributeMetadata.LogicalName}' does not allow lookup target '{reference.LogicalName}'.");
        }
    }

    private static void ValidateOption(
        SimulatedEntityMetadata entityMetadata,
        SimulatedAttributeMetadata attributeMetadata,
        object value)
    {
        if (attributeMetadata.Options.Count == 0)
        {
            return;
        }

        var optionValue = GetOptionValue(value);
        if (optionValue is null)
        {
            return;
        }

        if (!attributeMetadata.HasOption(optionValue.Value))
        {
            throw new SimulatedMetadataException(
                $"Attribute '{entityMetadata.LogicalName}.{attributeMetadata.LogicalName}' does not define option value {optionValue.Value}.");
        }
    }

    private static void ValidateMultiSelectOption(
        SimulatedEntityMetadata entityMetadata,
        SimulatedAttributeMetadata attributeMetadata,
        object value)
    {
        if (attributeMetadata.Options.Count == 0 || value is not OptionSetValueCollection values)
        {
            return;
        }

        foreach (var option in values)
        {
            if (!attributeMetadata.HasOption(option.Value))
            {
                throw new SimulatedMetadataException(
                    $"Attribute '{entityMetadata.LogicalName}.{attributeMetadata.LogicalName}' does not define option value {option.Value}.");
            }
        }
    }

    private static void ApplyStateStatusDefaults(Entity entity, SimulatedEntityMetadata entityMetadata)
    {
        var stateAttribute = entityMetadata.Attributes.FirstOrDefault(attribute => attribute.AttributeType == "State");
        var statusAttribute = entityMetadata.Attributes.FirstOrDefault(attribute => attribute.AttributeType == "Status");
        if (stateAttribute is null || statusAttribute is null || entityMetadata.StateStatus.Count == 0)
        {
            return;
        }

        var stateValue = entity.Attributes.TryGetValue(stateAttribute.LogicalName, out var state)
            ? GetOptionValue(state)
            : null;
        var statusValue = entity.Attributes.TryGetValue(statusAttribute.LogicalName, out var status)
            ? GetOptionValue(status)
            : null;

        if (stateValue is null && statusValue is null)
        {
            var defaultState = stateAttribute.DefaultValue ?? entityMetadata.StateStatus.First().State;
            var defaultStatus = entityMetadata.GetState(defaultState)?.DefaultStatus;
            entity[stateAttribute.LogicalName] = new OptionSetValue(defaultState);
            if (defaultStatus is not null)
            {
                entity[statusAttribute.LogicalName] = new OptionSetValue(defaultStatus.Value);
            }
        }
        else if (stateValue is not null && statusValue is null)
        {
            var defaultStatus = entityMetadata.GetState(stateValue.Value)?.DefaultStatus;
            if (defaultStatus is not null)
            {
                entity[statusAttribute.LogicalName] = new OptionSetValue(defaultStatus.Value);
            }
        }
        else if (stateValue is null && statusValue is not null)
        {
            var statusOption = statusAttribute.GetOption(statusValue.Value);
            if (statusOption?.State is not null)
            {
                entity[stateAttribute.LogicalName] = new OptionSetValue(statusOption.State.Value);
            }
        }

        ValidateStateStatus(entity, entityMetadata, allowDefaulting: true);
    }

    private static void ValidateStateStatus(
        Entity entity,
        SimulatedEntityMetadata entityMetadata,
        bool allowDefaulting)
    {
        var stateAttribute = entityMetadata.Attributes.FirstOrDefault(attribute => attribute.AttributeType == "State");
        var statusAttribute = entityMetadata.Attributes.FirstOrDefault(attribute => attribute.AttributeType == "Status");
        if (stateAttribute is null || statusAttribute is null || entityMetadata.StateStatus.Count == 0)
        {
            return;
        }

        var stateValue = entity.Attributes.TryGetValue(stateAttribute.LogicalName, out var state)
            ? GetOptionValue(state)
            : null;
        var statusValue = entity.Attributes.TryGetValue(statusAttribute.LogicalName, out var status)
            ? GetOptionValue(status)
            : null;

        if (!allowDefaulting && (stateValue is null || statusValue is null))
        {
            return;
        }

        if (stateValue is null || statusValue is null)
        {
            return;
        }

        if (!entityMetadata.IsValidStateStatusPair(stateValue.Value, statusValue.Value))
        {
            throw new SimulatedMetadataException(
                $"Entity '{entityMetadata.LogicalName}' does not allow state '{stateValue.Value}' with status '{statusValue.Value}'.");
        }
    }

    private static int? GetOptionValue(object? value)
    {
        return value switch
        {
            OptionSetValue option => option.Value,
            int intValue => intValue,
            _ => null
        };
    }

    private enum AttributeOperation
    {
        Create,
        Update
    }
}
