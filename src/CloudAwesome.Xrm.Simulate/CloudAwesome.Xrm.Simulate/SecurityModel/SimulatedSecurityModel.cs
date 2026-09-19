using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public class SimulatedSecurityModel : ISecurityModel
{
    private Dictionary<Guid, SimulatedBusinessUnit> _businessUnitsById = [];
    private Dictionary<Guid, SimulatedUser> _usersById = [];
    private Dictionary<Guid, SimulatedTeam> _teamsById = [];
    private Dictionary<string, SimulatedSecurityRole> _rolesByName = new(StringComparer.OrdinalIgnoreCase);

    public bool IgnoreMissingEntities { get; set; } = true;
    public List<IEntityPermission> EntityPermissions { get; set; } = [];
    public List<SimulatedBusinessUnit> BusinessUnits { get; } = [];
    public List<SimulatedUser> Users { get; } = [];
    public List<SimulatedTeam> Teams { get; } = [];
    public List<SimulatedSecurityRole> Roles { get; } = [];
    public List<SimulatedRoleAssignment> RoleAssignments { get; } = [];
    public List<SimulatedTeamMembership> TeamMemberships { get; } = [];
    public List<SimulatedPrincipalObjectAccess> PrincipalObjectAccesses { get; } = [];

    /// <summary>
    /// Empty constructor. Manually define all entity permissions required for the sut.
    /// </summary>
    public SimulatedSecurityModel()
    {
    }

    public static SimulatedSecurityModel Create() => new();

    /// <summary>
    /// Generate the SimulatedSecurityModel from an exported dataverse security role XML file.
    /// Use the PAC Cli to export and extract security roles, which generates XML files in the format expected.
    /// </summary>
    /// <param name="xmlPath">File path to the extract security role.</param>
    /// <param name="logicalNameOverrides">
    /// If necessary, provide any entity logical name overrides to match what is in your simulated data store.
    /// The key should the name as seen in the exported role, the value is the target.
    /// </param>
    public SimulatedSecurityModel(
        string xmlPath,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        var model = SecurityRoleParser.GenerateFromExportedSecurityRoleXml(xmlPath, logicalNameOverrides);
        EntityPermissions = model.EntityPermissions;
    }

    /// <summary>
    /// Generate the SimulatedSecurityModel from a list exported dataverse security role XML files.
    /// Use the PAC Cli to export and extract security roles, which generates XML files in the format expected.
    /// </summary>
    /// <param name="xmlPaths">
    /// A list of file paths to parse and merge.
    /// If an entity permission is referenced multiple times, the highest permission is taken.
    /// </param>
    /// <param name="logicalNameOverrides">
    /// If necessary, provide any entity logical name overrides to match what is in your simulated data store.
    /// The key should the name as seen in the exported role, the value is the target.
    /// </param>
    public SimulatedSecurityModel(
        IEnumerable<string> xmlPaths,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        var model = SecurityRoleParser.GenerateFromExportedSecurityRoleXml(xmlPaths, logicalNameOverrides);
        EntityPermissions = model.EntityPermissions;
    }

    /// <summary>
    /// Generate the SimulatedSecurityModel from any exported dataverse security role XML files in a given directory.
    /// Use the PAC Cli to export and extract security roles, which generates XML files in the format expected.
    /// </summary>
    /// <param name="directoryPath">Path to read the extracted XML files.</param>
    /// <param name="searchPattern">Optional. Defaults to *.xml, but a more granular filter can be provided if required.</param>
    /// <param name="recursive">Check child directories recursively.</param>
    /// <param name="logicalNameOverrides">
    /// If necessary, provide any entity logical name overrides to match what is in your simulated data store.
    /// The key should the name as seen in the exported role, the value is the target.
    /// </param>
    public SimulatedSecurityModel(
        string directoryPath,
        bool recursive,
        string searchPattern = "*.xml",
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        var model = SecurityRoleParser.GenerateSecurityModelFromDirectory(
            directoryPath,
            searchPattern,
            recursive,
            logicalNameOverrides);

        EntityPermissions = model.EntityPermissions;
    }

    public SimulatedSecurityModel WithBusinessUnit(Guid id, string name, Guid? parentBusinessUnitId = null)
    {
        EnsureIdIsProvided(id, nameof(id));
        EnsureUnique(id, BusinessUnits.Select(x => x.Id), "business unit");

        BusinessUnits.Add(new SimulatedBusinessUnit(id, name, parentBusinessUnitId));
        return this;
    }

    public SimulatedSecurityModel WithUser(Guid id, Guid businessUnitId, string? fullName = null)
    {
        EnsureIdIsProvided(id, nameof(id));
        EnsureIdIsProvided(businessUnitId, nameof(businessUnitId));
        EnsureUnique(id, Users.Select(x => x.Id), "user");

        Users.Add(new SimulatedUser(id, businessUnitId, fullName));
        return this;
    }

    public SimulatedSecurityModel WithOwnerTeam(Guid id, Guid businessUnitId, string? name = null) =>
        WithTeam(id, businessUnitId, name, SimulatedTeamType.Owner);

    public SimulatedSecurityModel WithAccessTeam(Guid id, Guid businessUnitId, string? name = null) =>
        WithTeam(id, businessUnitId, name, SimulatedTeamType.Access);

    public SimulatedSecurityModel WithTeam(
        Guid id,
        Guid businessUnitId,
        string? name = null,
        SimulatedTeamType teamType = SimulatedTeamType.Owner)
    {
        EnsureIdIsProvided(id, nameof(id));
        EnsureIdIsProvided(businessUnitId, nameof(businessUnitId));
        EnsureUnique(id, Teams.Select(x => x.Id), "team");

        Teams.Add(new SimulatedTeam(id, businessUnitId, name, teamType));
        return this;
    }

    public SimulatedSecurityModel WithRole(string name, Action<SimulatedSecurityRole>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Role name must be provided.", nameof(name));
        if (Roles.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new SimulatedSecurityModelException($"A role named '{name}' already exists.");

        var role = new SimulatedSecurityRole(name);
        configure?.Invoke(role);
        Roles.Add(role);

        return this;
    }

    public SimulatedSecurityModel ImportRoleXml(
        string xmlPath,
        string? roleName = null,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        return AddRole(SecurityRoleParser.ParseRoleXml(xmlPath, roleName, logicalNameOverrides));
    }

    public SimulatedSecurityModel ImportRoleXml(
        IEnumerable<string> xmlPaths,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        if (xmlPaths is null) throw new ArgumentNullException(nameof(xmlPaths));

        foreach (var xmlPath in xmlPaths)
        {
            ImportRoleXml(xmlPath, logicalNameOverrides: logicalNameOverrides);
        }

        return this;
    }

    public SimulatedSecurityModel ImportRoleXmlDirectory(
        string directoryPath,
        string searchPattern = "*.xml",
        bool recursive = false,
        IDictionary<string, string>? logicalNameOverrides = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory must be provided.", nameof(directoryPath));
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException(directoryPath);

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return ImportRoleXml(Directory.EnumerateFiles(directoryPath, searchPattern, option), logicalNameOverrides);
    }

    public SimulatedSecurityModel AddRole(SimulatedSecurityRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (string.IsNullOrWhiteSpace(role.Name))
            throw new ArgumentException("Role name must be provided.", nameof(role));
        if (Roles.Any(x => string.Equals(x.Name, role.Name, StringComparison.OrdinalIgnoreCase)))
            throw new SimulatedSecurityModelException($"A role named '{role.Name}' already exists.");

        Roles.Add(role);
        return this;
    }

    public SimulatedSecurityModel AssignRoleToUser(string roleName, Guid userId) =>
        AssignRole(roleName, new EntityReference("systemuser", userId));

    public SimulatedSecurityModel AssignRoleToTeam(string roleName, Guid teamId) =>
        AssignRole(roleName, new EntityReference("team", teamId));

    public SimulatedSecurityModel AssignRole(string roleName, EntityReference principal)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("Role name must be provided.", nameof(roleName));
        if (principal.Id == Guid.Empty)
            throw new ArgumentException("Principal id must be provided.", nameof(principal));

        RoleAssignments.Add(new SimulatedRoleAssignment(roleName, principal));
        return this;
    }

    public SimulatedSecurityModel WithTeamMember(Guid teamId, Guid userId)
    {
        return AddTeamMember(teamId, userId);
    }

    public SimulatedSecurityModel WithPrincipalObjectAccess(
        EntityReference target,
        EntityReference principal,
        AccessRights accessRights)
    {
        return GrantAccess(target, principal, accessRights);
    }

    public SimulatedSecurityModel AddTeamMember(Guid teamId, Guid userId)
    {
        EnsureIdIsProvided(teamId, nameof(teamId));
        EnsureIdIsProvided(userId, nameof(userId));

        if (TeamMemberships.Any(x => x.TeamId == teamId && x.UserId == userId))
            return this;

        TeamMemberships.Add(new SimulatedTeamMembership(teamId, userId));

        return this;
    }

    public SimulatedSecurityModel RemoveTeamMember(Guid teamId, Guid userId)
    {
        EnsureIdIsProvided(teamId, nameof(teamId));
        EnsureIdIsProvided(userId, nameof(userId));

        var memberships = TeamMemberships
            .Where(x => x.TeamId == teamId && x.UserId == userId)
            .ToList();

        foreach (var membership in memberships)
        {
            TeamMemberships.Remove(membership);
        }

        return this;
    }

    public SimulatedSecurityModel GrantAccess(
        EntityReference target,
        EntityReference principal,
        AccessRights accessRights)
    {
        ValidateAccessArguments(target, principal);

        var existingAccess = FindPrincipalObjectAccess(target, principal);
        if (existingAccess is null)
        {
            PrincipalObjectAccesses.Add(new SimulatedPrincipalObjectAccess(target, principal, accessRights));
        }
        else
        {
            existingAccess.AccessRights |= accessRights;
        }

        return this;
    }

    public SimulatedSecurityModel ModifyAccess(
        EntityReference target,
        EntityReference principal,
        AccessRights accessRights)
    {
        ValidateAccessArguments(target, principal);

        var existingAccess = FindPrincipalObjectAccess(target, principal);
        if (existingAccess is null)
        {
            PrincipalObjectAccesses.Add(new SimulatedPrincipalObjectAccess(target, principal, accessRights));
        }
        else
        {
            existingAccess.AccessRights = accessRights;
        }

        return this;
    }

    public SimulatedSecurityModel RevokeAccess(EntityReference target, EntityReference principal)
    {
        ValidateAccessArguments(target, principal);

        var existingAccesses = PrincipalObjectAccesses
            .Where(x => PrincipalMatches(x.Target, target) && PrincipalMatches(x.Principal, principal))
            .ToList();

        foreach (var access in existingAccesses)
        {
            PrincipalObjectAccesses.Remove(access);
        }

        return this;
    }

    public AccessRights GetPrincipalAccess(EntityReference target, EntityReference principal)
    {
        ValidateAccessArguments(target, principal);
        Validate();

        return GetPrincipalObjectAccesses(target, principal)
            .Aggregate(default(AccessRights), (rights, access) => rights | access.AccessRights);
    }

    public IReadOnlyList<SimulatedPrincipalObjectAccess> GetSharedPrincipalsAndAccess(EntityReference target)
    {
        if (target.Id == Guid.Empty || string.IsNullOrWhiteSpace(target.LogicalName))
            throw new ArgumentException("Target must include a logical name and id.", nameof(target));

        Validate();

        return PrincipalObjectAccesses
            .Where(x => PrincipalMatches(x.Target, target))
            .ToList();
    }

    public IReadOnlyList<Entity> CreateBusinessUnitEntities()
    {
        Validate();
        return BusinessUnits.Select(x => x.ToEntity()).ToList();
    }

    public IReadOnlyList<Entity> CreateUserEntities()
    {
        Validate();
        return Users.Select(x => x.ToEntity()).ToList();
    }

    public IReadOnlyList<Entity> CreateTeamEntities()
    {
        Validate();
        return Teams.Select(x => x.ToEntity()).ToList();
    }

    public IReadOnlyList<Entity> CreatePrincipalEntities()
    {
        Validate();
        return BusinessUnits
            .Select(x => x.ToEntity())
            .Concat(Users.Select(x => x.ToEntity()))
            .Concat(Teams.Select(x => x.ToEntity()))
            .ToList();
    }

    public SimulatedSecurityModel Validate()
    {
        _businessUnitsById = BuildUniqueIdIndex(BusinessUnits, x => x.Id, "business unit");
        _usersById = BuildUniqueIdIndex(Users, x => x.Id, "user");
        _teamsById = BuildUniqueIdIndex(Teams, x => x.Id, "team");
        _rolesByName = BuildUniqueNameIndex(Roles);

        ValidateBusinessUnits();
        ValidateUsers();
        ValidateTeams();
        ValidateRoleAssignments();
        ValidateTeamMemberships();
        ValidatePrincipalObjectAccesses();

        return this;
    }

    public bool TryGetBusinessUnit(Guid id, out SimulatedBusinessUnit businessUnit)
    {
        Validate();
        return _businessUnitsById.TryGetValue(id, out businessUnit!);
    }

    public bool TryGetUser(Guid id, out SimulatedUser user)
    {
        Validate();
        return _usersById.TryGetValue(id, out user!);
    }

    public bool TryGetTeam(Guid id, out SimulatedTeam team)
    {
        Validate();
        return _teamsById.TryGetValue(id, out team!);
    }

    public bool IsBusinessUnitDescendantOrSelf(Guid businessUnitId, Guid ancestorBusinessUnitId)
    {
        Validate();

        var visited = new HashSet<Guid>();
        var currentId = businessUnitId;

        while (true)
        {
            if (currentId == ancestorBusinessUnitId)
                return true;

            if (!visited.Add(currentId) || !_businessUnitsById.TryGetValue(currentId, out var current))
                return false;

            if (current.ParentBusinessUnitId is not { } parentId)
                return false;

            currentId = parentId;
        }
    }

    public IReadOnlyList<Guid> GetTeamIdsForUser(Guid userId)
    {
        Validate();

        return TeamMemberships
            .Where(x => x.UserId == userId)
            .Select(x => x.TeamId)
            .Distinct()
            .ToList();
    }

    public IReadOnlyList<EntityPermission> GetEffectiveEntityPermissionsForUser(Guid userId)
    {
        Validate();

        var permissionsByEntity = new Dictionary<string, EntityPermission>(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in EntityPermissions)
        {
            MergePermission(permissionsByEntity, permission);
        }

        foreach (var role in GetRolesAssignedToPrincipal(new EntityReference("systemuser", userId)))
        {
            MergeRole(permissionsByEntity, role);
        }

        foreach (var teamId in GetOwnerTeamIdsForUser(userId))
        {
            foreach (var role in GetRolesAssignedToPrincipal(new EntityReference("team", teamId)))
            {
                MergeRole(permissionsByEntity, role);
            }
        }

        return permissionsByEntity.Values.ToList();
    }

    public IReadOnlyList<EntityPermission> GetEffectiveEntityPermissionsForPrincipal(EntityReference principal)
    {
        Validate();

        return principal.LogicalName switch
        {
            "systemuser" => GetEffectiveEntityPermissionsForUser(principal.Id),
            "team" => GetEffectiveEntityPermissionsForTeam(principal.Id),
            _ => []
        };
    }

    public EntityPermission? GetEffectiveEntityPermissionForUser(Guid userId, string entityLogicalName)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name must be provided.", nameof(entityLogicalName));

        return GetEffectiveEntityPermissionsForUser(userId)
            .SingleOrDefault(x => string.Equals(
                x.LogicalName,
                entityLogicalName,
                StringComparison.OrdinalIgnoreCase));
    }

    public EntityPermission? GetEffectiveEntityPermissionForPrincipal(EntityReference principal, string entityLogicalName)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name must be provided.", nameof(entityLogicalName));

        return GetEffectiveEntityPermissionsForPrincipal(principal)
            .SingleOrDefault(x => string.Equals(
                x.LogicalName,
                entityLogicalName,
                StringComparison.OrdinalIgnoreCase));
    }

    public Guid? GetPrincipalBusinessUnitId(EntityReference principal)
    {
        Validate();

        return principal.LogicalName switch
        {
            "systemuser" when _usersById.TryGetValue(principal.Id, out var user) => user.BusinessUnitId,
            "team" when _teamsById.TryGetValue(principal.Id, out var team) => team.BusinessUnitId,
            _ => null
        };
    }

    public IReadOnlyList<SimulatedPrincipalObjectAccess> GetPrincipalObjectAccesses(
        EntityReference target,
        EntityReference principal)
    {
        Validate();

        return PrincipalObjectAccesses
            .Where(x => PrincipalMatches(x.Target, target) && PrincipalMatches(x.Principal, principal))
            .ToList();
    }

    internal SecurityModelSnapshot CreateSnapshot()
    {
        return new SecurityModelSnapshot(
            TeamMemberships
                .Select(membership => new SimulatedTeamMembership(membership.TeamId, membership.UserId))
                .ToList(),
            PrincipalObjectAccesses
                .Select(access => new SimulatedPrincipalObjectAccess(
                    CloneReference(access.Target),
                    CloneReference(access.Principal),
                    access.AccessRights))
                .ToList());
    }

    internal void RestoreSnapshot(SecurityModelSnapshot snapshot)
    {
        TeamMemberships.Clear();
        TeamMemberships.AddRange(snapshot.TeamMemberships.Select(membership =>
            new SimulatedTeamMembership(membership.TeamId, membership.UserId)));

        PrincipalObjectAccesses.Clear();
        PrincipalObjectAccesses.AddRange(snapshot.PrincipalObjectAccesses.Select(access =>
            new SimulatedPrincipalObjectAccess(
                CloneReference(access.Target),
                CloneReference(access.Principal),
                access.AccessRights)));
    }

    internal sealed record SecurityModelSnapshot(
        List<SimulatedTeamMembership> TeamMemberships,
        List<SimulatedPrincipalObjectAccess> PrincipalObjectAccesses);

    private IEnumerable<Guid> GetOwnerTeamIdsForUser(Guid userId)
    {
        return TeamMemberships
            .Where(x => x.UserId == userId)
            .Select(x => x.TeamId)
            .Where(teamId => _teamsById.TryGetValue(teamId, out var team) && team.TeamType == SimulatedTeamType.Owner)
            .Distinct();
    }

    private IReadOnlyList<EntityPermission> GetEffectiveEntityPermissionsForTeam(Guid teamId)
    {
        var permissionsByEntity = new Dictionary<string, EntityPermission>(StringComparer.OrdinalIgnoreCase);

        if (!_teamsById.TryGetValue(teamId, out var team) || team.TeamType != SimulatedTeamType.Owner)
            return [];

        foreach (var role in GetRolesAssignedToPrincipal(new EntityReference("team", teamId)))
        {
            MergeRole(permissionsByEntity, role);
        }

        return permissionsByEntity.Values.ToList();
    }

    private IEnumerable<SimulatedSecurityRole> GetRolesAssignedToPrincipal(EntityReference principal)
    {
        return RoleAssignments
            .Where(x => PrincipalMatches(x.Principal, principal))
            .Select(x => _rolesByName[x.RoleName]);
    }

    private void ValidateBusinessUnits()
    {
        foreach (var businessUnit in BusinessUnits)
        {
            EnsureIdIsProvided(businessUnit.Id, nameof(SimulatedBusinessUnit.Id));

            if (businessUnit.ParentBusinessUnitId is { } parentBusinessUnitId &&
                !_businessUnitsById.ContainsKey(parentBusinessUnitId))
            {
                throw new SimulatedSecurityModelException(
                    $"Business unit '{businessUnit.Id}' references missing parent business unit '{parentBusinessUnitId}'.");
            }
        }

        foreach (var businessUnit in BusinessUnits)
        {
            ValidateBusinessUnitHasNoCycle(businessUnit);
        }
    }

    private void ValidateUsers()
    {
        foreach (var user in Users)
        {
            EnsureIdIsProvided(user.Id, nameof(SimulatedUser.Id));
            EnsureIdIsProvided(user.BusinessUnitId, nameof(SimulatedUser.BusinessUnitId));

            if (!_businessUnitsById.ContainsKey(user.BusinessUnitId))
            {
                throw new SimulatedSecurityModelException(
                    $"User '{user.Id}' references missing business unit '{user.BusinessUnitId}'.");
            }
        }
    }

    private void ValidateTeams()
    {
        foreach (var team in Teams)
        {
            EnsureIdIsProvided(team.Id, nameof(SimulatedTeam.Id));
            EnsureIdIsProvided(team.BusinessUnitId, nameof(SimulatedTeam.BusinessUnitId));

            if (!_businessUnitsById.ContainsKey(team.BusinessUnitId))
            {
                throw new SimulatedSecurityModelException(
                    $"Team '{team.Id}' references missing business unit '{team.BusinessUnitId}'.");
            }
        }
    }

    private void ValidateRoleAssignments()
    {
        foreach (var assignment in RoleAssignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.RoleName) || !_rolesByName.ContainsKey(assignment.RoleName))
                throw new SimulatedSecurityModelException($"Role assignment references missing role '{assignment.RoleName}'.");

            ValidatePrincipalExists(assignment.Principal, "Role assignment");
        }
    }

    private void ValidateTeamMemberships()
    {
        var seen = new HashSet<(Guid TeamId, Guid UserId)>();

        foreach (var membership in TeamMemberships)
        {
            if (!_teamsById.ContainsKey(membership.TeamId))
            {
                throw new SimulatedSecurityModelException(
                    $"Team membership references missing team '{membership.TeamId}'.");
            }

            if (!_usersById.ContainsKey(membership.UserId))
            {
                throw new SimulatedSecurityModelException(
                    $"Team membership references missing user '{membership.UserId}'.");
            }

            if (!seen.Add((membership.TeamId, membership.UserId)))
            {
                throw new SimulatedSecurityModelException(
                    $"Team membership for team '{membership.TeamId}' and user '{membership.UserId}' is duplicated.");
            }
        }
    }

    private void ValidatePrincipalObjectAccesses()
    {
        foreach (var access in PrincipalObjectAccesses)
        {
            if (access.Target.Id == Guid.Empty || string.IsNullOrWhiteSpace(access.Target.LogicalName))
                throw new SimulatedSecurityModelException("Principal object access target must include a logical name and id.");

            ValidatePrincipalExists(access.Principal, "Principal object access");
        }
    }

    private void ValidateBusinessUnitHasNoCycle(SimulatedBusinessUnit businessUnit)
    {
        var visited = new HashSet<Guid>();
        var current = businessUnit;

        while (current.ParentBusinessUnitId is { } parentId)
        {
            if (!visited.Add(current.Id))
            {
                throw new SimulatedSecurityModelException(
                    $"Business unit '{businessUnit.Id}' participates in a parent hierarchy cycle.");
            }

            current = _businessUnitsById[parentId];
        }
    }

    private void ValidatePrincipalExists(EntityReference principal, string source)
    {
        if (principal.Id == Guid.Empty || string.IsNullOrWhiteSpace(principal.LogicalName))
            throw new SimulatedSecurityModelException($"{source} principal must include a logical name and id.");

        switch (principal.LogicalName)
        {
            case "systemuser":
                if (!_usersById.ContainsKey(principal.Id))
                    throw new SimulatedSecurityModelException(
                        $"{source} references missing user principal '{principal.Id}'.");
                break;
            case "team":
                if (!_teamsById.ContainsKey(principal.Id))
                    throw new SimulatedSecurityModelException(
                        $"{source} references missing team principal '{principal.Id}'.");
                break;
            default:
                throw new SimulatedSecurityModelException(
                    $"{source} references unsupported principal type '{principal.LogicalName}'.");
        }
    }

    private SimulatedPrincipalObjectAccess? FindPrincipalObjectAccess(
        EntityReference target,
        EntityReference principal)
    {
        return PrincipalObjectAccesses.SingleOrDefault(x =>
            PrincipalMatches(x.Target, target) && PrincipalMatches(x.Principal, principal));
    }

    private static void ValidateAccessArguments(EntityReference target, EntityReference principal)
    {
        if (target.Id == Guid.Empty || string.IsNullOrWhiteSpace(target.LogicalName))
            throw new ArgumentException("Target must include a logical name and id.", nameof(target));

        if (principal.Id == Guid.Empty || string.IsNullOrWhiteSpace(principal.LogicalName))
            throw new ArgumentException("Principal must include a logical name and id.", nameof(principal));
    }

    private static Dictionary<Guid, T> BuildUniqueIdIndex<T>(
        IEnumerable<T> items,
        Func<T, Guid> idSelector,
        string itemType)
    {
        var result = new Dictionary<Guid, T>();

        foreach (var item in items)
        {
            var id = idSelector(item);
            EnsureIdIsProvided(id, itemType);

            if (!result.TryAdd(id, item))
                throw new SimulatedSecurityModelException($"A {itemType} with id '{id}' is duplicated.");
        }

        return result;
    }

    private static Dictionary<string, SimulatedSecurityRole> BuildUniqueNameIndex(
        IEnumerable<SimulatedSecurityRole> roles)
    {
        var result = new Dictionary<string, SimulatedSecurityRole>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            if (string.IsNullOrWhiteSpace(role.Name))
                throw new SimulatedSecurityModelException("Role name must be provided.");

            if (!result.TryAdd(role.Name, role))
                throw new SimulatedSecurityModelException($"A role named '{role.Name}' already exists.");
        }

        return result;
    }

    private static void MergeRole(
        IDictionary<string, EntityPermission> permissionsByEntity,
        SimulatedSecurityRole role)
    {
        foreach (var permission in role.EntityPermissions)
        {
            MergePermission(permissionsByEntity, permission);
        }
    }

    private static void MergePermission(
        IDictionary<string, EntityPermission> permissionsByEntity,
        IEntityPermission source)
    {
        if (!permissionsByEntity.TryGetValue(source.LogicalName, out var target))
        {
            target = new EntityPermission { LogicalName = source.LogicalName };
            permissionsByEntity[source.LogicalName] = target;
        }

        target.Create = Max(target.Create, source.Create);
        target.Read = Max(target.Read, source.Read);
        target.Write = Max(target.Write, source.Write);
        target.Delete = Max(target.Delete, source.Delete);
        target.Append = Max(target.Append, source.Append);
        target.AppendTo = Max(target.AppendTo, source.AppendTo);
        target.Assign = Max(target.Assign, source.Assign);
        target.Share = Max(target.Share, source.Share);
    }

    private static PrivilegeDepthEnum Max(PrivilegeDepthEnum a, PrivilegeDepthEnum b) =>
        (PrivilegeDepthEnum)Math.Max((int)a, (int)b);

    private static bool PrincipalMatches(EntityReference first, EntityReference second) =>
        first.Id == second.Id &&
        string.Equals(first.LogicalName, second.LogicalName, StringComparison.OrdinalIgnoreCase);

    private static EntityReference CloneReference(EntityReference reference)
    {
        return new EntityReference(reference.LogicalName, reference.Id)
        {
            Name = reference.Name,
            RowVersion = reference.RowVersion
        };
    }

    private static void EnsureUnique(Guid id, IEnumerable<Guid> existingIds, string itemType)
    {
        if (existingIds.Contains(id))
            throw new SimulatedSecurityModelException($"A {itemType} with id '{id}' already exists.");
    }

    private static void EnsureIdIsProvided(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id must be provided.", parameterName);
    }
}
