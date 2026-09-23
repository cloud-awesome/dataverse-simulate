using CloudAwesome.Xrm.Simulate.DataServices;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

internal static class SimulatedSecurityModelDataSeeder
{
    internal static void Seed(MockedEntityDataService dataService, SimulatedSecurityModel securityModel)
    {
        foreach (var businessUnit in securityModel.BusinessUnits)
        {
            dataService.Upsert(businessUnit.ToEntity());
        }

        foreach (var user in securityModel.Users)
        {
            dataService.Upsert(user.ToEntity());
        }

        foreach (var team in securityModel.Teams)
        {
            dataService.Upsert(team.ToEntity());
        }

        var rootBusinessUnitId = ResolveRootBusinessUnitId(dataService, securityModel);

        foreach (var role in securityModel.Roles)
        {
            foreach (var businessUnit in GetRoleBusinessUnits(dataService, securityModel))
            {
                dataService.Upsert(CreateRoleEntity(role, businessUnit, rootBusinessUnitId));
            }
        }

        foreach (var assignment in securityModel.RoleAssignments)
        {
            dataService.Upsert(CreateRoleAssignmentEntity(assignment, securityModel, rootBusinessUnitId));
        }

        foreach (var membership in securityModel.TeamMemberships)
        {
            dataService.Upsert(CreateTeamMembershipEntity(membership));
        }
    }

    internal static Entity CreateRoleEntity(
        SimulatedSecurityRole role,
        SimulatedBusinessUnit businessUnit,
        Guid rootBusinessUnitId)
    {
        return role.ToEntity(
            businessUnit.Id,
            rootBusinessUnitId,
            businessUnit.ParentBusinessUnitId);
    }

    internal static Entity CreateRoleAssignmentEntity(
        SimulatedRoleAssignment assignment,
        SimulatedSecurityModel securityModel,
        Guid rootBusinessUnitId)
    {
        var role = securityModel.Roles.Single(x =>
            string.Equals(x.Name, assignment.RoleName, StringComparison.OrdinalIgnoreCase));
        var principalBusinessUnitId = securityModel.GetPrincipalBusinessUnitId(assignment.Principal)
            ?? throw new SimulatedSecurityModelException(
                $"Role assignment principal '{assignment.Principal.LogicalName}:{assignment.Principal.Id}' does not have a business unit.");
        var roleId = role.GetRoleId(principalBusinessUnitId, rootBusinessUnitId);

        return assignment.Principal.LogicalName switch
        {
            "systemuser" => new Entity("systemuserroles", assignment.Id)
            {
                ["systemuserroleid"] = assignment.Id,
                ["systemuserid"] = assignment.Principal.Id,
                ["roleid"] = roleId
            },
            "team" => new Entity("teamroles", assignment.Id)
            {
                ["teamroleid"] = assignment.Id,
                ["teamid"] = assignment.Principal.Id,
                ["roleid"] = roleId
            },
            _ => throw new SimulatedSecurityModelException(
                $"Role assignment references unsupported principal type '{assignment.Principal.LogicalName}'.")
        };
    }

    internal static Entity CreateTeamMembershipEntity(SimulatedTeamMembership membership)
    {
        return new Entity("teammembership", membership.Id)
        {
            ["teammembershipid"] = membership.Id,
            ["teamid"] = membership.TeamId,
            ["systemuserid"] = membership.UserId
        };
    }

    internal static Guid ResolveRootBusinessUnitId(
        MockedEntityDataService dataService,
        SimulatedSecurityModel securityModel)
    {
        return securityModel.BusinessUnits.FirstOrDefault(x => x.ParentBusinessUnitId is null)?.Id
               ?? securityModel.BusinessUnits.FirstOrDefault()?.Id
               ?? dataService.BusinessUnit.Id;
    }

    internal static IReadOnlyList<SimulatedBusinessUnit> GetRoleBusinessUnits(
        MockedEntityDataService dataService,
        SimulatedSecurityModel securityModel)
    {
        return securityModel.BusinessUnits.Count > 0
            ? securityModel.BusinessUnits
            : [new SimulatedBusinessUnit(dataService.BusinessUnit.Id, dataService.BusinessUnit.Name ?? "Business Unit")];
    }
}
