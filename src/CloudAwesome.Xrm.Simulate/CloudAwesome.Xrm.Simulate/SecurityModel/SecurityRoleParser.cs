using System.Text.RegularExpressions;
using System.Xml.Linq;
using CloudAwesome.Xrm.Simulate.Interfaces;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

internal static class SecurityRoleParser
{
	internal static ISecurityModel GenerateFromExportedSecurityRoleXml(
		string xmlPath,
		IDictionary<string, string>? logicalNameOverrides = null)
	{
		var dict = ParseRoleFileToDictionary(xmlPath, logicalNameOverrides);
		return FromDict(dict);
	}

	/// <summary>
	/// Read and merge multiple role XML files. Highest privilege depth wins.
	/// </summary>
	internal static ISecurityModel GenerateFromExportedSecurityRoleXml(
		IEnumerable<string> xmlPaths,
		IDictionary<string, string>? logicalNameOverrides = null)
	{
		if (xmlPaths is null) throw new ArgumentNullException(nameof(xmlPaths));

		var acc = new Dictionary<string, EntityPermission>(StringComparer.OrdinalIgnoreCase);

		foreach (var path in xmlPaths)
		{
			var dict = ParseRoleFileToDictionary(path, logicalNameOverrides);
			MergeEntityPermissionDictionaries(acc, dict);
		}

		return FromDict(acc);
	}
	
	/// <summary>
	/// Read and merge all role XML files in a directory. Uses searchPattern and optional recursion.
	/// </summary>
	internal static ISecurityModel GenerateSecurityModelFromDirectory(
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
		var files = Directory.EnumerateFiles(directoryPath, searchPattern, option);

		return GenerateFromExportedSecurityRoleXml(files, logicalNameOverrides);
	}
	
	/// <summary>
	/// Public helper to merge arbitrary models (e.g., when you already have instances).
	/// </summary>
	[Obsolete]
	private static ISecurityModel MergeSecurityModels(params ISecurityModel[] models)
	{
		if (models is null) throw new ArgumentNullException(nameof(models));

		var acc = new Dictionary<string, EntityPermission>(StringComparer.OrdinalIgnoreCase);

		foreach (var m in models.Where(m => m != null))
		{
			foreach (var ep in m.EntityPermissions.OfType<EntityPermission>())
			{
				if (!acc.TryGetValue(ep.LogicalName, out var dest))
				{
					dest = new EntityPermission { LogicalName = ep.LogicalName };
					acc[ep.LogicalName] = dest;
				}
				MergeInto(dest, ep);
			}

			// If some ISecurityModel instances use a different class for IEntityPermission, map them:
			foreach (var ep in m.EntityPermissions.Where(p => p is not EntityPermission))
			{
				if (!acc.TryGetValue(ep.LogicalName, out var dest))
				{
					dest = new EntityPermission { LogicalName = ep.LogicalName };
					acc[ep.LogicalName] = dest;
				}
				MergeInto(dest, ep);
			}
		}

		return FromDict(acc);
	}
	
	private static ISecurityModel FromDict(Dictionary<string, EntityPermission> dict) =>
        new SimulatedSecurityModel
        {
            IgnoreMissingEntities = true,
            EntityPermissions = dict.Values.Cast<IEntityPermission>().ToList()
        };

    private static Dictionary<string, EntityPermission> ParseRoleFileToDictionary(
        string xmlPath,
        IDictionary<string, string>? logicalNameOverrides)
    {
        if (string.IsNullOrWhiteSpace(xmlPath))
            throw new ArgumentException("Path must be provided.", nameof(xmlPath));
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException("Role XML not found.", xmlPath);

        var doc = XDocument.Load(xmlPath);

        var result = new Dictionary<string, EntityPermission>(StringComparer.OrdinalIgnoreCase);

        var rolePrivileges = doc
            .Descendants("RolePrivileges")
            .Descendants("RolePrivilege")
            .Select(x => new
            {
                Name = (string?)x.Attribute("name"),
                Level = (string?)x.Attribute("level")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Level));

        foreach (var rp in rolePrivileges)
        {
            if (!LevelMap.TryGetValue(rp.Level!, out var depth))
                continue;

            var m = PrivRegex.Match(rp.Name!);
            if (!m.Success) continue;

            var priv = m.Groups["priv"].Value;
            var entityToken = m.Groups["entity"].Value;
            if (string.IsNullOrEmpty(entityToken)) continue;

            var logicalName = ResolveLogicalName(entityToken, logicalNameOverrides);

            if (!result.TryGetValue(logicalName, out var ep))
            {
                ep = new EntityPermission { LogicalName = logicalName };
                result[logicalName] = ep;
            }

            ApplyPrivilege(ep, priv, depth);
        }

        return result;
    }
    
    // Regex matches: prv + Privilege + (optional "To") + Entity
    // Examples: prvCreateAccount, prvAppendToContact, prvReadmsdyn_warehouse
    private static readonly Regex PrivRegex =
	    new(@"^prv(?<priv>Create|Read|Write|Delete|AppendTo|Append|Assign|Share)(?<entity>.+)$",
		    RegexOptions.Compiled);
	
    private static readonly Dictionary<string, PrivilegeDepthEnum> LevelMap =
	    new(StringComparer.OrdinalIgnoreCase)
	    {
		    ["None"]  = PrivilegeDepthEnum.None, // Just in case
		    ["Basic"] = PrivilegeDepthEnum.User,
		    ["Local"] = PrivilegeDepthEnum.BusinessUnit,
		    ["Deep"]  = PrivilegeDepthEnum.ParentChild,
		    ["Global"]= PrivilegeDepthEnum.Organization
	    };

    private static void MergeEntityPermissionDictionaries(
        IDictionary<string, EntityPermission> acc,
        IDictionary<string, EntityPermission> next)
    {
        foreach (var kvp in next)
        {
            if (!acc.TryGetValue(kvp.Key, out var dest))
            {
                // clone to avoid sharing instances between accumulators
                dest = new EntityPermission { LogicalName = kvp.Key };
                acc[kvp.Key] = dest;
            }
            MergeInto(dest, kvp.Value);
        }
    }

    private static void MergeInto(EntityPermission dest, IEntityPermission src)
    {
        dest.Create   = Max(dest.Create,   src.Create);
        dest.Read     = Max(dest.Read,     src.Read);
        dest.Write    = Max(dest.Write,    src.Write);
        dest.Delete   = Max(dest.Delete,   src.Delete);
        dest.Append   = Max(dest.Append,   src.Append);
        dest.AppendTo = Max(dest.AppendTo, src.AppendTo);
        dest.Assign   = Max(dest.Assign,   src.Assign);
        dest.Share    = Max(dest.Share,    src.Share);
    }

    private static void ApplyPrivilege(EntityPermission ep, string priv, PrivilegeDepthEnum depth)
    {
        switch (priv)
        {
            case "Create":   ep.Create   = Max(ep.Create, depth);   break;
            case "Read":     ep.Read     = Max(ep.Read, depth);     break;
            case "Write":    ep.Write    = Max(ep.Write, depth);    break;
            case "Delete":   ep.Delete   = Max(ep.Delete, depth);   break;
            case "Append":   ep.Append   = Max(ep.Append, depth);   break;
            case "AppendTo": ep.AppendTo = Max(ep.AppendTo, depth); break;
            case "Assign":   ep.Assign   = Max(ep.Assign, depth);   break;
            case "Share":    ep.Share    = Max(ep.Share, depth);    break;
        }
    }

    private static PrivilegeDepthEnum Max(PrivilegeDepthEnum a, PrivilegeDepthEnum b)
        => (PrivilegeDepthEnum)Math.Max((int)a, (int)b);

    private static string ResolveLogicalName(string entityToken, IDictionary<string, string>? overrides)
    {
        if (overrides != null && overrides.TryGetValue(entityToken, out var mapped))
            return mapped;

        if (entityToken.Contains("_"))
            return entityToken.ToLowerInvariant();

        return char.ToLowerInvariant(entityToken[0]) + entityToken[1..];
    }
}