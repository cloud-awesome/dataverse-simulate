using System;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.RequestFailureSettingTests;

[TestFixture]
public class DeleteFailureTests
{
	private IOrganizationService _service = null!;
	
	[Test]
	public void Delete_Request_Fails_When_Configured()
	{
		var account = new Account(Guid.NewGuid())
		{
			Name = "Test Account"
		};
		
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [ new RequestFailureSetting("Delete") ]
			}
		};
		
		_service = _service.Simulate(options);
		_service.Simulated().Data().Add(account);
		
		var sut = () => _service.Delete(account.LogicalName, account.Id);
		
		sut.Should().Throw<Exception>();
	}
	
	[Test]
	public void Delete_Request_Fails_On_Specific_Record_When_Configured()
	{
		var account = new Account(Guid.NewGuid())
		{
			Name = "Test Account"
		};
		
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = 
				[ 
					new RequestFailureSetting("Delete")
					{
						FailingRecords = [ account.Id ]
					} 
				]
			}
		};
		
		_service = _service.Simulate(options);
		_service.Simulated().Data().Add(account);
		
		var sut = () => _service.Delete(account.LogicalName, account.Id);
		
		sut.Should().Throw<Exception>();
	}
	
	[Test]
	public void Delete_Request_Succeeds_If_Not_Listed_In_Failing_Records()
	{
		var account = new Account(Guid.NewGuid())
		{
			Name = "Test Account"
		};
		
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = 
				[ 
					new RequestFailureSetting("Delete")
					{
						FailingRecords = [ Guid.NewGuid() ]
					} 
				]
			}
		};
		
		_service = _service.Simulate(options);
		_service.Simulated().Data().Add(account);
		
		var sut = () => _service.Delete(account.LogicalName, account.Id);
		
		sut.Should().NotThrow<Exception>();
	}
}