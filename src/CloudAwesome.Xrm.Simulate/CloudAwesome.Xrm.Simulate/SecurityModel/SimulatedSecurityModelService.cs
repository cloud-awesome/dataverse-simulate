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
        return this;
    }

    public SimulatedSecurityModelService WithRole(string name, Action<SimulatedSecurityRole>? configure = null)
    {
        _securityModel.WithRole(name, configure);
        return this;
    }

    public SimulatedSecurityModelService ImportRoleXml(
        string xmlPath,
        string? roleName = null,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        _securityModel.ImportRoleXml(xmlPath, roleName, logicalNameOverrides);
        return this;
    }

    public SimulatedSecurityModelService ImportRoleXml(
        IEnumerable<string> xmlPaths,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        _securityModel.ImportRoleXml(xmlPaths, logicalNameOverrides);
        return this;
    }

    public SimulatedSecurityModelService ImportRoleXmlDirectory(
        string directoryPath,
        string searchPattern = "*.xml",
        bool recursive = false,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        _securityModel.ImportRoleXmlDirectory(directoryPath, searchPattern, recursive, logicalNameOverrides);
        return this;
    }

    public SimulatedSecurityModelService AssignRoleToUser(string roleName, Guid userId)
    {
        _securityModel.AssignRoleToUser(roleName, userId);
        return this;
    }

    public SimulatedSecurityModelService AssignRoleToTeam(string roleName, Guid teamId)
    {
        _securityModel.AssignRoleToTeam(roleName, teamId);
        return this;
    }

    public SimulatedSecurityModelService AssignRole(string roleName, EntityReference principal)
    {
        _securityModel.AssignRole(roleName, principal);
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
}
