using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

internal static class RoleAssignmentAssociationHandler
{
    private const string TeamRolesRelationshipName = "teamroles_association";
    private const string SystemUserRolesRelationshipName = "systemuserroles_association";

    internal static bool TryAssociate(
        MockedEntityDataService dataService,
        EntityReference target,
        Relationship relationship,
        EntityReferenceCollection relatedEntities,
        ISimulatorOptions? options)
    {
        if (options?.SimulatedSecurityModel is not SimulatedSecurityModel securityModel ||
            !TryGetRoleAssignmentShape(target, relationship, out var principalLogicalName))
        {
            return false;
        }

        foreach (var relatedEntity in relatedEntities)
        {
            if (!TryResolvePrincipalAndRole(dataService, target, relatedEntity, principalLogicalName, out var principal, out var role))
            {
                return false;
            }

            ValidateRoleBusinessUnit(principal, role);
            securityModel.AssignRole(role.GetAttributeValue<string>("name"), principal.ToEntityReference());
            securityModel.Validate();

            var assignment = securityModel.RoleAssignments.Single(x =>
                string.Equals(x.RoleName, role.GetAttributeValue<string>("name"), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Principal.LogicalName, principal.LogicalName, StringComparison.OrdinalIgnoreCase) &&
                x.Principal.Id == principal.Id);

            dataService.Upsert(SimulatedSecurityModelDataSeeder.CreateRoleAssignmentEntity(
                assignment,
                securityModel,
                SimulatedSecurityModelDataSeeder.ResolveRootBusinessUnitId(dataService, securityModel)));
        }

        dataService.Associate(target, relationship, relatedEntities);
        return true;
    }

    internal static bool TryDisassociate(
        MockedEntityDataService dataService,
        EntityReference target,
        Relationship relationship,
        EntityReferenceCollection relatedEntities,
        ISimulatorOptions? options)
    {
        if (options?.SimulatedSecurityModel is not SimulatedSecurityModel securityModel ||
            !TryGetRoleAssignmentShape(target, relationship, out var principalLogicalName))
        {
            return false;
        }

        foreach (var relatedEntity in relatedEntities)
        {
            if (!TryResolvePrincipalAndRole(dataService, target, relatedEntity, principalLogicalName, out var principal, out var role))
            {
                return false;
            }

            var roleName = role.GetAttributeValue<string>("name");
            securityModel.RemoveRoleAssignment(roleName, principal.ToEntityReference());
            securityModel.Validate();

            RemoveRoleAssignmentRows(dataService, principal, role, principalLogicalName);
        }

        dataService.Disassociate(target, relationship, relatedEntities);
        return true;
    }

    private static bool TryGetRoleAssignmentShape(
        EntityReference target,
        Relationship relationship,
        out string principalLogicalName)
    {
        principalLogicalName = relationship.SchemaName switch
        {
            TeamRolesRelationshipName => "team",
            SystemUserRolesRelationshipName => "systemuser",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(principalLogicalName) &&
               (string.Equals(target.LogicalName, principalLogicalName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target.LogicalName, "role", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolvePrincipalAndRole(
        MockedEntityDataService dataService,
        EntityReference target,
        EntityReference relatedEntity,
        string principalLogicalName,
        out Entity principal,
        out Entity role)
    {
        principal = null!;
        role = null!;

        if (string.Equals(target.LogicalName, principalLogicalName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(relatedEntity.LogicalName, "role", StringComparison.OrdinalIgnoreCase))
        {
            principal = dataService.Get(target);
            role = dataService.Get(relatedEntity);
            return true;
        }

        if (string.Equals(target.LogicalName, "role", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(relatedEntity.LogicalName, principalLogicalName, StringComparison.OrdinalIgnoreCase))
        {
            role = dataService.Get(target);
            principal = dataService.Get(relatedEntity);
            return true;
        }

        return false;
    }

    private static void ValidateRoleBusinessUnit(Entity principal, Entity role)
    {
        var principalBusinessUnitId = principal.GetAttributeValue<EntityReference>("businessunitid")?.Id;
        var roleBusinessUnitId = role.GetAttributeValue<EntityReference>("businessunitid")?.Id;

        if (principalBusinessUnitId is null || roleBusinessUnitId is null || principalBusinessUnitId == roleBusinessUnitId)
        {
            return;
        }

        throw DataverseServiceFaults.AccessDenied(
            $"Role '{role.Id}' belongs to business unit '{roleBusinessUnitId}', but principal '{principal.LogicalName}:{principal.Id}' belongs to business unit '{principalBusinessUnitId}'.");
    }

    private static void RemoveRoleAssignmentRows(
        MockedEntityDataService dataService,
        Entity principal,
        Entity role,
        string principalLogicalName)
    {
        var intersectLogicalName = principalLogicalName == "team" ? "teamroles" : "systemuserroles";
        var principalIdAttribute = principalLogicalName == "team" ? "teamid" : "systemuserid";

        var rows = dataService.Get(intersectLogicalName)
            .Where(x =>
                x.GetAttributeValue<Guid>(principalIdAttribute) == principal.Id &&
                x.GetAttributeValue<Guid>("roleid") == role.Id)
            .ToList();

        foreach (var row in rows)
        {
            dataService.Delete(row);
        }
    }
}
