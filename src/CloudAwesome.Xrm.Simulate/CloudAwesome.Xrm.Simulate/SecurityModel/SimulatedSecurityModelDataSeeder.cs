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

        foreach (var role in securityModel.Roles)
        {
            dataService.Upsert(CreateRoleEntity(role, ResolveDefaultBusinessUnitId(dataService, securityModel)));
        }

        foreach (var assignment in securityModel.RoleAssignments)
        {
            dataService.Upsert(CreateRoleAssignmentEntity(assignment, securityModel));
        }

        foreach (var membership in securityModel.TeamMemberships)
        {
            dataService.Upsert(CreateTeamMembershipEntity(membership));
        }
    }

    internal static Entity CreateRoleEntity(
        SimulatedSecurityRole role,
        Guid defaultBusinessUnitId)
    {
        return role.ToEntity(defaultBusinessUnitId);
    }

    internal static Entity CreateRoleAssignmentEntity(
        SimulatedRoleAssignment assignment,
        SimulatedSecurityModel securityModel)
    {
        var role = securityModel.Roles.Single(x =>
            string.Equals(x.Name, assignment.RoleName, StringComparison.OrdinalIgnoreCase));

        return assignment.Principal.LogicalName switch
        {
            "systemuser" => new Entity("systemuserroles", assignment.Id)
            {
                ["systemuserroleid"] = assignment.Id,
                ["systemuserid"] = new EntityReference("systemuser", assignment.Principal.Id),
                ["roleid"] = new EntityReference("role", role.Id)
            },
            "team" => new Entity("teamroles", assignment.Id)
            {
                ["teamroleid"] = assignment.Id,
                ["teamid"] = new EntityReference("team", assignment.Principal.Id),
                ["roleid"] = new EntityReference("role", role.Id)
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
            ["teamid"] = new EntityReference("team", membership.TeamId),
            ["systemuserid"] = new EntityReference("systemuser", membership.UserId)
        };
    }

    internal static Guid ResolveDefaultBusinessUnitId(
        MockedEntityDataService dataService,
        SimulatedSecurityModel securityModel)
    {
        return securityModel.BusinessUnits.FirstOrDefault(x => x.ParentBusinessUnitId is null)?.Id
               ?? securityModel.BusinessUnits.FirstOrDefault()?.Id
               ?? dataService.BusinessUnit.Id;
    }
}
