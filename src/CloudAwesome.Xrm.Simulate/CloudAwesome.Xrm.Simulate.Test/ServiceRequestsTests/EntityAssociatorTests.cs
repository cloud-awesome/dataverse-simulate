using System.Linq;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests;

[TestFixture]
public class EntityAssociatorTests
{
	private IOrganizationService _organizationService = null!;
	
	[SetUp]
	public void Setup()
	{
		_organizationService = _organizationService.Simulate();
	}
	
	[Test]
	public void Associate_Request_Should_Associate_Entities()
	{
		_organizationService.Simulated().Data().Add(Arthur.Account());
		_organizationService.Simulated().Data().Add(Arthur.Contact());
		
		var relationship = new Relationship(Account.Fields.Account_Primary_Contact);
		var relatedEntities = new EntityReferenceCollection
		{
			Arthur.Account().ToEntityReference()
		};

		_organizationService.Associate(Contact.EntityLogicalName, Arthur.Contact().Id, 
			relationship, relatedEntities);
		
		var contact = _organizationService.Simulated()
			.Data().Get<Contact>(Arthur.Contact().Id);
		
		contact.RelatedEntities.Count.Should().Be(1);
		contact.RelatedEntities[relationship].Entities.SingleOrDefault()?.Id.Should().Be(Arthur.Account().Id);
	}
}