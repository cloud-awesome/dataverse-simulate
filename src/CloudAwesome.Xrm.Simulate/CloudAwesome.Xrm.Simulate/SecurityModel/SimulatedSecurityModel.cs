using CloudAwesome.Xrm.Simulate.Interfaces;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public class SimulatedSecurityModel: ISecurityModel
{
	public bool IgnoreMissingEntities { get; set; } = true;
	public List<IEntityPermission> EntityPermissions { get; set; } = new List<IEntityPermission>();

	/// <summary>
	/// Empty constructor. Manually define all entity permissions required for the sut
	/// </summary>
	public SimulatedSecurityModel() {}
	
	/// <summary>
	/// Generate the SimulatedSecurityModel from an exported dataverse security role XML file. 
	/// Use the PAC Cli to export and extract security roles, which generates XML files in the format expected. 
	/// </summary>
	/// <param name="xmlPath">File path to the extract security role</param>
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
	/// If an entity permission is referenced multiple times, the highest permission is taken (as matches dataverse itself).
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
	/// <param name="directoryPath">Path to read the extracted XML files</param>
	/// <param name="searchPattern">Optional. Defaults to *.xml, but a more granular filter can be provided if required</param>
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
		var model = SecurityRoleParser.GenerateSecurityModelFromDirectory(directoryPath, searchPattern, recursive,
			logicalNameOverrides);

		EntityPermissions = model.EntityPermissions;
	}
}