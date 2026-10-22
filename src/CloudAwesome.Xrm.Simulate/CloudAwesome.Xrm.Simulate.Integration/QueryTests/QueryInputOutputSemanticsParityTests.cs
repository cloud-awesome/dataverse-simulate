using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using CloudAwesome.Xrm.Simulate.Metadata;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture]
[Category("Parity")]
[Category("QueryBase")]
[Category("QuerySemantics")]
public sealed class QueryInputOutputSemanticsParityTests : IntegrationBaseFixture
{
    [TestCase("QueryExpression", "empty")]
    [TestCase("QueryExpression", "selected")]
    [TestCase("QueryByAttribute", "empty")]
    [TestCase("QueryByAttribute", "selected")]
    [TestCase("FetchXML", "selected")]
    [TestCase("QueryExpression", "distinct")]
    [TestCase("QueryExpression", "distinct-id")]
    [TestCase("QueryExpression", "distinct-empty")]
    [TestCase("QueryExpression", "distinct-linked-only")]
    [TestCase("QueryExpression", "distinct-linked-filter")]
    [TestCase("FetchXML", "distinct")]
    [TestCase("FetchXML", "distinct-id")]
    public void Projection_And_Identity_Match_Dataverse(string form, string mode)
    {
        var lastName = UniqueLastName();
        var scenario = ContactsScenario<IReadOnlyList<ProjectionSnapshot>>(lastName, service =>
        {
            var result = service.RetrieveMultiple(BuildQuery(lastName, form, mode));
            var distinct = mode.StartsWith("distinct", StringComparison.Ordinal);
            var includesId = !distinct || mode is "distinct-id" or "distinct-empty" or "distinct-linked-only" or "distinct-linked-filter";
            var includesName = mode is "selected" or "distinct" or "distinct-id";
            var includesLinkedName = mode == "distinct-linked-only";
            var expectedKeys = new List<string>();
            if (includesId) expectedKeys.Add("contactid");
            if (includesName) expectedKeys.Add("firstname");
            if (includesLinkedName) expectedKeys.Add("linked.firstname");

            result.Entities.Count.Should().Be(mode == "distinct" ? 2 : 3);
            foreach (var entity in result.Entities)
            {
                entity.Attributes.Keys.Should().BeEquivalentTo(expectedKeys);
                if (includesId)
                {
                    entity.Id.Should().NotBeEmpty();
                    entity.GetAttributeValue<Guid>("contactid").Should().Be(entity.Id);
                }
                else entity.Id.Should().BeEmpty();

                if (includesLinkedName)
                {
                    var name = entity.GetAttributeValue<AliasedValue>("linked.firstname");
                    name.EntityLogicalName.Should().Be("contact");
                    name.AttributeLogicalName.Should().Be("firstname");
                    name.Value.Should().BeOfType<string>().Which.Should().BeOneOf("Ada", "Grace");
                }
            }

            return Snapshot(result.Entities);
        });

        DataverseParityHarness.Execute(scenario);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FetchXml_Default_And_All_Attributes_Return_Stored_Columns(bool allAttributes)
    {
        var lastName = UniqueLastName();
        var scenario = ContactsScenario<IReadOnlyList<ProjectionSnapshot>>(lastName, service =>
        {
            var columns = allAttributes ? "<all-attributes/>" : "";
            var result = service.RetrieveMultiple(new FetchExpression($"""
                                                                       <fetch><entity name='contact'>{columns}
                                                                         <filter><condition attribute='lastname' operator='eq' value='{lastName}'/></filter>
                                                                       </entity></fetch>
                                                                       """));
            result.Entities.Should().HaveCount(3);
            foreach (var entity in result.Entities)
            {
                entity.Attributes.Keys.Should()
                    .Contain(new[] { "contactid", "firstname", "lastname", "emailaddress1" });
                entity.Id.Should().Be(entity.GetAttributeValue<Guid>("contactid"));
            }

            // Live rows also have platform-generated attributes outside this seeded contract.
            return Snapshot(result.Entities.Select(entity => new Entity("contact")
            {
                Id = entity.Id, ["contactid"] = entity["contactid"], ["firstname"] = entity["firstname"],
                ["lastname"] = entity["lastname"], ["emailaddress1"] = entity["emailaddress1"]
            }));
        });

        DataverseParityHarness.Execute(scenario);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FetchXml_Or_And_Nested_Filters_Match_Dataverse(bool nested)
    {
        var lastName = UniqueLastName();
        var scenario = ContactsScenario<IReadOnlyList<ProjectionSnapshot>>(lastName, service =>
        {
            var nameFilter = nested
                ? "<filter type='and'><condition attribute='firstname' operator='eq' value='Ada'/><condition attribute='emailaddress1' operator='like' value='%@example.invalid'/></filter>"
                : "<condition attribute='firstname' operator='eq' value='Ada'/>";
            var result = service.RetrieveMultiple(new FetchExpression($"""
                                                                       <fetch><entity name='contact'><attribute name='firstname'/>
                                                                         <filter type='and'>
                                                                           <condition attribute='lastname' operator='eq' value='{lastName}'/>
                                                                           <filter type='or'>
                                                                             {nameFilter}
                                                                             <condition attribute='firstname' operator='eq' value='Missing'/>
                                                                           </filter>
                                                                         </filter>
                                                                       </entity></fetch>
                                                                       """));
            result.Entities.Should().HaveCount(2);
            result.Entities.Should().OnlyContain(entity => entity.GetAttributeValue<string>("firstname") == "Ada");
            return Snapshot(result.Entities);
        });

        DataverseParityHarness.Execute(scenario);
    }

    [TestCase("<entity name='ca_nonexistent_query_semantics_entity'/>", "metadata", "ca_nonexistent_query_semantics_entity")]
    [TestCase("<entity name='contact'><attribute name='ca_nonexistent_query_semantics_attribute'/></entity>",
        "metadata", "ca_nonexistent_query_semantics_attribute")]
    [TestCase(
        "<entity name='contact'><filter><filter type='or'><condition attribute='ca_nonexistent_query_semantics_attribute' operator='eq' value='x'/></filter></filter></entity>",
        "metadata", "ca_nonexistent_query_semantics_attribute")]
    [TestCase(
        "<entity name='contact'><filter><condition attribute='firstname' operator='equal' value='x'/></filter></entity>",
        "unsupported", "equal")]
    [TestCase(
        "<entity name='contact'><filter><condition attribute='firstname' operator='unknown' value='x'/></filter></entity>",
        "unsupported", "unknown")]
    [TestCase("<entity name='contact'>", "input", "Invalid XML")]
    [TestCase("", "input", "entityName")]
    public void Invalid_FetchXml_Is_Rejected_By_Both_Services(string entity, string errorKind,
        string expectedFaultFragment)
    {
        var scenario = new DataverseParityScenario<Exception?>
        {
            Name = nameof(Invalid_FetchXml_Is_Rejected_By_Both_Services),
            CreateSimulatorOptions = _ => new SimulatorOptions
            {
                Metadata = SimulatedMetadata.Load(Path.Combine(TestContext.CurrentContext.TestDirectory,
                    "TestMetadata", "entity-metadata.json"))
            },
            Act = service =>
                CaptureException(() => service.RetrieveMultiple(new FetchExpression($"<fetch>{entity}</fetch>"))),
            AssertEquivalent = (live, simulated) =>
            {
                // The simulator documents metadata/input exceptions; exact server fault envelopes remain separate work.
                var fault = live.Should().BeOfType<FaultException<OrganizationServiceFault>>().Subject;
                fault.Detail.Message.Should().Contain(expectedFaultFragment);
                if (errorKind == "metadata") simulated.Should().BeOfType<SimulatedMetadataException>();
                else if (errorKind == "unsupported") simulated.Should().BeOfType<NotSupportedException>();
                else simulated.Should().BeAssignableTo<ArgumentException>();
                TestContext.WriteLine($"Live rejection: {live!.Message}");
                TestContext.WriteLine($"Simulated rejection: {simulated!.Message}");
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    private static DataverseParityScenario<TResult> ContactsScenario<TResult>(string lastName,
        Func<IOrganizationService, TResult> act) => new()
    {
        Name = nameof(QueryInputOutputSemanticsParityTests),
        ArrangeLive = context =>
        {
            var contacts = new List<Entity>();
            foreach (var firstName in new[] { "Ada", "Ada", "Grace" })
            {
                var contact = new Entity("contact")
                {
                    ["firstname"] = firstName, ["lastname"] = lastName,
                    ["emailaddress1"] = $"{Guid.NewGuid():N}@example.invalid"
                };
                var id = context.Service.Create(contact);
                context.Cleanup.TrackForDelete("contact", id);
                contact.Id = id;
                contact["contactid"] = id;
                contacts.Add(contact);
            }

            context.State.Set("contacts", contacts);
        },
        ArrangeSimulated = context =>
        {
            foreach (var contact in context.State.Get<List<Entity>>("contacts"))
                context.Simulation.Data().Add(contact);
        },
        Act = act
    };

    private static QueryBase BuildQuery(string lastName, string form, string mode)
    {
        var columns = mode switch
        {
            "empty" or "distinct-empty" or "distinct-linked-only" or "distinct-linked-filter" => new ColumnSet(false),
            "distinct-id" => new ColumnSet("firstname", "contactid"),
            _ => new ColumnSet("firstname")
        };
        if (form == "QueryByAttribute")
            return new QueryByAttribute("contact")
            {
                ColumnSet = columns, Attributes = { "lastname" }, Values = { lastName }
            };
        if (form == "FetchXML")
        {
            var attributes = string.Concat(columns.Columns.Select(column => $"<attribute name='{column}'/>"));
            return new FetchExpression($"""
                                        <fetch distinct='{mode.StartsWith("distinct", StringComparison.Ordinal).ToString().ToLowerInvariant()}'>
                                          <entity name='contact'>{attributes}
                                            <filter><condition attribute='lastname' operator='eq' value='{lastName}'/></filter>
                                          </entity>
                                        </fetch>
                                        """);
        }

        var query = new QueryExpression("contact")
        {
            ColumnSet = columns, Distinct = mode.StartsWith("distinct", StringComparison.Ordinal)
        };
        query.Criteria.AddCondition("lastname", ConditionOperator.Equal, lastName);
        if (mode is "distinct-linked-only" or "distinct-linked-filter")
        {
            query.LinkEntities.Add(new LinkEntity("contact", "contact", "contactid", "contactid", JoinOperator.Inner)
            {
                EntityAlias = "linked",
                Columns = mode == "distinct-linked-only" ? new ColumnSet("firstname") : new ColumnSet(false)
            });
        }
        return query;
    }

    private static IReadOnlyList<ProjectionSnapshot> Snapshot(IEnumerable<Entity> entities) => entities
        .Select(entity => new ProjectionSnapshot(entity.Id, entity.GetAttributeValue<Guid?>("contactid"),
            entity.GetAttributeValue<string>("firstname"), entity.GetAttributeValue<string>("lastname"),
            entity.GetAttributeValue<string>("emailaddress1"),
            entity.GetAttributeValue<AliasedValue>("linked.firstname")?.Value as string,
            entity.Attributes.Keys.OrderBy(key => key).ToArray()))
        .ToList();

    private static Exception? CaptureException(Action act)
    {
        try
        {
            act();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    // Keep the prefix and full GUID within contact.lastname's 50-character limit.
    private static string UniqueLastName() => $"CASim QS {Guid.NewGuid():N}";

    private sealed record ProjectionSnapshot(Guid Id, Guid? PrimaryId, string? FirstName,
        string? LastName, string? Email, string? LinkedFirstName, string[] Attributes);
}
