using System;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture]
public class LinkedEntityTests
{
	private IOrganizationService _organizationService = null!;

	[SetUp]
	public void SetUp()
	{
		_organizationService = _organizationService.Simulate();
	}
	
	[Test]
	public void LinkedEntity_Should_Return_AliasedValue()
	{
		// Arrange
		var businessUnitId = Guid.NewGuid();
		var teamId = Guid.NewGuid();
		var roleId = Guid.NewGuid();
		
		_organizationService.Simulated().Data().Add(
			new Team
			{
				Id = teamId,
				Name = "Delivery Team",
				BusinessUnitId = new EntityReference("businessunit", businessUnitId)
			});
		_organizationService.Simulated().Data().Add(
			new Role
			{
				Id = roleId,
				Name = "Basic User",
				BusinessUnitId = new EntityReference("businessunit", businessUnitId) 
			});
		_organizationService.Simulated().Data().Add(
			new TeamRoles
			{
				Id = Guid.NewGuid(),
				TeamId = teamId,
				RoleId = roleId
			});

		var teamRolesQuery = new QueryExpression
		{
			EntityName = TeamRoles.EntityLogicalName,
			ColumnSet = new ColumnSet(true),
			Criteria = new FilterExpression
			{
				Conditions =
				{
					new ConditionExpression(TeamRoles.Fields.TeamId, ConditionOperator.Equal, teamId)
				}
			},
			LinkEntities =
			{
				new LinkEntity
				{
					LinkFromEntityName = TeamRoles.EntityLogicalName,
					LinkToEntityName = Role.EntityLogicalName,
					LinkFromAttributeName = TeamRoles.Fields.RoleId,
					LinkToAttributeName = Role.Fields.RoleId,
					EntityAlias = "roleAlias",
					Columns = new ColumnSet(Role.Fields.Name)
				}
			}
		};
		
		var teamRoles = 
			_organizationService.RetrieveMultiple(teamRolesQuery).Entities;

		var roleNameAddress = $"roleAlias.{Role.Fields.Name}";
		foreach (var teamRole in teamRoles)
		{
			teamRole[roleNameAddress].Should().BeOfType<AliasedValue>()
				.Which.Value.Should().Be("Basic User");
		}

	}
}