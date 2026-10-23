using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

/// <summary>Shared ownership validation for create, update/upsert, and Assign.</summary>
internal sealed class RecordOwnership(MockedEntityDataService dataService, ISimulatorOptions? options)
{
    internal void PrepareCreate(Entity record)
    {
        if (IsOrganizationOwned(record.LogicalName))
        {
            RejectOwnershipColumns(record);
            return;
        }

        RejectDerivedOwnerColumns(record);

        var owner = record.GetAttributeValue<EntityReference>("ownerid") ?? dataService.AuthenticatedUser;
        var businessUnit = ValidateOwner(owner);
        ValidateBusinessUnit(record, businessUnit);
        DemandOwnerRead(record.LogicalName, owner);
        Apply(record, owner, businessUnit);
    }

    internal void PrepareUpdate(Entity changes, Entity existing, bool assign = false)
    {
        if (IsOrganizationOwned(existing.LogicalName))
        {
            if (assign)
                throw new NotSupportedException($"Organization-owned table '{existing.LogicalName}' cannot be assigned.");
            RejectOwnershipColumns(changes);
            return;
        }

        RejectDerivedOwnerColumns(changes);

        if (!changes.Attributes.ContainsKey("ownerid"))
        {
            if (changes.Attributes.ContainsKey("owningbusinessunit"))
                throw new NotSupportedException("Changing owningbusinessunit independently requires modernized business-unit ownership, which is not supported.");
            return;
        }

        var owner = changes.GetAttributeValue<EntityReference>("ownerid")
            ?? throw new ArgumentException("ownerid must identify a systemuser or owner team.");
        var enforcer = new SimulatedSecurityEnforcer(dataService);
        if (assign || !owner.Equals(existing.GetAttributeValue<EntityReference>("ownerid")))
        {
            enforcer.DemandRecordAccess(existing, SecurityPrivilege.Assign, options);
            enforcer.DemandRecordAccess(existing, SecurityPrivilege.Write, options);
            enforcer.DemandRecordAccess(existing, SecurityPrivilege.Read, options);
        }

        var businessUnit = ValidateOwner(owner);
        ValidateBusinessUnit(changes, businessUnit);
        DemandOwnerRead(existing.LogicalName, owner);
        Apply(changes, owner, businessUnit);
    }

    private bool IsOrganizationOwned(string logicalName) =>
        options?.Metadata?.TryGetEntity(logicalName, out var metadata) == true
        && string.Equals(metadata.OwnershipType, "OrganizationOwned", StringComparison.OrdinalIgnoreCase);

    private static void RejectOwnershipColumns(Entity entity)
    {
        if (new[] { "ownerid", "owninguser", "owningteam", "owningbusinessunit" }.Any(entity.Attributes.ContainsKey))
            throw new SimulatedMetadataException($"Organization-owned table '{entity.LogicalName}' has no record owner.");
    }

    private static void RejectDerivedOwnerColumns(Entity entity)
    {
        if (entity.Attributes.ContainsKey("owninguser") || entity.Attributes.ContainsKey("owningteam"))
            throw new NotSupportedException("owninguser and owningteam are derived from ownerid and cannot be set directly.");
    }

    private EntityReference? ValidateOwner(EntityReference owner)
    {
        if (owner.LogicalName is not ("systemuser" or "team") || owner.Id == Guid.Empty)
            throw new ArgumentException("ownerid must identify a systemuser or owner team.");

        if (dataService.IsSystemUser && owner.Id == MockedEntityDataService.SystemUserId)
            return dataService.BusinessUnit;

        var principal = dataService.Get(owner);
        if (owner.LogicalName == "team" && principal.GetAttributeValue<OptionSetValue>("teamtype")?.Value is not (null or 0))
            throw new NotSupportedException("Only owner teams can own records; access and group teams are not supported as owners.");
        return principal.GetAttributeValue<EntityReference>("businessunitid");
    }

    private static void ValidateBusinessUnit(Entity record, EntityReference? businessUnit)
    {
        if (record.Attributes.TryGetValue("owningbusinessunit", out var supplied)
            && (supplied is not EntityReference suppliedReference || !suppliedReference.Equals(businessUnit)))
            throw new NotSupportedException("Owning business unit must match the owner's business unit. Modernized business-unit ownership is not supported.");
    }

    private void DemandOwnerRead(string logicalName, EntityReference owner)
    {
        if (options?.SimulatedSecurityModel is not SimulatedSecurityModel model
            || (dataService.IsSystemUser && owner.Id == MockedEntityDataService.SystemUserId))
            return;
        var decision = PermissionsCalculator.CanPerform(new SecurityEvaluationContext(model, options.Metadata), owner, logicalName, SecurityPrivilege.Read);
        if (!decision.Allowed)
            throw DataverseServiceFaults.AccessDenied($"The proposed owner lacks Read privilege for '{logicalName}'. {decision.DenialReason}");
    }

    private static void Apply(Entity record, EntityReference owner, EntityReference? businessUnit)
    {
        record["ownerid"] = owner;
        record["owninguser"] = owner.LogicalName == "systemuser" ? owner : null;
        record["owningteam"] = owner.LogicalName == "team" ? owner : null;
        record["owningbusinessunit"] = businessUnit;
    }
}
