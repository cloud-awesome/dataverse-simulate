using System;
using System.Linq;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.OrganizationRequestsTests;

[TestFixture]
public class WhoAmIRequestTests
{
	private IOrganizationService _organizationService = null!;
	
	private readonly SimulatorOptions _options = new()
	{
		AuthenticatedUser = new SystemUser
		{
			Id = Guid.NewGuid(),
			FullName = "Daphne Moon"
		},
		BusinessUnit = new BusinessUnit
		{
			Id = Guid.NewGuid(),
			Name = "Root BU"
		},
		Organization = new Organization
		{
			Id = Guid.NewGuid(),
			Name = "Cloud Awesome"
		}
	};

	[SetUp]
	public void SetUp()
	{
		_organizationService = _organizationService.Simulate(_options);
	}
	
	[Test]
	public void Basic_Request_Does_Not_Throw_Exception()
	{
		var sut = () => _organizationService.Execute(new WhoAmIRequest());
		sut.Should().NotThrow();
	}

	[Test]
	public void Request_Returns_Valid_Data_From_Injected_Options()
	{
		var response = (WhoAmIResponse) _organizationService.Execute(new WhoAmIRequest());

		response.UserId.Should().Be(_options.AuthenticatedUser!.Id);
		response.BusinessUnitId.Should().Be(_options.BusinessUnit!.Id);
		response.OrganizationId.Should().Be(_options.Organization!.Id);
	}

	[Test]
	public void Request_Results_Can_Be_Used_To_Correlate_With_User_Entity_Data()
	{
		var response = (WhoAmIResponse) _organizationService.Execute(new WhoAmIRequest());

		var user = _organizationService
			.Simulated().Data()
			.Get(SystemUser.EntityLogicalName)
			.Cast<SystemUser>()
			.SingleOrDefault(x => x.Id == response.UserId);

		user.Should().NotBeNull();
		user!.Id.Should().Be(_options.AuthenticatedUser!.Id);
		user!.FullName.Should().Be("Daphne Moon");
	}
	
	[Test]
	public void Request_Results_Can_Be_Used_To_Correlate_With_BusinessUnit_Entity_Data()
	{
		var response = (WhoAmIResponse) _organizationService.Execute(new WhoAmIRequest());

		var bu = _organizationService
			.Simulated().Data()
			.Get(BusinessUnit.EntityLogicalName)
			.Cast<BusinessUnit>()
			.SingleOrDefault(x => x.Id == response.BusinessUnitId);

		bu.Should().NotBeNull();
		bu!.Id.Should().Be(_options.BusinessUnit!.Id);
		bu!.Name.Should().Be("Root BU");
	}
	
	[Test]
	public void Request_Results_Can_Be_Used_To_Correlate_With_Organization_Entity_Data()
	{
		var response = (WhoAmIResponse) _organizationService.Execute(new WhoAmIRequest());

		var org = _organizationService
			.Simulated().Data()
			.Get(Organization.EntityLogicalName)
			.Cast<Organization>()
			.SingleOrDefault(x => x.Id == response.OrganizationId);

		org.Should().NotBeNull();
		org!.Id.Should().Be(_options.Organization!.Id);
		org!.Name.Should().Be("Cloud Awesome");
	}

	[Test]
	public void Request_Fails_When_Configured()
	{
		var options = new SimulatorOptions
		{
			AuthenticatedUser = new SystemUser
			{
				Id = Guid.NewGuid(),
				FullName = "Daphne Moon"
			},
			BusinessUnit = new BusinessUnit
			{
				Id = Guid.NewGuid(),
				Name = "Root BU"
			},
			Organization = new Organization
			{
				Id = Guid.NewGuid(),
				Name = "Cloud Awesome"
			},
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [ new RequestFailureSetting("WhoAmI") ]
			}
		};;
		_organizationService = _organizationService.Simulate(options);
		
		var sut = () => _organizationService.Execute(new WhoAmIRequest());
		sut.Should().Throw<Exception>();
	}
}