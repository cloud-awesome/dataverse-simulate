using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedSecurityRole
{
    private readonly Dictionary<Guid, Guid> _roleIdsByBusinessUnitId = [];

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid? BusinessUnitId { get; set; }
    public List<SimulatedRolePrivilege> EntityPermissions { get; } = [];

    public SimulatedSecurityRole()
    {
    }

    public SimulatedSecurityRole(string name)
    {
        Name = name;
    }

    public Guid GetRoleId(Guid businessUnitId, Guid rootBusinessUnitId)
    {
        if ((BusinessUnitId ?? rootBusinessUnitId) == businessUnitId)
        {
            return Id;
        }

        if (!_roleIdsByBusinessUnitId.TryGetValue(businessUnitId, out var roleId))
        {
            roleId = Guid.NewGuid();
            _roleIdsByBusinessUnitId[businessUnitId] = roleId;
        }

        return roleId;
    }

    public Entity ToEntity(
        Guid businessUnitId,
        Guid rootBusinessUnitId,
        Guid? parentBusinessUnitId = null)
    {
        var roleId = GetRoleId(businessUnitId, rootBusinessUnitId);
        var rootRoleId = GetRoleId(BusinessUnitId ?? rootBusinessUnitId, rootBusinessUnitId);
        var entity = new Entity("role", Id)
        {
            Id = roleId,
            ["roleid"] = roleId,
            ["name"] = Name,
            ["businessunitid"] = new EntityReference("businessunit", businessUnitId),
            ["parentrootroleid"] = new EntityReference("role", rootRoleId)
        };

        if (parentBusinessUnitId is { } parentId)
        {
            entity["parentroleid"] = new EntityReference("role", GetRoleId(parentId, rootBusinessUnitId));
        }

        return entity;
    }

	/// <summary>Sets the Create privilege depth for a table in this role.</summary>
	/// <param name="entityLogicalName">The table logical name.</param>
	/// <param name="depth">The privilege depth, including None to deny this privilege.</param>
	/// <returns>This role definition for further privilege configuration.</returns>
	/// <remarks>Defining privileges does not assign the role to a user or team.</remarks>
    public SimulatedSecurityRole CanCreate(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Create, depth);

	/// <summary>Sets the Read privilege depth for a table in this role.</summary>
	/// <param name="entityLogicalName">The table logical name.</param>
	/// <param name="depth">The privilege depth, including None to deny this privilege.</param>
	/// <returns>This role definition for further privilege configuration.</returns>
	/// <remarks>Defining privileges does not assign the role to a user or team.</remarks>
    public SimulatedSecurityRole CanRead(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Read, depth);

	/// <summary>Sets the Write privilege depth for a table in this role.</summary>
	/// <param name="entityLogicalName">The table logical name.</param>
	/// <param name="depth">The privilege depth, including None to deny this privilege.</param>
	/// <returns>This role definition for further privilege configuration.</returns>
	/// <remarks>Defining privileges does not assign the role to a user or team.</remarks>
    public SimulatedSecurityRole CanWrite(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Write, depth);

	/// <summary>Sets the Delete privilege depth for a table in this role.</summary>
	/// <param name="entityLogicalName">The table logical name.</param>
	/// <param name="depth">The privilege depth, including None to deny this privilege.</param>
	/// <returns>This role definition for further privilege configuration.</returns>
	/// <remarks>Defining privileges does not assign the role to a user or team.</remarks>
    public SimulatedSecurityRole CanDelete(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Delete, depth);

	/// <summary>Sets the Append privilege depth for a table in this role.</summary>
	/// <param name="entityLogicalName">The table logical name.</param>
	/// <param name="depth">The privilege depth, including None to deny this privilege.</param>
	/// <returns>This role definition for further privilege configuration.</returns>
	/// <remarks>Defining privileges does not assign the role to a user or team.</remarks>
    public SimulatedSecurityRole CanAppend(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Append, depth);

	/// <summary>Sets the AppendTo privilege depth for a table in this role.</summary>
	/// <param name="entityLogicalName">The table logical name.</param>
	/// <param name="depth">The privilege depth, including None to deny this privilege.</param>
	/// <returns>This role definition for further privilege configuration.</returns>
	/// <remarks>Defining privileges does not assign the role to a user or team.</remarks>
    public SimulatedSecurityRole CanAppendTo(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.AppendTo, depth);

	/// <summary>Sets the Assign privilege depth for a table in this role.</summary>
	/// <param name="entityLogicalName">The table logical name.</param>
	/// <param name="depth">The privilege depth, including None to deny this privilege.</param>
	/// <returns>This role definition for further privilege configuration.</returns>
	/// <remarks>Defining privileges does not assign the role to a user or team.</remarks>
    public SimulatedSecurityRole CanAssign(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Assign, depth);

	/// <summary>Sets the Share privilege depth for a table in this role.</summary>
	/// <param name="entityLogicalName">The table logical name.</param>
	/// <param name="depth">The privilege depth, including None to deny this privilege.</param>
	/// <returns>This role definition for further privilege configuration.</returns>
	/// <remarks>Defining privileges does not assign the role to a user or team.</remarks>
    public SimulatedSecurityRole CanShare(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Share, depth);

	/// <summary>Sets an explicitly selected table privilege and depth.</summary>
	/// <param name="entityLogicalName">The table logical name.</param>
	/// <param name="privilege">The privilege to configure.</param>
	/// <param name="depth">The privilege depth.</param>
	/// <returns>This role definition for further configuration.</returns>
	/// <exception cref="ArgumentException">The logical name is empty.</exception>
    public SimulatedSecurityRole SetPrivilege(
        string entityLogicalName,
        SecurityPrivilege privilege,
        PrivilegeDepthEnum depth)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
            throw new ArgumentException("Entity logical name must be provided.", nameof(entityLogicalName));

        var permission = EntityPermissions.SingleOrDefault(x =>
            string.Equals(x.LogicalName, entityLogicalName, StringComparison.OrdinalIgnoreCase));

        if (permission is null)
        {
            permission = new SimulatedRolePrivilege { LogicalName = entityLogicalName };
            EntityPermissions.Add(permission);
        }

        permission.SetDepth(privilege, depth);
        return this;
    }
}
