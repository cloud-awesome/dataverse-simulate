using System;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.RequestFailureSettingTests;

[TestFixture]
public class CreateFailureTests
{
	private IOrganizationService _service = null!;
	
	[Test]
	public void Create_Request_Fails_When_Configured()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [ new RequestFailureSetting("Create") ]
			}
		};
		
		_service = _service.Simulate(options);
		
		var sut = () => _service.Create(new Account()
		{
			Name = "Test Account"
		});

		sut.Should().Throw<Exception>();
	}

	[Test]
	public void Create_Request_Throws_Configured_Exception()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = 
				[ 
					new RequestFailureSetting("Create")
					{
						Exception = new InvalidOperationException("Simulated exception for Create request")
					} 
				]
			}
		};
		
		_service = _service.Simulate(options);
		
		var sut = () => _service.Create(new Account()
		{
			Name = "Test Account"
		});
		
		sut.Should().ThrowExactly<InvalidOperationException>();
	}
}