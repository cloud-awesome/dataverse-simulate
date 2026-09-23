using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedSecurityModelService
{
    private readonly MockedEntityDataService _dataService;
    private readonly SimulatedSecurityModel _securityModel;

    internal SimulatedSecurityModelService(
        MockedEntityDataService dataService,
        ISimulatorOptions options)
    {
        _dataService = dataService;

        if (options.SimulatedSecurityModel is null)
        {
            _securityModel = new SimulatedSecurityModel();
            options.SimulatedSecurityModel = _securityModel;
        }
        else if (options.SimulatedSecurityModel is SimulatedSecurityModel simulatedSecurityModel)
        {
            _securityModel = simulatedSecurityModel;
        }
        else
        {
            throw new InvalidOperationException(
                "SecurityModel() requires SimulatorOptions.SimulatedSecurityModel to be null or a SimulatedSecurityModel.");
        }
    }

    public SimulatedSecurityModel Model => _securityModel;

    public SimulatedSecurityModelService WithBusinessUnit(Guid id, string name, Guid? parentBusinessUnitId = null)
    {
        _securityModel.WithBusinessUnit(id, name, parentBusinessUnitId);
        _dataService.Upsert(_securityModel.BusinessUnits.Single(x => x.Id == id).ToEntity());
        return this;
    }

    public SimulatedSecurityModelService WithUser(Guid id, Guid businessUnitId, string? fullName = null)
    {
        _securityModel.WithUser(id, businessUnitId, fullName);
        _dataService.Upsert(_securityModel.Users.Single(x => x.Id == id).ToEntity());
        return this;
    }

    public SimulatedSecurityModelService WithOwnerTeam(Guid id, Guid businessUnitId, string? name = null) =>
        WithTeam(id, businessUnitId, name, SimulatedTeamType.Owner);

    public SimulatedSecurityModelService WithAccessTeam(Guid id, Guid businessUnitId, string? name = null) =>
        WithTeam(id, businessUnitId, name, SimulatedTeamType.Access);

    public SimulatedSecurityModelService WithTeam(
        Guid id,
        Guid businessUnitId,
        string? name = null,
        SimulatedTeamType teamType = SimulatedTeamType.Owner)
    {
        _securityModel.WithTeam(id, businessUnitId, name, teamType);
        _dataService.Upsert(_securityModel.Teams.Single(x => x.Id == id).ToEntity());
        return this;
    }

    public SimulatedSecurityModelService WithTeamMember(Guid teamId, Guid userId)
    {
        _securityModel.WithTeamMember(teamId, userId);
        var membership = _securityModel.TeamMemberships.Single(x => x.TeamId == teamId && x.UserId == userId);
        _dataService.Upsert(SimulatedSecurityModelDataSeeder.CreateTeamMembershipEntity(membership));
        return this;
    }

    public SimulatedSecurityModelService WithRole(string name, Action<SimulatedSecurityRole>? configure = null)
    {
        _securityModel.WithRole(name, configure);
        var role = _securityModel.Roles.Single(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        _dataService.Upsert(SimulatedSecurityModelDataSeeder.CreateRoleEntity(
            role,
            SimulatedSecurityModelDataSeeder.ResolveDefaultBusinessUnitId(_dataService, _securityModel)));
        return this;
    }

    public SimulatedSecurityModelService ImportRoleXml(
        string xmlPath,
        string? roleName = null,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        _securityModel.ImportRoleXml(xmlPath, roleName, logicalNameOverrides);
        UpsertRoleEntities();
        return this;
    }

    public SimulatedSecurityModelService ImportRoleXml(
        IEnumerable<string> xmlPaths,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        _securityModel.ImportRoleXml(xmlPaths, logicalNameOverrides);
        UpsertRoleEntities();
        return this;
    }

    public SimulatedSecurityModelService ImportRoleXmlDirectory(
        string directoryPath,
        string searchPattern = "*.xml",
        bool recursive = false,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        _securityModel.ImportRoleXmlDirectory(directoryPath, searchPattern, recursive, logicalNameOverrides);
        UpsertRoleEntities();
        return this;
    }

    public SimulatedSecurityModelService AssignRoleToUser(string roleName, Guid userId)
    {
        _securityModel.AssignRoleToUser(roleName, userId);
        UpsertRoleAssignmentIfComplete(roleName, new EntityReference("systemuser", userId));
        return this;
    }

    public SimulatedSecurityModelService AssignRoleToTeam(string roleName, Guid teamId)
    {
        _securityModel.AssignRoleToTeam(roleName, teamId);
        UpsertRoleAssignmentIfComplete(roleName, new EntityReference("team", teamId));
        return this;
    }

    public SimulatedSecurityModelService AssignRole(string roleName, EntityReference principal)
    {
        _securityModel.AssignRole(roleName, principal);
        UpsertRoleAssignmentIfComplete(roleName, principal);
        return this;
    }

    public SimulatedSecurityModelService WithPrincipalObjectAccess(
        EntityReference target,
        EntityReference principal,
        AccessRights accessRights)
    {
        _securityModel.WithPrincipalObjectAccess(target, principal, accessRights);
        return this;
    }

    public SimulatedSecurityModelService Validate()
    {
        _securityModel.Validate();
        SimulatedSecurityModelDataSeeder.Seed(_dataService, _securityModel);
        return this;
    }

    private void UpsertRoleEntities()
    {
        var defaultBusinessUnitId = SimulatedSecurityModelDataSeeder.ResolveDefaultBusinessUnitId(
            _dataService,
            _securityModel);

        foreach (var role in _securityModel.Roles)
        {
            _dataService.Upsert(SimulatedSecurityModelDataSeeder.CreateRoleEntity(role, defaultBusinessUnitId));
        }
    }

    private void UpsertRoleAssignmentIfComplete(string roleName, EntityReference principal)
    {
        if (!_securityModel.Roles.Any(x => string.Equals(x.Name, roleName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var assignment = _securityModel.RoleAssignments.Last(x =>
            string.Equals(x.RoleName, roleName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Principal.LogicalName, principal.LogicalName, StringComparison.OrdinalIgnoreCase) &&
            x.Principal.Id == principal.Id);

        _dataService.Upsert(SimulatedSecurityModelDataSeeder.CreateRoleAssignmentEntity(assignment, _securityModel));
    }
}
