using CloudAwesome.Xrm.Simulate.Interfaces;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public class EntityPermission: IEntityPermission
{
	public string LogicalName { get; set; } = string.Empty;
	
	public PrivilegeDepthEnum Create { get; set; } = PrivilegeDepthEnum.None;
	public PrivilegeDepthEnum Read { get; set; } = PrivilegeDepthEnum.None;
	public PrivilegeDepthEnum Write { get; set; } = PrivilegeDepthEnum.None;
	public PrivilegeDepthEnum Delete { get; set; } = PrivilegeDepthEnum.None;
	public PrivilegeDepthEnum Append { get; set; } = PrivilegeDepthEnum.None;
	public PrivilegeDepthEnum AppendTo { get; set; } = PrivilegeDepthEnum.None;
	public PrivilegeDepthEnum Assign { get; set; } = PrivilegeDepthEnum.None;
	public PrivilegeDepthEnum Share { get; set; } = PrivilegeDepthEnum.None;

	public PrivilegeDepthEnum GetDepth(SecurityPrivilege privilege) =>
		privilege switch
		{
			SecurityPrivilege.Create => Create,
			SecurityPrivilege.Read => Read,
			SecurityPrivilege.Write => Write,
			SecurityPrivilege.Delete => Delete,
			SecurityPrivilege.Append => Append,
			SecurityPrivilege.AppendTo => AppendTo,
			SecurityPrivilege.Assign => Assign,
			SecurityPrivilege.Share => Share,
			_ => PrivilegeDepthEnum.None
		};

	public void SetDepth(SecurityPrivilege privilege, PrivilegeDepthEnum depth)
	{
		switch (privilege)
		{
			case SecurityPrivilege.Create:
				Create = depth;
				break;
			case SecurityPrivilege.Read:
				Read = depth;
				break;
			case SecurityPrivilege.Write:
				Write = depth;
				break;
			case SecurityPrivilege.Delete:
				Delete = depth;
				break;
			case SecurityPrivilege.Append:
				Append = depth;
				break;
			case SecurityPrivilege.AppendTo:
				AppendTo = depth;
				break;
			case SecurityPrivilege.Assign:
				Assign = depth;
				break;
			case SecurityPrivilege.Share:
				Share = depth;
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(privilege), privilege, null);
		}
	}
}
