using System;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.DocumentationCode;

/// <summary>
/// Includes code samples for https://docs.cloudawesome.uk/dataverse-simulate/inject-custom-organization-requests
/// </summary>
[TestFixture]
public class CustomOrgRequestHandlerTests
{
	private IOrganizationService _organizationService = null!;
	
	[Test]
	public void Code_Can_Execute_Custom_Request()
	{
		//Arrange
		_organizationService = _organizationService.Simulate();

		_organizationService
			.Simulated()
			.CustomOrgRequests()
			.Add<new_CalculateAccountScoreRequest>((request, context) =>
			{
				var account = context.Data.Get(request.Target);
				account["new_score"] = 100;
				context.Data.Update(account);

				return new new_CalculateAccountScoreResponse
				{
					Results = new ParameterCollection
					{
						["Score"] = 100
					},
					ResponseName = request.RequestName
				};
			});

		var account = new Entity("account", Guid.NewGuid())
		{
			["name"] = "Cloud Awesome"
		};

		_organizationService.Simulated().Data().Add(account);

		// Act
		var response = (new_CalculateAccountScoreResponse)
			_organizationService.Execute(
				new new_CalculateAccountScoreRequest
				{
					Target = account.ToEntityReference()
				}
			);

		// Assert
		response.Results["Score"].Should().Be(100);
    
		_organizationService.Simulated().Data()
			.Get(account.LogicalName, account.Id)["new_score"]
			.Should().Be(100);
	}
	
	[Test]
	public void Code_Can_Execute_Custom_Request_With_External_Implementation()
	{
		//Arrange
		_organizationService = _organizationService.Simulate();

		_organizationService
			.Simulated()
			.CustomOrgRequests()
			.Add<new_CalculateAccountScoreRequest>(CalculateAccountScore);

		var account = new Entity("account", Guid.NewGuid())
		{
			["name"] = "Cloud Awesome"
		};

		_organizationService.Simulated().Data().Add(account);

		// Act
		var response = (new_CalculateAccountScoreResponse)
			_organizationService.Execute(
				new new_CalculateAccountScoreRequest
				{
					Target = account.ToEntityReference()
				}
			);

		// Assert
		response.Results["Score"].Should().Be(100);
    
		_organizationService.Simulated().Data()
			.Get(account.LogicalName, account.Id)["new_score"]
			.Should().Be(100);
	}

	[Test]
	public void Implementation_Can_Consume_SimulatorOptions()
	{
		var options = new SimulatorOptions
		{
			ClockSimulator = new MockSystemTime(new DateTime(2026, 1, 1))
		};

		_organizationService = _organizationService.Simulate(options);

		_organizationService
			.Simulated()
			.CustomOrgRequests()
			.Add<new_TimestampRequest>((request, context) =>
			{
				var now = context.Data.SystemTime;

				return new OrganizationResponse
				{
					ResponseName = request.RequestName,
					Results = new ParameterCollection
					{
						["Timestamp"] = now
					}
				};
			});
		
		// Act
		var response = (OrganizationResponse)
			_organizationService.Execute(
				new new_TimestampRequest()
			);

		// Assert
		response.Results["Timestamp"].Should().Be(options.ClockSimulator.Now);
	}

	[Test]
	public void Duplicate_Handler_Registration_Throws_Exception()
	{
		_organizationService = _organizationService.Simulate();
		
		// Throws InvalidOperationException
		 var sut = () =>
			 _organizationService
				 .Simulated()
				 .CustomOrgRequests()
				 .Add<CreateRequest>((request, context) => new CreateResponse());
		 
		 sut.Should().Throw<InvalidOperationException>();
	}
	
	private sealed class new_TimestampRequest : OrganizationRequest
	{
		public new_TimestampRequest()
		{
			RequestName = "new_TimestampRequest";
		}

		public DateTime Timestamp
		{
			get => (DateTime)Parameters["Timestamp"];
			init => Parameters["Timestamp"] = value;
		}
	}
	
	private sealed class new_CalculateAccountScoreRequest : OrganizationRequest
	{
		public new_CalculateAccountScoreRequest()
		{
			RequestName = "new_CalculateAccountScore";
		}

		public EntityReference Target
		{
			get => (EntityReference)Parameters["Target"];
			init => Parameters["Target"] = value;
		}
	}

	private sealed class new_CalculateAccountScoreResponse : OrganizationResponse
	{
		public int Score => (int)Results["Score"];
	}
	
	private OrganizationResponse CalculateAccountScore(
		new_CalculateAccountScoreRequest request, 
		CustomOrganizationRequestContext context) {
        
		var account = context.Data.Get(request.Target);
		account["new_score"] = 100;
		context.Data.Update(account);

		return new new_CalculateAccountScoreResponse
		{
			Results = new ParameterCollection
			{
				["Score"] = 100
			},
			ResponseName = request.RequestName
		};
	}
}