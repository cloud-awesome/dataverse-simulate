using System;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.OrganizationRequestsTests;

[TestFixture]
public class CustomOrganizationRequestTests
{
	private IOrganizationService _organizationService = null!;

	[SetUp]
	public void Setup()
	{
		_organizationService = _organizationService.Simulate();
	}

	[Test]
	public void Add_Handles_Custom_Request_Through_Execute()
	{
		var account = new Entity("account")
		{
			Id = Guid.NewGuid()
		};

		_organizationService
			.Simulated()
			.CustomOrgRequests()
			.Add<TestCustomRequest>((request, context) =>
			{
				context.Data.Add(request.Target);

				return new OrganizationResponse
				{
					ResponseName = request.RequestName,
					Results = new ParameterCollection
					{
						["id"] = request.Target.Id
					}
				};
			});

		var response = _organizationService.Execute(new TestCustomRequest
		{
			Target = account
		});

		response.Results["id"].Should().Be(account.Id);
		_organizationService.Simulated().Data().Get(account.ToEntityReference()).Should().BeSameAs(account);
	}

	[Test]
	public void Custom_Handler_With_Multiple_Parameters_Can_Be_Added()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact());
		_organizationService
			.Simulated()
			.CustomOrgRequests()
			.Add<ParameteredTestCustomRequest>((request, context) => 
			{
				var contact = request.Target.ToEntity<Contact>();
				var retrievedContact = context.Data.Get<Contact>(contact.Id);
				
				retrievedContact.FirstName = request.NewFirstName;
				context.Data.Update(retrievedContact);
			
				return new OrganizationResponse
				{
					ResponseName = request.RequestName
				}; 
			});

		var response = _organizationService.Execute(new ParameteredTestCustomRequest
		{
			Target = Arthur.Contact(),
			NewFirstName = "test"
		});
		
		var contact = _organizationService.Simulated().Data().Get<Contact>(Arthur.Contact().Id);
		
		response.ResponseName.Should().Be("cloudawesome_ParameteredTestCustom");
		contact.FirstName.Should().Be("test");
	}

	[Test]
	public void Add_Throws_When_Request_Already_Has_Built_In_Handler()
	{
		var sut = () => _organizationService
			.Simulated()
			.CustomOrgRequests()
			.Add<CreateRequest>((_, _) => new CreateResponse());

		sut.Should()
			.Throw<InvalidOperationException>()
			.WithMessage("*CreateRequest*already registered*");
	}

	[Test]
	public void Add_Throws_When_Custom_Request_Is_Already_Registered()
	{
		_organizationService
			.Simulated()
			.CustomOrgRequests()
			.Add<TestCustomRequest>((request, _) => new OrganizationResponse
			{
				ResponseName = request.RequestName
			});

		var sut = () => _organizationService
			.Simulated()
			.CustomOrgRequests()
			.Add<TestCustomRequest>((request, _) => new OrganizationResponse
			{
				ResponseName = request.RequestName
			});

		sut.Should()
			.Throw<InvalidOperationException>()
			.WithMessage("*TestCustomRequest*already registered*");
	}

	private sealed class TestCustomRequest : OrganizationRequest
	{
		public TestCustomRequest()
		{
			RequestName = "cloudawesome_TestCustom";
		}

		public Entity Target
		{
			get => (Entity)Parameters["Target"];
			init => Parameters["Target"] = value;
		}
	}
	
	private sealed class ParameteredTestCustomRequest : OrganizationRequest
	{
		public ParameteredTestCustomRequest()
		{
			RequestName = "cloudawesome_ParameteredTestCustom";
		}

		public Entity Target
		{
			get => (Entity)Parameters["Target"];
			init => Parameters["Target"] = value;
		}
		
		public string NewFirstName
		{
			get => (string)Parameters["NewFirstName"];
			init => Parameters["NewFirstName"] = value;
		}
	}
}
