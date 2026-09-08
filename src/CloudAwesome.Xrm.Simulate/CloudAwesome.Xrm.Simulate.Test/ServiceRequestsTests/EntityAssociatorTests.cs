using System;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using CloudAwesome.Xrm.Simulate.Test.TestEntities;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
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
    public void Associate_Request_Should_Not_Mutate_Target_RelatedEntities()
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

        contact.RelatedEntities.Count.Should().Be(0);

        var relationships = _organizationService.Simulated()
            .Data().GetRelationships(Arthur.Contact().ToEntityReference(), relationship);

        relationships.Should().ContainSingle(r =>
            r.TargetLogicalName == Contact.EntityLogicalName &&
            r.TargetId == Arthur.Contact().Id &&
            r.RelatedLogicalName == Arthur.Account().LogicalName &&
            r.RelatedId == Arthur.Account().Id);
    }

    [Test]
    public void Associate_Request_Should_Not_Duplicate_Existing_Association()
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
        _organizationService.Associate(Contact.EntityLogicalName, Arthur.Contact().Id,
            relationship, relatedEntities);

        _organizationService.Simulated()
            .Data().GetRelationships(Arthur.Contact().ToEntityReference(), relationship)
            .Should().ContainSingle();
    }

    [Test]
    public void Disassociate_Request_Should_Remove_Association()
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
        _organizationService.Disassociate(Contact.EntityLogicalName, Arthur.Contact().Id,
            relationship, relatedEntities);

        var contact = _organizationService.Simulated()
            .Data().Get<Contact>(Arthur.Contact().Id);

        contact.RelatedEntities.Count.Should().Be(0);
        _organizationService.Simulated()
            .Data().GetRelationships(Arthur.Contact().ToEntityReference(), relationship)
            .Should().BeEmpty();
    }

    [Test]
    public void Execute_AssociateRequest_Should_Associate_Entities()
    {
        _organizationService.Simulated().Data().Add(Arthur.Account());
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var response = _organizationService.Execute(new AssociateRequest
        {
            Target = Arthur.Contact().ToEntityReference(),
            Relationship = new Relationship(Account.Fields.Account_Primary_Contact),
            RelatedEntities = new EntityReferenceCollection
            {
                Arthur.Account().ToEntityReference()
            }
        });

        response.ResponseName.Should().Be("Associate");
        _organizationService.Simulated()
            .Data().GetRelationships(Arthur.Contact().ToEntityReference(), new Relationship(Account.Fields.Account_Primary_Contact))
            .Should().ContainSingle();
    }

    [Test]
    public void Execute_DisassociateRequest_Should_Disassociate_Entities()
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

        var response = _organizationService.Execute(new DisassociateRequest
        {
            Target = Arthur.Contact().ToEntityReference(),
            Relationship = relationship,
            RelatedEntities = relatedEntities
        });

        response.ResponseName.Should().Be("Disassociate");
        _organizationService.Simulated()
            .Data().GetRelationships(Arthur.Contact().ToEntityReference(), relationship)
            .Should().BeEmpty();
    }

    [Test]
    public void SetRelationship_Should_Arrange_Association_Without_Calling_Associate()
    {
        _organizationService.Simulated().Data().Add(Arthur.Account());
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var relationship = new Relationship(Account.Fields.Account_Primary_Contact);

        _organizationService.Simulated().Data().SetRelationship(
            Arthur.Contact().ToEntityReference(),
            relationship,
            new EntityReferenceCollection
            {
                Arthur.Account().ToEntityReference()
            });

        _organizationService.Simulated()
            .Data().GetRelationships(Arthur.Contact().ToEntityReference(), relationship)
            .Should().ContainSingle();
    }

    [Test]
    public void SetRelationship_Should_Throw_If_Target_Record_Does_Not_Exist()
    {
        _organizationService.Simulated().Data().Add(Arthur.Account());

        var setRelationship = () => _organizationService.Simulated().Data().SetRelationship(
            Arthur.Contact().ToEntityReference(),
            new Relationship(Account.Fields.Account_Primary_Contact),
            new EntityReferenceCollection
            {
                Arthur.Account().ToEntityReference()
            });

        setRelationship.Should().Throw<Exception>();
    }

    [Test]
    public void SetRelationship_Should_Throw_If_Related_Record_Does_Not_Exist()
    {
        _organizationService.Simulated().Data().Add(Arthur.Contact());

        var setRelationship = () => _organizationService.Simulated().Data().SetRelationship(
            Arthur.Contact().ToEntityReference(),
            new Relationship(Account.Fields.Account_Primary_Contact),
            new EntityReferenceCollection
            {
                Arthur.Account().ToEntityReference()
            });

        setRelationship.Should().Throw<Exception>();
    }
}
