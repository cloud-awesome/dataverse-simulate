using System;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.OrganizationRequestsTests;

[TestFixture]
public class UnsupportedOrganizationRequestTests
{
	private IOrganizationService _organizationService = null!;

	[SetUp]
	public void SetUp()
	{
		_organizationService = _organizationService.Simulate();
	}

	[Test]
	public void Execute_Unsupported_Sdk_Request_Throws_Clear_NotSupportedException()
	{
		var executeUnsupportedRequest = () => _organizationService.Execute(new ExecuteMultipleRequest
		{
			Requests = new OrganizationRequestCollection()
		});

		executeUnsupportedRequest.Should()
			.Throw<NotSupportedException>()
			.WithMessage("*ExecuteMultipleRequest*ExecuteMultiple*");
	}

	[Test]
	public void Execute_Unsupported_Custom_Request_Throws_Clear_NotSupportedException()
	{
		var executeUnsupportedRequest = () => _organizationService.Execute(new UnsupportedCustomRequest());

		executeUnsupportedRequest.Should()
			.Throw<NotSupportedException>()
			.WithMessage("*UnsupportedCustomRequest*cloudawesome_UnsupportedCustom*");
	}

	private sealed class UnsupportedCustomRequest : OrganizationRequest
	{
		public UnsupportedCustomRequest()
		{
			RequestName = "cloudawesome_UnsupportedCustom";
		}
	}
}
