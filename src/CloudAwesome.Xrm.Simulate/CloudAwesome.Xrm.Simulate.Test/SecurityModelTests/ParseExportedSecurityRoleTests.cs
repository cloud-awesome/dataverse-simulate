using System.IO;
using System.Linq;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.SecurityModelTests;

[TestFixture]
public class ParseExportedSecurityRoleTests
{
	private readonly string _securityRoleFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory,
		"TestData", "Roles", "Initial Test Security Role.xml");
	
	[Test]
	public void Single_Role_Is_Parsed_From_Xml_File()
	{
		var securityModel = new SimulatedSecurityModel(_securityRoleFilePath);

		securityModel.EntityPermissions.Should().NotBeNull();
		securityModel.EntityPermissions.Count.Should().Be(149);
	}
	
	[Test]
	public void Single_Role_Maps_Privileges_Correctly()
	{
		var securityModel = new SimulatedSecurityModel(_securityRoleFilePath);

		var contactPermissions = 
			securityModel
				.EntityPermissions
				.SingleOrDefault(x => x.LogicalName == "contact");

		contactPermissions.Should().NotBeNull();
		
		contactPermissions!.Create.Should().Be(PrivilegeDepthEnum.User);
		contactPermissions.Read.Should().Be(PrivilegeDepthEnum.User);
		contactPermissions.Write.Should().Be(PrivilegeDepthEnum.User);
		contactPermissions.Delete.Should().Be(PrivilegeDepthEnum.User);
		contactPermissions.Append.Should().Be(PrivilegeDepthEnum.User);
		contactPermissions.AppendTo.Should().Be(PrivilegeDepthEnum.User);
		contactPermissions.Assign.Should().Be(PrivilegeDepthEnum.User);
		contactPermissions.Share.Should().Be(PrivilegeDepthEnum.Organization);
	}

	[Test]
	public void ImportRoleXml_Adds_Named_Role_From_Xml()
	{
		var securityModel = SimulatedSecurityModel.Create()
			.ImportRoleXml(_securityRoleFilePath);

		securityModel.Roles.Should().ContainSingle();
		securityModel.Roles.Single().Name.Should().Be("Initial Test Security Role");
		securityModel.Roles.Single().EntityPermissions.Should().HaveCount(149);
	}

	[Test]
	public void Imported_Role_Can_Be_Assigned_To_User()
	{
		var businessUnitId = System.Guid.NewGuid();
		var userId = System.Guid.NewGuid();
		var securityModel = SimulatedSecurityModel.Create()
			.WithBusinessUnit(businessUnitId, "Root")
			.WithUser(userId, businessUnitId)
			.ImportRoleXml(_securityRoleFilePath, "Imported Salesperson")
			.AssignRoleToUser("Imported Salesperson", userId);

		var permissions = securityModel.GetEffectiveEntityPermissionForPrincipal(
			new EntityReference("systemuser", userId),
			"contact");

		permissions.Should().NotBeNull();
		permissions!.Read.Should().Be(PrivilegeDepthEnum.User);
		permissions.Share.Should().Be(PrivilegeDepthEnum.Organization);
	}
}
