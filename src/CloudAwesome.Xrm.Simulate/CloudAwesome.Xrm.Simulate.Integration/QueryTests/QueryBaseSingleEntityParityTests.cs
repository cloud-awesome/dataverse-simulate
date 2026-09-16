using System;
using System.Collections.Generic;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture]
[Category("Parity")]
[Category("QueryBase")]
public sealed class QueryBaseSingleEntityParityTests : IntegrationBaseFixture
{
    private const string ContactsKey = "contacts";
    private const string ContactLogicalName = "contact";
    private const string ContactIdAttribute = "contactid";
    private const string FirstNameAttribute = "firstname";
    private const string LastNameAttribute = "lastname";
    private const string EmailAddressAttribute = "emailaddress1";

    [Test(Description = "A single-entity QueryExpression should filter on an unreturned attribute and project only the selected contact columns.")]
    public void QueryExpression_Should_Filter_And_Project_Selected_Columns()
    {
        var lastName = UniqueLastName();

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactQueryResult>>
        {
            Name = nameof(QueryExpression_Should_Filter_And_Project_Selected_Columns),
            ArrangeLive = context =>
            {
                var contacts = new[]
                {
                    CreateLiveContact(context, "Ada", lastName, "ada@example.invalid"),
                    CreateLiveContact(context, "Grace", "Not " + lastName, "grace@example.invalid")
                };

                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service =>
            {
                var query = new QueryExpression(ContactLogicalName)
                {
                    ColumnSet = new ColumnSet(FirstNameAttribute),
                    Criteria = new FilterExpression(LogicalOperator.And)
                };

                query.Criteria.AddCondition(LastNameAttribute, ConditionOperator.Equal, lastName);

                return ProjectContactResults(service.RetrieveMultiple(query).Entities);
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A single-entity QueryExpression should order by an unreturned column before applying TopCount.")]
    public void QueryExpression_Should_Order_By_Unselected_Column_Before_TopCount()
    {
        var lastName = UniqueLastName();

        var scenario = new DataverseParityScenario<IReadOnlyList<string?>>
        {
            Name = nameof(QueryExpression_Should_Order_By_Unselected_Column_Before_TopCount),
            ArrangeLive = context =>
            {
                var contacts = new[]
                {
                    CreateLiveContact(context, "Zulu", lastName, "zulu@example.invalid"),
                    CreateLiveContact(context, "Alpha", lastName, "alpha@example.invalid"),
                    CreateLiveContact(context, "Bravo", lastName, "bravo@example.invalid")
                };

                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service =>
            {
                var query = new QueryExpression(ContactLogicalName)
                {
                    ColumnSet = new ColumnSet(FirstNameAttribute),
                    TopCount = 2,
                    Criteria = new FilterExpression(LogicalOperator.And),
                    Orders =
                    {
                        new OrderExpression(EmailAddressAttribute, OrderType.Ascending)
                    }
                };

                query.Criteria.AddCondition(LastNameAttribute, ConditionOperator.Equal, lastName);

                return service.RetrieveMultiple(query)
                    .Entities
                    .Select(entity => entity.GetAttributeValue<string>(FirstNameAttribute))
                    .ToList();
            },
            AssertEquivalent = (live, simulated) => simulated.Should().Equal(live)
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A single-entity QueryExpression should return TotalRecordCount only when PageInfo.ReturnTotalRecordCount is requested.")]
    public void QueryExpression_Should_Match_TotalRecordCount_Shape()
    {
        var lastName = UniqueLastName();

        var scenario = new DataverseParityScenario<TotalRecordCountResult>
        {
            Name = nameof(QueryExpression_Should_Match_TotalRecordCount_Shape),
            ArrangeLive = context =>
            {
                var contacts = new[]
                {
                    CreateLiveContact(context, "Ada", lastName, "ada@example.invalid"),
                    CreateLiveContact(context, "Grace", lastName, "grace@example.invalid")
                };

                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service =>
            {
                var queryWithoutCount = BuildLastNameQuery(lastName);
                var resultWithoutCount = service.RetrieveMultiple(queryWithoutCount);

                var queryWithCount = BuildLastNameQuery(lastName);
                queryWithCount.PageInfo = new PagingInfo
                {
                    ReturnTotalRecordCount = true
                };

                var resultWithCount = service.RetrieveMultiple(queryWithCount);

                return new TotalRecordCountResult(
                    resultWithoutCount.Entities.Count,
                    resultWithoutCount.TotalRecordCount,
                    resultWithoutCount.TotalRecordCountLimitExceeded,
                    resultWithCount.Entities.Count,
                    resultWithCount.TotalRecordCount,
                    resultWithCount.TotalRecordCountLimitExceeded);
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A single-entity QueryExpression with Distinct should compare projected column values rather than record identity.")]
    public void QueryExpression_Distinct_Should_Compare_Selected_Columns()
    {
        var lastName = UniqueLastName();

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactQueryResult>>
        {
            Name = nameof(QueryExpression_Distinct_Should_Compare_Selected_Columns),
            ArrangeLive = context =>
            {
                var contacts = new[]
                {
                    CreateLiveContact(context, "Ada", lastName, "ada.one@example.invalid"),
                    CreateLiveContact(context, "Ada", lastName, "ada.two@example.invalid"),
                    CreateLiveContact(context, "Grace", lastName, "grace@example.invalid")
                };

                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service =>
            {
                var query = BuildLastNameQuery(lastName);
                query.ColumnSet = new ColumnSet(FirstNameAttribute);
                query.Distinct = true;
                query.Orders.Add(new OrderExpression(FirstNameAttribute, OrderType.Ascending));

                return ProjectContactResults(service.RetrieveMultiple(query).Entities);
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A single-entity FetchXML query should filter on an unreturned attribute and project only the selected contact columns.")]
    public void FetchXml_Should_Filter_And_Project_Selected_Columns()
    {
        var lastName = UniqueLastName();

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactQueryResult>>
        {
            Name = nameof(FetchXml_Should_Filter_And_Project_Selected_Columns),
            ArrangeLive = context =>
            {
                var contacts = new[]
                {
                    CreateLiveContact(context, "Ada", lastName, "ada@example.invalid"),
                    CreateLiveContact(context, "Grace", "Not " + lastName, "grace@example.invalid")
                };

                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service => ProjectContactResults(
                service.RetrieveMultiple(new FetchExpression(ContactFetchXml(lastName, includeOrder: false))).Entities)
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A single-entity FetchXML query should order by an unreturned column before applying top.")]
    public void FetchXml_Should_Order_By_Unselected_Column_Before_Top()
    {
        var lastName = UniqueLastName();

        var scenario = new DataverseParityScenario<IReadOnlyList<string?>>
        {
            Name = nameof(FetchXml_Should_Order_By_Unselected_Column_Before_Top),
            ArrangeLive = context =>
            {
                var contacts = new[]
                {
                    CreateLiveContact(context, "Zulu", lastName, "zulu@example.invalid"),
                    CreateLiveContact(context, "Alpha", lastName, "alpha@example.invalid"),
                    CreateLiveContact(context, "Bravo", lastName, "bravo@example.invalid")
                };

                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service => service.RetrieveMultiple(new FetchExpression(ContactFetchXml(lastName, top: 2)))
                .Entities
                .Select(entity => entity.GetAttributeValue<string>(FirstNameAttribute))
                .ToList(),
            AssertEquivalent = (live, simulated) => simulated.Should().Equal(live)
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A single-entity FetchXML distinct query should compare projected attribute values rather than record identity.")]
    public void FetchXml_Distinct_Should_Compare_Selected_Columns()
    {
        var lastName = UniqueLastName();

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactQueryResult>>
        {
            Name = nameof(FetchXml_Distinct_Should_Compare_Selected_Columns),
            ArrangeLive = context =>
            {
                var contacts = new[]
                {
                    CreateLiveContact(context, "Ada", lastName, "ada.one@example.invalid"),
                    CreateLiveContact(context, "Ada", lastName, "ada.two@example.invalid"),
                    CreateLiveContact(context, "Grace", lastName, "grace@example.invalid")
                };

                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service => ProjectContactResults(
                service.RetrieveMultiple(new FetchExpression(
                    ContactFetchXml(lastName, distinct: true, includeOrder: false))).Entities)
        };

        DataverseParityHarness.Execute(scenario);
    }

    private static QueryExpression BuildLastNameQuery(string lastName)
    {
        var query = new QueryExpression(ContactLogicalName)
        {
            ColumnSet = new ColumnSet(FirstNameAttribute),
            Criteria = new FilterExpression(LogicalOperator.And)
        };

        query.Criteria.AddCondition(LastNameAttribute, ConditionOperator.Equal, lastName);

        return query;
    }

    private static string ContactFetchXml(
        string lastName,
        int? top = null,
        bool distinct = false,
        bool includeOrder = true)
    {
        var topAttribute = top.HasValue ? $" top='{top.Value}'" : string.Empty;
        var distinctAttribute = distinct ? "true" : "false";
        var order = includeOrder
            ? $"<order attribute='{EmailAddressAttribute}' descending='false' />"
            : string.Empty;

        return $"""
                <fetch version='1.0' mapping='logical' distinct='{distinctAttribute}'{topAttribute}>
                  <entity name='{ContactLogicalName}'>
                    <attribute name='{FirstNameAttribute}' />
                    <filter type='and'>
                      <condition attribute='{LastNameAttribute}' operator='eq' value='{lastName}' />
                    </filter>
                    {order}
                  </entity>
                </fetch>
                """;
    }

    private static IReadOnlyList<ContactQueryResult> ProjectContactResults(IEnumerable<Entity> entities)
    {
        return entities
            .Select(entity => new ContactQueryResult(
                entity.GetAttributeValue<string>(FirstNameAttribute),
                entity.Contains(LastNameAttribute),
                entity.Contains(EmailAddressAttribute)))
            .ToList();
    }

    private static void SeedSimulationFromState(SimulatedDataverseScenarioContext context)
    {
        foreach (var contact in context.State.Get<IReadOnlyList<ContactSeed>>(ContactsKey))
        {
            context.Simulation.Data().Add(Contact(contact));
        }
    }

    private static ContactSeed CreateLiveContact(
        LiveDataverseScenarioContext context,
        string firstName,
        string lastName,
        string emailAddress)
    {
        var contact = new Entity(ContactLogicalName)
        {
            Attributes =
            {
                [FirstNameAttribute] = firstName,
                [LastNameAttribute] = lastName,
                [EmailAddressAttribute] = emailAddress
            }
        };

        var id = context.Service.Create(contact);
        context.Cleanup.TrackForDelete(ContactLogicalName, id);

        return new ContactSeed(id, firstName, lastName, emailAddress);
    }

    private static Entity Contact(ContactSeed seed)
    {
        var contact = new Entity(ContactLogicalName) { Id = seed.Id };
        contact[ContactIdAttribute] = seed.Id;
        contact[FirstNameAttribute] = seed.FirstName;
        contact[LastNameAttribute] = seed.LastName;
        contact[EmailAddressAttribute] = seed.EmailAddress;

        return contact;
    }

    private static string UniqueLastName()
    {
        return $"CASim {Guid.NewGuid():N}";
    }

    private sealed record ContactSeed(Guid Id, string FirstName, string LastName, string EmailAddress);

    private sealed record ContactQueryResult(
        string? FirstName,
        bool ContainsLastName,
        bool ContainsEmailAddress);

    private sealed record TotalRecordCountResult(
        int CountWithoutTotal,
        int TotalWithoutTotal,
        bool LimitExceededWithoutTotal,
        int CountWithTotal,
        int TotalWithTotal,
        bool LimitExceededWithTotal);
}
