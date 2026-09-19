using System;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
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
	public void Execute_Unsupported_Request_Throws_Clear_NotSupportedException()
	{
		var executeUnsupportedRequest = () => _organizationService.Execute(new OrganizationRequest
		{
			RequestName = "cloudawesome_UnsupportedSdkLikeRequest"
		});

		executeUnsupportedRequest.Should()
			.Throw<NotSupportedException>()
			.WithMessage("*OrganizationRequest*cloudawesome_UnsupportedSdkLikeRequest*");
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
