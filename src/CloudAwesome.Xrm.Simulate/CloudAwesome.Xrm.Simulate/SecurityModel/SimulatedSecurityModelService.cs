using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedSecurityModelService
{
    private readonly MockedEntityDataService? _dataService;
	private readonly ISimulatorOptions _options;

	private SimulatedSecurityModel _securityModel
	{
		get => _options.SimulatedSecurityModel as SimulatedSecurityModel
		       ?? throw new InvalidOperationException("Security is disabled or the configured model is unsupported.");
		set => _options.SimulatedSecurityModel = value;
	}

    internal SimulatedSecurityModelService(
        MockedEntityDataService? dataService,
        ISimulatorOptions options)
    {
        _dataService = dataService;
		_options = options;

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

    /// <summary>The existing mutable security graph; no configuration copy is created.</summary>
    public SimulatedSecurityModel Model => _securityModel;

    /// <summary>Sets the existing policy for tables without effective permission entries.</summary>
    /// <param name="ignoreMissingEntities">True preserves permissive missing-table behavior; false denies unlisted tables.</param>
    /// <returns>This existing security facade for further configuration.</returns>
    public SimulatedSecurityModelService WithMissingEntityPolicy(bool ignoreMissingEntities)
    {
        _securityModel.IgnoreMissingEntities = ignoreMissingEntities;
        return this;
    }

    /// <summary>Adds an existing global entity-permission entry by reference.</summary>
    /// <param name="permission">The legacy entity permission.</param>
    /// <returns>This security facade for further configuration.</returns>
    public SimulatedSecurityModelService WithEntityPermission(IEntityPermission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        _securityModel.EntityPermissions.Add(permission);
        return this;
    }

    /// <summary>Defines a security business unit and seeds its rows when used on a live simulation.</summary>
    /// <param name="id">The non-empty definition id.</param>
    /// <param name="name">The definition name.</param>
    /// <param name="parentBusinessUnitId">The parent unit, or null for the root.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService WithBusinessUnit(Guid id, string name, Guid? parentBusinessUnitId = null)
    {
        _securityModel.WithBusinessUnit(id, name, parentBusinessUnitId);
        if (_dataService is null) return this;
        _dataService.Upsert(_securityModel.BusinessUnits.Single(x => x.Id == id).ToEntity());
        foreach (var team in _securityModel.Teams.Where(x => x.BusinessUnitId == id))
        {
            _dataService.Upsert(team.ToEntity());
        }

        UpsertRoleEntities();
        return this;
    }

    /// <summary>Defines a graph user without switching the authenticated connection.</summary>
    /// <param name="id">The non-empty definition id.</param>
    /// <param name="businessUnitId">The containing business unit.</param>
    /// <param name="fullName">An optional display name.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService WithUser(Guid id, Guid businessUnitId, string? fullName = null)
    {
        _securityModel.WithUser(id, businessUnitId, fullName);
        _dataService?.Upsert(_securityModel.Users.Single(x => x.Id == id).ToEntity());
        return this;
    }

    /// <summary>Defines an owner team.</summary>
    /// <param name="id">The non-empty definition id.</param>
    /// <param name="businessUnitId">The containing business unit.</param>
    /// <param name="name">The definition name.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService WithOwnerTeam(Guid id, Guid businessUnitId, string? name = null) =>
        WithTeam(id, businessUnitId, name, SimulatedTeamType.Owner);

    /// <summary>Defines an access team.</summary>
    /// <param name="id">The non-empty definition id.</param>
    /// <param name="businessUnitId">The containing business unit.</param>
    /// <param name="name">The definition name.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService WithAccessTeam(Guid id, Guid businessUnitId, string? name = null) =>
        WithTeam(id, businessUnitId, name, SimulatedTeamType.Access);

    /// <summary>Defines a typed team.</summary>
    /// <param name="id">The non-empty definition id.</param>
    /// <param name="businessUnitId">The containing business unit.</param>
    /// <param name="name">The definition name.</param>
    /// <param name="teamType">Owner or access team.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService WithTeam(
        Guid id,
        Guid businessUnitId,
        string? name = null,
        SimulatedTeamType teamType = SimulatedTeamType.Owner)
    {
        _securityModel.WithTeam(id, businessUnitId, name, teamType);
        _dataService?.Upsert(_securityModel.Teams.Single(x => x.Id == id).ToEntity());
        return this;
    }

    /// <summary>Adds a team membership.</summary>
    /// <param name="teamId">The team id.</param>
    /// <param name="userId">The graph user id.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService WithTeamMember(Guid teamId, Guid userId)
    {
        _securityModel.WithTeamMember(teamId, userId);
        var membership = _securityModel.TeamMemberships.Single(x => x.TeamId == teamId && x.UserId == userId);
        _dataService?.Upsert(SimulatedSecurityModelDataSeeder.CreateTeamMembershipEntity(membership));
        return this;
    }

    /// <summary>Defines a role and its privileges without assigning it.</summary>
    /// <param name="name">The definition name.</param>
    /// <param name="configure">An optional role privilege callback.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService WithRole(string name, Action<SimulatedSecurityRole>? configure = null)
    {
        _securityModel.WithRole(name, configure);
        UpsertRoleEntities();
        return this;
    }

    /// <summary>Imports role definitions from the existing XML contract.</summary>
    /// <param name="xmlPath">The exported role XML path.</param>
    /// <param name="roleName">The defined or imported role name.</param>
    /// <param name="logicalNameOverrides">Optional exported-name overrides.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService ImportRoleXml(
        string xmlPath,
        string? roleName = null,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        _securityModel.ImportRoleXml(xmlPath, roleName, logicalNameOverrides);
        UpsertRoleEntities();
        return this;
    }

    /// <summary>Imports role definitions from the existing XML contract.</summary>
    /// <param name="xmlPaths">Exported role XML paths.</param>
    /// <param name="logicalNameOverrides">Optional exported-name overrides.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService ImportRoleXml(
        IEnumerable<string> xmlPaths,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        _securityModel.ImportRoleXml(xmlPaths, logicalNameOverrides);
        UpsertRoleEntities();
        return this;
    }

    /// <summary>Imports matching role XML files from a directory.</summary>
    /// <param name="directoryPath">The directory containing XML exports.</param>
    /// <param name="searchPattern">The file search pattern.</param>
    /// <param name="recursive">Whether child directories are included.</param>
    /// <param name="logicalNameOverrides">Optional exported-name overrides.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
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

    /// <summary>Explicitly assigns a role to a graph user.</summary>
    /// <param name="roleName">The defined or imported role name.</param>
    /// <param name="userId">The graph user id.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService AssignRoleToUser(string roleName, Guid userId)
    {
        _securityModel.AssignRoleToUser(roleName, userId);
        UpsertRoleAssignmentIfComplete(roleName, new EntityReference("systemuser", userId));
        return this;
    }

    /// <summary>Explicitly assigns a role to an owner team.</summary>
    /// <param name="roleName">The defined or imported role name.</param>
    /// <param name="teamId">The team id.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService AssignRoleToTeam(string roleName, Guid teamId)
    {
        _securityModel.AssignRoleToTeam(roleName, teamId);
        UpsertRoleAssignmentIfComplete(roleName, new EntityReference("team", teamId));
        return this;
    }

    /// <summary>Explicitly assigns a role to a user or team.</summary>
    /// <param name="roleName">The defined or imported role name.</param>
    /// <param name="principal">A user or team reference.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService AssignRole(string roleName, EntityReference principal)
    {
        _securityModel.AssignRole(roleName, principal);
        UpsertRoleAssignmentIfComplete(roleName, principal);
        return this;
    }

    /// <summary>Configures explicit record sharing.</summary>
    /// <param name="target">The shared record reference.</param>
    /// <param name="principal">A user or team reference.</param>
    /// <param name="accessRights">The explicit sharing rights.</param>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService WithPrincipalObjectAccess(
        EntityReference target,
        EntityReference principal,
        AccessRights accessRights)
    {
        _securityModel.WithPrincipalObjectAccess(target, principal, accessRights);
        return this;
    }

    /// <summary>Validates the graph and seeds rows when attached to a live simulation.</summary>
    /// <returns>This security facade for further configuration.</returns>
    /// <remarks>Uses the existing model and validation rules. Live changes apply immediately without rollback; initial setup is seeded by the simulator initializer.</remarks>
    public SimulatedSecurityModelService Validate()
    {
        _securityModel.Validate();
        if (_dataService is not null) SimulatedSecurityModelDataSeeder.Seed(_dataService, _securityModel);
        return this;
    }

    private void UpsertRoleEntities()
    {
        if (_dataService is null) return;
        var rootBusinessUnitId = SimulatedSecurityModelDataSeeder.ResolveRootBusinessUnitId(
            _dataService,
            _securityModel);

        foreach (var role in _securityModel.Roles)
        {
			foreach (var businessUnit in SimulatedSecurityModelDataSeeder.GetRoleBusinessUnits(_dataService,
				         _securityModel))
            {
                _dataService.Upsert(SimulatedSecurityModelDataSeeder.CreateRoleEntity(
                    role,
                    businessUnit,
                    rootBusinessUnitId));
            }
        }
    }

    private void UpsertRoleAssignmentIfComplete(string roleName, EntityReference principal)
    {
        if (_dataService is null) return;
        if (!_securityModel.Roles.Any(x => string.Equals(x.Name, roleName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (FindPrincipalBusinessUnitId(principal) is null)
        {
            return;
        }

        var assignment = _securityModel.RoleAssignments.Last(x =>
            string.Equals(x.RoleName, roleName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Principal.LogicalName, principal.LogicalName, StringComparison.OrdinalIgnoreCase) &&
            x.Principal.Id == principal.Id);

        _dataService.Upsert(SimulatedSecurityModelDataSeeder.CreateRoleAssignmentEntity(
            assignment,
            _securityModel,
            SimulatedSecurityModelDataSeeder.ResolveRootBusinessUnitId(_dataService, _securityModel)));
    }

    private Guid? FindPrincipalBusinessUnitId(EntityReference principal)
    {
        return principal.LogicalName switch
        {
            "systemuser" => _securityModel.Users.SingleOrDefault(x => x.Id == principal.Id)?.BusinessUnitId,
            "team" => _securityModel.Teams.SingleOrDefault(x => x.Id == principal.Id)?.BusinessUnitId,
            _ => null
        };
    }
}
