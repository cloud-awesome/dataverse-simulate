namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SimulatedSecurityRole
{
    public string Name { get; set; } = string.Empty;
    public List<SimulatedRolePrivilege> EntityPermissions { get; } = [];

    public SimulatedSecurityRole()
    {
    }

    public SimulatedSecurityRole(string name)
    {
        Name = name;
    }

    public SimulatedSecurityRole CanCreate(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Create, depth);

    public SimulatedSecurityRole CanRead(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Read, depth);

    public SimulatedSecurityRole CanWrite(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Write, depth);

    public SimulatedSecurityRole CanDelete(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Delete, depth);

    public SimulatedSecurityRole CanAppend(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Append, depth);

    public SimulatedSecurityRole CanAppendTo(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.AppendTo, depth);

    public SimulatedSecurityRole CanAssign(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Assign, depth);

    public SimulatedSecurityRole CanShare(string entityLogicalName, PrivilegeDepthEnum depth) =>
        SetPrivilege(entityLogicalName, SecurityPrivilege.Share, depth);

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
