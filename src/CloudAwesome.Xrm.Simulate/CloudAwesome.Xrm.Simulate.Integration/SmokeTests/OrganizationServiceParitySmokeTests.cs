using System;
using System.Collections.Generic;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture]
[Category("ParitySmoke")]
public sealed class OrganizationServiceParitySmokeTests : IntegrationBaseFixture
{
    private const string ContactsKey = "contacts";
    private const string WhoAmIKey = "whoami";
    private const string ContactLogicalName = "contact";
    private static readonly Guid NonEmptyGuidMarker = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Test]
    public void Create_Returns_A_Non_Empty_Id()
    {
        var lastName = UniqueLastName(nameof(Create_Returns_A_Non_Empty_Id));

        var scenario = new DataverseParityScenario<CreatedRecordResult>
        {
            Name = nameof(Create_Returns_A_Non_Empty_Id),
            Act = service =>
            {
                var id = service.Create(Contact("Parity", lastName));
                return new CreatedRecordResult(id);
            },
            AfterLiveAct = (context, result) =>
                context.Cleanup.TrackForDelete(ContactLogicalName, result.Id),
            Normalize = result => result with
            {
                Id = result.Id == Guid.Empty ? Guid.Empty : NonEmptyGuidMarker
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void RetrieveMultiple_QueryExpression_Returns_Matching_Contacts()
    {
        var lastName = UniqueLastName(nameof(RetrieveMultiple_QueryExpression_Returns_Matching_Contacts));

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactResult>>
        {
            Name = nameof(RetrieveMultiple_QueryExpression_Returns_Matching_Contacts),
            ArrangeLive = context =>
            {
                var contacts = new List<ContactSeed>
                {
                    CreateLiveContact(context, "Ada", lastName),
                    CreateLiveContact(context, "Grace", lastName),
                    CreateLiveContact(context, "Katherine", lastName)
                };

                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = context =>
            {
                foreach (var contact in context.State.Get<IReadOnlyList<ContactSeed>>(ContactsKey))
                {
                    context.Simulation.Data().Add(Contact(contact.Id, contact.FirstName, contact.LastName));
                }
            },
            Act = service =>
            {
                var query = new QueryExpression(ContactLogicalName)
                {
                    ColumnSet = new ColumnSet("firstname", "lastname"),
                    Criteria = new FilterExpression(LogicalOperator.And)
                };

                query.Criteria.AddCondition("lastname", ConditionOperator.Equal, lastName);
                query.Orders.Add(new OrderExpression("firstname", OrderType.Ascending));

                return service.RetrieveMultiple(query)
                    .Entities
                    .Select(e => new ContactResult(
                        e.GetAttributeValue<string>("firstname"),
                        e.GetAttributeValue<string>("lastname")))
                    .ToList();
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void WhoAmI_Returns_Configured_User_BusinessUnit_And_Organization()
    {
        var scenario = new DataverseParityScenario<WhoAmIResult>
        {
            Name = nameof(WhoAmI_Returns_Configured_User_BusinessUnit_And_Organization),
            ArrangeLive = context =>
                context.State.Set(WhoAmIKey, WhoAmI(context.Service)),
            CreateSimulatorOptions = context =>
            {
                var whoAmI = context.State.Get<WhoAmIResult>(WhoAmIKey);

                return new SimulatorOptions
                {
                    AuthenticatedUser = new Entity("systemuser") { Id = whoAmI.UserId },
                    BusinessUnit = new Entity("businessunit") { Id = whoAmI.BusinessUnitId },
                    Organization = new Entity("organization") { Id = whoAmI.OrganizationId }
                };
            },
            Act = WhoAmI
        };

        DataverseParityHarness.Execute(scenario);
    }

    private static ContactSeed CreateLiveContact(
        LiveDataverseScenarioContext context,
        string firstName,
        string lastName)
    {
        var id = context.Service.Create(Contact(firstName, lastName));
        context.Cleanup.TrackForDelete(ContactLogicalName, id);
        return new ContactSeed(id, firstName, lastName);
    }

    private static Entity Contact(string firstName, string lastName)
    {
        return new Entity(ContactLogicalName)
        {
            Attributes =
            {
                ["firstname"] = firstName,
                ["lastname"] = lastName
            }
        };
    }

    private static Entity Contact(Guid id, string firstName, string lastName)
    {
        var contact = Contact(firstName, lastName);
        contact.Id = id;
        return contact;
    }

    private static WhoAmIResult WhoAmI(IOrganizationService service)
    {
        var response = (WhoAmIResponse)service.Execute(new WhoAmIRequest());
        return new WhoAmIResult(response.UserId, response.BusinessUnitId, response.OrganizationId);
    }

    private static string UniqueLastName(string testName)
    {
        return $"CASim {Guid.NewGuid():N}";
    }

    private sealed record CreatedRecordResult(Guid Id);

    private sealed record ContactSeed(Guid Id, string FirstName, string LastName);

    private sealed record ContactResult(string? FirstName, string? LastName);

    private sealed record WhoAmIResult(Guid UserId, Guid BusinessUnitId, Guid OrganizationId);
}
