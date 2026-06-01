using System;
using CloudAwesome.Xrm.Simulate.DataStores;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.RequestFailureSettingTests;

[TestFixture]
public class RetrieveMultipleFailureTests
{
	private IOrganizationService _service = null!;
	
	[Test]
	public void QueryExpression_Fails_When_Configured()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [ new RequestFailureSetting("RetrieveMultiple") ]
			}
		};
		
		_service = _service.Simulate(options);
		
		var query = new QueryExpression("account");
		
		var sut = () => _service.RetrieveMultiple(query);
		
		sut.Should().Throw<Exception>();
	}
	
	[Test]
	public void FetchExpression_Fails_When_Configured()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [ new RequestFailureSetting("RetrieveMultiple") ]
			}
		};
		
		_service = _service.Simulate(options);
		
		var fetch = @"<fetch version=""1.0"" output-format=""xml-platform"" mapping=""logical"">
                        <entity name=""contact"">
                          <order attribute=""FirstName"" descending=""true"" />
                        </entity>
                      </fetch>";
        
		var query = new FetchExpression { Query = fetch };
		
		var sut = () => _service.RetrieveMultiple(query);
		
		sut.Should().Throw<Exception>();
	}
	
	[Test]
	public void QueryByAttribute_Fails_When_Configured()
	{
		var options = new SimulatorOptions
		{
			FakeServiceFailureSettings = new FakeServiceFailureSettings
			{
				RequestFailureSettings = [ new RequestFailureSetting("RetrieveMultiple") ]
			}
		};
		
		_service = _service.Simulate(options);
		
		var query = new QueryByAttribute("account");
		
		var sut = () => _service.RetrieveMultiple(query);
		
		sut.Should().Throw<Exception>();
	}
}