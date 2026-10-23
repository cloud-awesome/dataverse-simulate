namespace CloudAwesome.Xrm.Simulate.Interfaces;

/// <summary>
/// Legacy table-permission shape. Service simulation supports SimulatedSecurityModel;
/// other implementations are explicitly rejected because this interface alone does
/// not describe principals, ownership, or record access.
/// </summary>
public interface ISecurityModel
{
	/// <summary>
	/// If a entity doesn't have an security configuration defined, do not apply any security checks.
	/// This is to distinguish between a simulated user legitimately not having a privilege, and the test
	///		not being concerned with the security level. 
	/// </summary>
	public bool IgnoreMissingEntities { get; set; }
	
	/// <summary>
	/// List of all entity permission required for test actions and assertions
	/// </summary>
	public List<IEntityPermission> EntityPermissions { get; set; }
}
