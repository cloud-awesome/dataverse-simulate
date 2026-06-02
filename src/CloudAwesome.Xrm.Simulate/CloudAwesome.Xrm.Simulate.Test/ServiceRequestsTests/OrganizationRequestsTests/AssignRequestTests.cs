using System;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.OrganizationRequestsTests;

[TestFixture]
public class AssignRequestTests
{
	private IOrganizationService _organizationService = null!;

	private SimulatorOptions _options = new()
	{
		ClockSimulator = new MockSystemTime(new DateTime(2024, 03, 10, 14, 30, 00)),
		AuthenticatedUser = new SystemUser
		{
			Id = Guid.NewGuid(),
			FullName = "Daphne Moon"
		}
	};

	[SetUp]
	public void SetUp()
	{
		_organizationService = _organizationService.Simulate(_options);
		_organizationService.Simulated().Data().Add(_targetUser);
		_organizationService.Simulated().Data().Add(_targetTeam);
		_organizationService.Simulated().Data().Add(_lead);
	}
	
	[Test]
	public void Assigning_To_A_User_Correctly_Updates_Record()
	{
		var assignRequest = new AssignRequest
		{
			Assignee = _targetUser.ToEntityReference(),
			Target = _lead.ToEntityReference()
		};

		var response = _organizationService.Execute(assignRequest);

		response.ResponseName.Should().Be("Assign");

		var lead = (Lead) _organizationService.Simulated().Data().Get(_lead.ToEntityReference());

		lead.OwnerId.Should().Be(_targetUser.ToEntityReference());
		lead.OwningUser.Should().Be(_targetUser.ToEntityReference());

		lead.ModifiedOn.Should().Be(new DateTime(2024, 03, 10, 14, 30, 00));
		lead.ModifiedBy.Id.Should().Be(_options.AuthenticatedUser!.Id);
	}

	[Test]
	public void Assigning_To_A_Team_Correctly_Updates_Record()
	{
		var assignRequest = new AssignRequest
		{
			Assignee = _targetTeam.ToEntityReference(),
			Target = _lead.ToEntityReference()
		};

		var response = _organizationService.Execute(assignRequest);
		
		response.ResponseName.Should().Be("Assign");
		
		var lead = (Lead) _organizationService.Simulated().Data().Get(_lead.ToEntityReference());
		
		lead.OwnerId.Should().Be(_targetTeam.ToEntityReference());
		lead.OwningTeam.Should().Be(_targetTeam.ToEntityReference());
		
		lead.ModifiedOn.Should().Be(new DateTime(2024, 03, 10, 14, 30, 00));
		lead.ModifiedBy.Id.Should().Be(_options.AuthenticatedUser!.Id);
	}

	[Test]
	public void Assign_Request_Fails_When_Configured()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [ new RequestFailureSetting("Assign") ]
			}
		};
		
		_organizationService = _organizationService.Simulate(options);
		_organizationService.Simulated().Data().Add(_targetUser);
		_organizationService.Simulated().Data().Add(_targetTeam);
		_organizationService.Simulated().Data().Add(_lead);
		
		var assignRequest = new AssignRequest
		{
			Assignee = _targetUser.ToEntityReference(),
			Target = _lead.ToEntityReference()
		};
		
		var sut = () => _organizationService.Execute(assignRequest);
		
		sut.Should().Throw<Exception>();
	}
	
	[Test]
	public void Assign_Request_Fails_On_Specific_Record_When_Configured()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = 
					[
						new RequestFailureSetting("Assign")
						{
							FailingRecords = [ _lead.Id ]
						}
					]
			}
		};
		
		_organizationService = _organizationService.Simulate(options);
		_organizationService.Simulated().Data().Add(_targetUser);
		_organizationService.Simulated().Data().Add(_targetTeam);
		_organizationService.Simulated().Data().Add(_lead);
		
		var assignRequest = new AssignRequest
		{
			Assignee = _targetUser.ToEntityReference(),
			Target = _lead.ToEntityReference()
		};
		
		var sut = () => _organizationService.Execute(assignRequest);
		
		sut.Should().Throw<Exception>();
	}
	
	[Test]
	public void Assign_Request_Succeeds_When_Specific_Record_Not_Configured()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = 
				[
					new RequestFailureSetting("Assign")
					{
						FailingRecords = [ Guid.NewGuid() ]
					}
				]
			},
		};
		
		_organizationService = _organizationService.Simulate(options);
		_organizationService.Simulated().Data().Add(_targetUser);
		_organizationService.Simulated().Data().Add(_targetTeam);
		_organizationService.Simulated().Data().Add(_lead);
		
		var assignRequest = new AssignRequest
		{
			Assignee = _targetUser.ToEntityReference(),
			Target = _lead.ToEntityReference()
		};
		
		var sut = () => _organizationService.Execute(assignRequest);

		sut.Should().NotThrow();
	}
	
	private readonly SystemUser _targetUser = new SystemUser
	{
		Id = Guid.NewGuid(),
		FirstName = "Frasier",
		LastName = "Crane"
	};

	private readonly Team _targetTeam = new Team
	{
		Id = Guid.NewGuid(),
		Name = "Sales Team"
	};

	private Lead _lead = new Lead()
	{
		Id = Guid.NewGuid(),
		Subject = "New lead for someone"
	};
}