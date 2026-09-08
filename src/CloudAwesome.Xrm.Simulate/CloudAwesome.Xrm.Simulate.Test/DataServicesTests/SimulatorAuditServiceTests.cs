using System.Linq;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.DataServicesTests;

[TestFixture]
public class SimulatorAuditServiceTests
{
	private IOrganizationService _organizationService = null!;

	[SetUp]
	public void SetUp()
	{
		_organizationService = _organizationService.Simulate();
	}
	
	[Test]
	public void Initialise_Simulator_Audit_With_No_Events_Should_Return_Empty_List()
	{
		var audits = _organizationService.Simulated().Audit().Get();
		audits.Count.Should().Be(0);
	}

	[Test]
	public void Create_Request_Should_Add_Single_Audit_Record()
	{
		var createdRecordId = _organizationService.Create(Arthur.Contact());
		
		var audits = _organizationService.Simulated().Audit().Get();

		audits.Count.Should().Be(1);
		var audit = audits.SingleOrDefault();

		audit?.Id.Should().Be(createdRecordId);
		audit?.Message.Should().Be("Create");
		audit?.EntityLogicalName.Should().Be(Arthur.Contact().LogicalName);
	}
	
	[Test]
	public void Two_Create_Request_Should_Add_Two_Audit_Records()
	{
		_organizationService.Create(Arthur.Contact());
		_organizationService.Create(Arthur.Account());
		
		var audits = _organizationService.Simulated().Audit().Get();

		audits.Count.Should().Be(2);
	}
	
	[Test]
	public void Audit_From_Create_Request_Should_Be_Retrievable_By_id()
	{
		var id = _organizationService.Create(Arthur.Contact());
		
		var audits = _organizationService.Simulated().Audit().Get(id);

		audits.Count.Should().Be(1);
	}
	
	[Test]
	public void Retrieve_Request_Should_Add_Single_Audit_Record()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact());
		
		var retrievedEntity = 
			_organizationService.Retrieve(Arthur.Contact().LogicalName, Arthur.Contact().Id, new ColumnSet());
		
		var audits = _organizationService.Simulated().Audit().Get();

		audits.Count.Should().Be(1);
		var audit = audits.SingleOrDefault();

		audit?.Id.Should().Be(retrievedEntity.Id);
		audit?.Message.Should().Be("Retrieve");
		audit?.EntityLogicalName.Should().Be(retrievedEntity.LogicalName);
	}
	
	[Test]
	public void Audits_Should_Be_Retrievable_By_Message()
	{
		_organizationService.Simulated().Data().Add(Arthur.Contact());
		
		var retrievedEntity = 
			_organizationService.Retrieve(Arthur.Contact().LogicalName, Arthur.Contact().Id, new ColumnSet());
		
		_organizationService.Create(Siobhan.Contact());
		_organizationService.Create(Arthur.Account());

		var allAudits = _organizationService.Simulated().Audit().Get();
		allAudits.Count.Should().Be(3);
		
		var audits = _organizationService.Simulated().Audit().Get("Create");
		audits.Count.Should().Be(2);
	}
}
