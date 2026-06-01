using System;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.RequestFailureSettingTests;

[TestFixture]
public class DisassociateFailureTests
{
	private IOrganizationService _service = null!;
	
	[Test]
	public void Disassociate_Request_Fails_When_Configured()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [ new RequestFailureSetting("Disassociate") ]
			}
		};
		
		_service = _service.Simulate(options);
		_service.Simulated().Data().Add(Arthur.Account());
		_service.Simulated().Data().Add(Arthur.Contact());
		
		var relationship = new Relationship(Account.Fields.Account_Primary_Contact);
		var relatedEntities = new EntityReferenceCollection
		{
			Arthur.Account().ToEntityReference()
		};

		var sut = () => _service.Disassociate(Contact.EntityLogicalName, Arthur.Contact().Id, 
			relationship, relatedEntities);
		
		sut.Should().Throw<Exception>();
	}
	
	[Test]
	public void Disassociate_Request_Fails_On_Specific_Record_When_Configured()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = 
				[ 
					new RequestFailureSetting("Disassociate")
					{
						FailingRecords = [ Arthur.Contact().Id, Arthur.Account().Id ]
					} 
				]
			}
		};
		
		_service = _service.Simulate(options);
		_service.Simulated().Data().Add(Arthur.Account());
		_service.Simulated().Data().Add(Arthur.Contact());
		
		var relationship = new Relationship(Account.Fields.Account_Primary_Contact);
		var relatedEntities = new EntityReferenceCollection
		{
			Arthur.Account().ToEntityReference()
		};

		var sut = () => _service.Disassociate(Contact.EntityLogicalName, Arthur.Contact().Id, 
			relationship, relatedEntities);
		
		sut.Should().Throw<Exception>();
	}
	
	[Test]
	public void Disassociate_Request_Succeeds_If_Not_Listed_In_Failing_Records()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = 
				[ 
					new RequestFailureSetting("Disassociate")
					{
						FailingRecords = [ Guid.NewGuid() ]
					} 
				]
			}
		};
		
		_service = _service.Simulate(options);
		_service.Simulated().Data().Add(Arthur.Account());
		_service.Simulated().Data().Add(Arthur.Contact());
		
		var relationship = new Relationship(Account.Fields.Account_Primary_Contact);
		var relatedEntities = new EntityReferenceCollection
		{
			Arthur.Account().ToEntityReference()
		};

		var sut = () => _service.Disassociate(Contact.EntityLogicalName, Arthur.Contact().Id, 
			relationship, relatedEntities);
		
		sut.Should().NotThrow<Exception>();
	}
}