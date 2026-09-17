using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public static class PermissionsCalculator
{
    public static SecurityDecision CanPerform(
        SecurityEvaluationContext context,
        EntityReference principal,
        string entityLogicalName,
        SecurityPrivilege privilege)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name must be provided.", nameof(entityLogicalName));

        context.SecurityModel.Validate();

        var permission = context.SecurityModel.GetEffectiveEntityPermissionForPrincipal(principal, entityLogicalName);
        if (permission is null)
        {
            return context.SecurityModel.IgnoreMissingEntities
                ? SecurityDecision.Allow(PrivilegeDepthEnum.Organization)
                : SecurityDecision.Deny(
                    $"No simulated security permissions exist for entity '{entityLogicalName}'.");
        }

        var depth = permission.GetDepth(privilege);

        return depth == PrivilegeDepthEnum.None
            ? SecurityDecision.Deny(
                $"Principal '{principal.LogicalName}:{principal.Id}' does not have {privilege} privilege for entity '{entityLogicalName}'.")
            : SecurityDecision.Allow(depth);
    }

    public static SecurityDecision CanAccessRecord(
        SecurityEvaluationContext context,
        EntityReference principal,
        Entity record,
        SecurityPrivilege privilege)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));

        var tableDecision = CanPerform(context, principal, record.LogicalName, privilege);
        if (!tableDecision.Allowed)
            return tableDecision;

        if (privilege == SecurityPrivilege.Create || tableDecision.EffectiveDepth == PrivilegeDepthEnum.Organization)
            return tableDecision;

        if (IsAccessAllowedByDepth(context.SecurityModel, principal, record, tableDecision.EffectiveDepth))
            return tableDecision;

        return IsAccessAllowedByShare(context.SecurityModel, principal, record, privilege)
            ? tableDecision
            : SecurityDecision.Deny(
                $"Principal '{principal.LogicalName}:{principal.Id}' does not have {privilege} access to {record.LogicalName}:{record.Id}.",
                tableDecision.EffectiveDepth);
    }

    public static IReadOnlyList<Entity> FilterReadableRecords(
        SecurityEvaluationContext context,
        EntityReference principal,
        string entityLogicalName,
        IEnumerable<Entity> records)
    {
        if (records is null) throw new ArgumentNullException(nameof(records));

        var tableDecision = CanPerform(context, principal, entityLogicalName, SecurityPrivilege.Read);
        if (!tableDecision.Allowed)
            return [];

        return records
            .Where(record => CanAccessRecord(context, principal, record, SecurityPrivilege.Read).Allowed)
            .ToList();
    }

    /// <summary>
    /// Compatibility wrapper for existing service code. New code should use CanPerform.
    /// </summary>
    public static bool ValidateEntityPermission(string entityLogicalName, string message, ISimulatorOptions? options)
    {
        if (options?.SimulatedSecurityModel is null) return true;

        var privilege = ParsePrivilege(message);
        var principal = GetPrincipal(options);

        if (options.SimulatedSecurityModel is SimulatedSecurityModel securityModel)
        {
            return CanPerform(
                    new SecurityEvaluationContext(securityModel),
                    principal,
                    entityLogicalName,
                    privilege)
                .Allowed;
        }

        return ValidateLegacyEntityPermission(entityLogicalName, privilege, options.SimulatedSecurityModel);
    }

    /// <summary>
    /// Compatibility wrapper for existing service code. New code should use CanAccessRecord.
    /// </summary>
    public static bool ValidateEntityPermission(
        string entityLogicalName,
        string message,
        ISimulatorOptions? options,
        Entity entity)
    {
        if (options?.SimulatedSecurityModel is null) return true;

        var privilege = ParsePrivilege(message);
        var principal = GetPrincipal(options);

        return options.SimulatedSecurityModel is SimulatedSecurityModel securityModel
            ? CanAccessRecord(new SecurityEvaluationContext(securityModel), principal, entity, privilege).Allowed
            : ValidateLegacyEntityPermission(entityLogicalName, privilege, options.SimulatedSecurityModel);
    }

    /// <summary>
    /// Compatibility wrapper for existing service code. New code should use FilterReadableRecords.
    /// </summary>
    public static bool ValidateEntityPermission(
        string entityLogicalName,
        string message,
        ISimulatorOptions? options,
        List<Entity> entities)
    {
        if (options?.SimulatedSecurityModel is null) return true;

        var privilege = ParsePrivilege(message);
        var principal = GetPrincipal(options);

        if (options.SimulatedSecurityModel is not SimulatedSecurityModel securityModel)
            return ValidateLegacyEntityPermission(entityLogicalName, privilege, options.SimulatedSecurityModel);

        return entities.All(entity =>
            CanAccessRecord(new SecurityEvaluationContext(securityModel), principal, entity, privilege).Allowed);
    }

    private static bool IsAccessAllowedByDepth(
        SimulatedSecurityModel securityModel,
        EntityReference principal,
        Entity record,
        PrivilegeDepthEnum depth)
    {
        var owner = GetOwner(record);
        if (owner is null)
            return false;

        if (depth == PrivilegeDepthEnum.User)
            return IsUserDepthMatch(securityModel, principal, owner);

        var principalBusinessUnitId = securityModel.GetPrincipalBusinessUnitId(principal);
        var ownerBusinessUnitId = securityModel.GetPrincipalBusinessUnitId(owner);

        if (principalBusinessUnitId is null || ownerBusinessUnitId is null)
            return false;

        return depth switch
        {
            PrivilegeDepthEnum.BusinessUnit => principalBusinessUnitId == ownerBusinessUnitId,
            PrivilegeDepthEnum.ParentChild => securityModel.IsBusinessUnitDescendantOrSelf(
                ownerBusinessUnitId.Value,
                principalBusinessUnitId.Value),
            _ => false
        };
    }

    private static bool IsUserDepthMatch(
        SimulatedSecurityModel securityModel,
        EntityReference principal,
        EntityReference owner)
    {
        if (PrincipalMatches(principal, owner))
            return true;

        if (principal.LogicalName != "systemuser" || owner.LogicalName != "team")
            return false;

        return securityModel.GetTeamIdsForUser(principal.Id).Contains(owner.Id);
    }

    private static bool IsAccessAllowedByShare(
        SimulatedSecurityModel securityModel,
        EntityReference principal,
        Entity record,
        SecurityPrivilege privilege)
    {
        var requiredAccess = ToAccessRights(privilege);
        if (requiredAccess is null)
            return false;

        var target = new EntityReference(record.LogicalName, record.Id);
        var principalsToCheck = GetSharePrincipals(securityModel, principal);

        return principalsToCheck.Any(sharePrincipal =>
            securityModel
                .GetPrincipalObjectAccesses(target, sharePrincipal)
                .Any(access => access.AccessRights.HasFlag(requiredAccess.Value)));
    }

    private static IEnumerable<EntityReference> GetSharePrincipals(
        SimulatedSecurityModel securityModel,
        EntityReference principal)
    {
        yield return principal;

        if (principal.LogicalName != "systemuser")
            yield break;

        foreach (var teamId in securityModel.GetTeamIdsForUser(principal.Id))
        {
            yield return new EntityReference("team", teamId);
        }
    }

    private static EntityReference? GetOwner(Entity record)
    {
        if (!record.Attributes.TryGetValue(EntityConstants.OwnerId, out var owner))
            return null;

        return owner as EntityReference;
    }

    private static AccessRights? ToAccessRights(SecurityPrivilege privilege) =>
        privilege switch
        {
            SecurityPrivilege.Read => AccessRights.ReadAccess,
            SecurityPrivilege.Write => AccessRights.WriteAccess,
            SecurityPrivilege.Delete => AccessRights.DeleteAccess,
            SecurityPrivilege.Append => AccessRights.AppendAccess,
            SecurityPrivilege.AppendTo => AccessRights.AppendToAccess,
            SecurityPrivilege.Assign => AccessRights.AssignAccess,
            SecurityPrivilege.Share => AccessRights.ShareAccess,
            _ => null
        };

    private static SecurityPrivilege ParsePrivilege(string message)
    {
        if (Enum.TryParse<SecurityPrivilege>(message, ignoreCase: true, out var privilege))
            return privilege;

        throw new ArgumentException($"The message to validate is not recognised: {message}");
    }

    private static EntityReference GetPrincipal(ISimulatorOptions options)
    {
        if (options.AuthenticatedUser is not null)
            return options.AuthenticatedUser.ToEntityReference();

        if (options.SimulatedSecurityModel is SimulatedSecurityModel { Users.Count: 1 } securityModel)
            return new EntityReference("systemuser", securityModel.Users.Single().Id);

        return new EntityReference("systemuser", Guid.Empty);
    }

    private static bool ValidateLegacyEntityPermission(
        string entityLogicalName,
        SecurityPrivilege privilege,
        ISecurityModel securityModel)
    {
        var entityPermissions = securityModel.EntityPermissions.SingleOrDefault(x =>
            string.Equals(x.LogicalName, entityLogicalName, StringComparison.OrdinalIgnoreCase));

        if (entityPermissions is null)
            return securityModel.IgnoreMissingEntities;

        return GetDepth(entityPermissions, privilege) != PrivilegeDepthEnum.None;
    }

    private static PrivilegeDepthEnum GetDepth(IEntityPermission entityPermission, SecurityPrivilege privilege) =>
        privilege switch
        {
            SecurityPrivilege.Create => entityPermission.Create,
            SecurityPrivilege.Read => entityPermission.Read,
            SecurityPrivilege.Write => entityPermission.Write,
            SecurityPrivilege.Delete => entityPermission.Delete,
            SecurityPrivilege.Append => entityPermission.Append,
            SecurityPrivilege.AppendTo => entityPermission.AppendTo,
            SecurityPrivilege.Assign => entityPermission.Assign,
            SecurityPrivilege.Share => entityPermission.Share,
            _ => PrivilegeDepthEnum.None
        };

    private static bool PrincipalMatches(EntityReference first, EntityReference second) =>
        first.Id == second.Id &&
        string.Equals(first.LogicalName, second.LogicalName, StringComparison.OrdinalIgnoreCase);
}
