using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather.QueryTests;

[TestFixture]
[Category("Parity")]
[Category("PagingAggregates")]
public sealed class PagingAggregateParityTests : IntegrationBaseFixture
{
    [TestCase("numberofemployees", false)]
    [TestCase("revenue", false)]
    [TestCase("address1_latitude", false)]
    [TestCase("exchangerate", false)]
    [TestCase("numberofemployees", true)]
    [TestCase("revenue", true)]
    public void Aggregate_Sdk_Types_Identity_And_Values_Match(string attribute, bool queryExpression)
    {
        RunAggregate(attribute, queryExpression, empty: false, grouped: false, limit: null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Empty_Ungrouped_Aggregates_Match(bool queryExpression) => RunAggregate("numberofemployees", queryExpression, true, false, null);

    [TestCase("revenue")]
    [TestCase("address1_latitude")]
    [TestCase("exchangerate")]
    public void Empty_Aggregates_Preserve_Source_Numeric_Type(string attribute) => RunAggregate(attribute, false, true, false, null);

    [Test]
    public void Distinct_Column_Count_Excludes_Nulls_And_Duplicates() => RunAggregate("numberofemployees", false, false, false, null, distinct: true);

    [TestCase(false)]
    [TestCase(true)]
    public void Grouped_Aggregates_Include_Null_Group_And_Sdk_Wrappers(bool empty) => RunAggregate("numberofemployees", false, empty, true, null);

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Explicit_Aggregate_Limit_Uses_Live_Input_Boundary(int limit) => RunAggregate("numberofemployees", false, false, false, limit);

    private static void RunAggregate(string attribute, bool queryExpression, bool empty, bool grouped, int? limit, bool distinct = false)
    {
        var prefix = $"CAPage {Guid.NewGuid():N}";
        Entity[] seeds = [];
        DataverseParityHarness.Execute(new DataverseParityScenario<RowShape[]>
        {
            Name = $"Aggregate_{attribute}_qe_{queryExpression}_empty_{empty}_group_{grouped}_limit_{limit}",
            ArrangeLive = context =>
            {
                var rows = new List<Entity>();
                for (var i = 0; i < 3; i++)
                {
                    var row = new Entity("account") { ["name"] = prefix };
                    if (i < 2)
                    {
                        row["numberofemployees"] = distinct ? 1 : i + 1;
                        row["revenue"] = new Money(i == 0 ? 10.25m : 20.50m);
                        row["address1_latitude"] = i == 0 ? 1.25d : 2.50d;
                        row["address1_city"] = "City";
                    }
                    row.Id = context.Service.Create(row);
                    context.Cleanup.TrackForDelete("account", row.Id);
                    rows.Add(context.Service.Retrieve("account", row.Id, new ColumnSet("name", "numberofemployees", "revenue", "address1_latitude", "exchangerate", "address1_city")));
                }
                seeds = rows.ToArray();
            },
            ArrangeSimulated = context =>
            {
                foreach (var seed in seeds) context.Simulation.Data().Add(seed);
            },
            Act = service =>
            {
                var name = empty ? prefix + " absent" : prefix;
                // Explicit limits select arbitrary input rows; only assert their count, not which values were sampled.
                var operations = limit.HasValue ? new[] { "count" } : new[] { "sum", "avg", "min", "max", "count", "countcolumn" };
                QueryBase query;
                if (queryExpression)
                {
                    var expression = new QueryExpression("account") { ColumnSet = new ColumnSet(false) };
                    expression.Criteria.AddCondition("name", ConditionOperator.Equal, name);
                    foreach (var operation in operations)
                        expression.ColumnSet.AttributeExpressions.Add(new XrmAttributeExpression(attribute, Enum.Parse<XrmAggregateType>(operation, true), operation));
                    query = expression;
                }
                else
                {
                    var attributes = string.Concat(operations.Select(operation => $"<attribute name='{attribute}' alias='{operation}' aggregate='{operation}'/>"));
                    if (distinct) attributes += $"<attribute name='{attribute}' alias='distinct_count' aggregate='countcolumn' distinct='true'/>";
                    if (grouped) attributes += "<attribute name='address1_city' alias='city' groupby='true'/>";
                    query = new FetchExpression($"<fetch aggregate='true'{(limit.HasValue ? $" aggregatelimit='{limit}'" : "")}><entity name='account'>{attributes}<filter><condition attribute='name' operator='eq' value='{name}'/></filter>{(grouped ? "<order alias='city'/>" : "")}</entity></fetch>");
                }
                return service.RetrieveMultiple(query).Entities.Select(Snapshot).ToArray();
            },
            AssertEquivalent = (live, simulated) =>
            {
                simulated.Should().BeEquivalentTo(live, options => options.WithStrictOrdering());
            }
        });
    }

    [TestCase("QueryExpression", false)]
    [TestCase("FetchXML", false)]
    [TestCase("QueryByAttribute", false)]
    [TestCase("QueryExpression", true)]
    [TestCase("FetchXML", true)]
    [TestCase("QueryByAttribute", true)]
    [TestCase("QueryExpression", false, -1)]
    [TestCase("QueryExpression", false, 0)]
    [TestCase("QueryByAttribute", false, -1)]
    [TestCase("FetchXML", false, 0)]
    public void Multi_Page_Continuation_And_Metadata_Match(string form, bool cookie, int count = 2)
    {
        var prefix = $"CAPage {Guid.NewGuid():N}";
        Entity[] seeds = [];
        DataverseParityHarness.Execute(new DataverseParityScenario<PageShape[]>
        {
            Name = $"Paging_{form}_cookie_{cookie}",
            ArrangeLive = context =>
            {
                seeds = Enumerable.Range(0, 5).Select(i => new Entity("account") { ["name"] = prefix, ["accountnumber"] = $"{i:D2}" }).ToArray();
                foreach (var row in seeds)
                {
                    row.Id = context.Service.Create(row);
                    context.Cleanup.TrackForDelete("account", row.Id);
                }
            },
            ArrangeSimulated = context =>
            {
                foreach (var row in seeds) { row["accountid"] = row.Id; context.Simulation.Data().Add(row); }
            },
            Act = service =>
            {
                var pages = new List<PageShape>();
                string? previousCookie = null;
                for (var page = 1; page <= 4; page++)
                {
                    var query = PagingQuery(form, prefix, page, cookie ? previousCookie : null, count);
                    var results = service.RetrieveMultiple(query);
                    pages.Add(new PageShape(results.Entities.Select(e => e.Id).ToArray(), results.MoreRecords,
                        results.TotalRecordCount, results.TotalRecordCountLimitExceeded, !string.IsNullOrWhiteSpace(results.PagingCookie)));
                    previousCookie = results.PagingCookie;
                }
                return pages.ToArray();
            },
            AssertEquivalent = (live, simulated) =>
            {
                simulated.Should().BeEquivalentTo(live, options => options.WithStrictOrdering());
            }
        });
    }

    [TestCase("QueryExpression")]
    [TestCase("FetchXML")]
    [TestCase("QueryByAttribute")]
    public void Cookie_Continuation_Survives_Changed_Result_Set_And_Missing_Boundary(string form)
    {
        var prefix = $"CAPage {Guid.NewGuid():N}";
        Entity[] seeds = [];
        var insertedId = Guid.NewGuid();
        Action<Guid> track = _ => { };
        DataverseParityHarness.Execute(new DataverseParityScenario<PageShape>
        {
            Name = $"Cookie_mutation_{form}",
            ArrangeLive = context =>
            {
                track = id => context.Cleanup.TrackForDelete("account", id);
                seeds = Enumerable.Range(1, 5).Select(i => new Entity("account") { ["name"] = prefix, ["accountnumber"] = $"{i:D2}" }).ToArray();
                foreach (var row in seeds)
                {
                    row.Id = context.Service.Create(row);
                    track(row.Id);
                }
            },
            ArrangeSimulated = context =>
            {
                foreach (var row in seeds) { row["accountid"] = row.Id; context.Simulation.Data().Add(row); }
            },
            Act = service =>
            {
                var first = service.RetrieveMultiple(PagingQuery(form, prefix, 1, null));
                first.PagingCookie.Should().NotBeNullOrWhiteSpace();
                service.Create(new Entity("account", insertedId) { ["name"] = prefix, ["accountnumber"] = "00" });
                track(insertedId);
                // Remove both the earlier row and the boundary row from the matching set.
                foreach (var row in first.Entities)
                    service.Update(new Entity("account", row.Id) { ["name"] = prefix + " excluded" });
                var next = service.RetrieveMultiple(PagingQuery(form, prefix, 2, first.PagingCookie));
                next.Entities.Select(row => row.Id).Should().Equal(seeds[2].Id, seeds[3].Id);
                return new PageShape(next.Entities.Select(row => row.Id).ToArray(), next.MoreRecords,
                    next.TotalRecordCount, next.TotalRecordCountLimitExceeded, !string.IsNullOrEmpty(next.PagingCookie));
            }
        });
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(50001)]
    public void Invalid_Aggregate_Limits_Are_Rejected(int limit)
    {
        DataverseParityHarness.Execute(new DataverseParityScenario<(int Code, string Message)>
        {
            Name = $"Invalid_aggregate_limit_{limit}",
            Act = service =>
            {
                try
                {
                    service.RetrieveMultiple(new FetchExpression($"<fetch aggregate='true' aggregatelimit='{limit}'><entity name='account'><attribute name='accountid' alias='n' aggregate='count'/><filter><condition attribute='name' operator='eq' value='CAPage absent fixture'/></filter></entity></fetch>"));
                }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    fault.Detail.ErrorCode.Should().Be(-2147220989);
                    fault.Detail.Message.Should().Be("Expected value between 1 and 50000 inclusive.");
                    return (fault.Detail.ErrorCode, fault.Detail.Message);
                }
                throw new AssertionException("Invalid aggregate limit unexpectedly succeeded.");
            }
        });
    }

    [TestCase(2, -1, false)]
    [TestCase(2, 1, true)]
    [TestCase(0, 0, true)]
    public void Invalid_Paging_And_Top_Count_Combinations_Are_Rejected(int count, int page, bool top)
    {
        var query = new QueryExpression("account") { ColumnSet = new ColumnSet("accountid"), PageInfo = new PagingInfo { Count = count, PageNumber = page, ReturnTotalRecordCount = true } };
        if (top) query.TopCount = 1;
        AssertRejection(query, "page|paging|count|top|negative|positive",
            top ? -2147220891 : -2147220989,
            top ? "The Top.Count = 1 can't be specified with pagingInfo" : "A non valid page number was received: -1");
    }

    private static void AssertRejection(QueryBase query, string messagePattern, int? errorCode = null, string? message = null)
    {
        DataverseParityHarness.Execute(new DataverseParityScenario<bool>
        {
            Name = "Query_input_rejection",
            Act = service =>
            {
                try { service.RetrieveMultiple(query); }
                catch (FaultException<OrganizationServiceFault> fault)
                {
                    TestContext.Out.WriteLine($"Live query rejection: {fault.Detail.ErrorCode}: {fault.Detail.Message}");
                    if (errorCode.HasValue) fault.Detail.ErrorCode.Should().Be(errorCode.Value);
                    if (message is not null) fault.Detail.Message.Should().Be(message);
                    System.Text.RegularExpressions.Regex.IsMatch(fault.Detail.Message, messagePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                        .Should().BeTrue($"the fault must relate to the invalid query, not authentication or an unrelated outage: {fault.Detail.Message}");
                    return true;
                }
                catch (ArgumentException) when (!errorCode.HasValue) { return true; }
                throw new AssertionException("Invalid query unexpectedly succeeded.");
            }
        });
    }

    private static QueryBase PagingQuery(string form, string prefix, int page, string? cookie, int count = 2)
    {
        var info = new PagingInfo { PageNumber = page, Count = count, ReturnTotalRecordCount = true, PagingCookie = cookie };
        if (form == "FetchXML")
            return new FetchExpression($"<fetch page='{page}' count='{count}' returntotalrecordcount='true'{(cookie is null ? "" : $" paging-cookie='{SecurityElement.Escape(cookie)}'")}><entity name='account'><attribute name='accountid'/><order attribute='accountnumber'/><order attribute='accountid'/><filter><condition attribute='name' operator='eq' value='{prefix}'/></filter></entity></fetch>");
        if (form == "QueryByAttribute")
        {
            var query = new QueryByAttribute("account") { ColumnSet = new ColumnSet("accountid"), PageInfo = info };
            query.AddAttributeValue("name", prefix);
            query.AddOrder("accountnumber", OrderType.Ascending);
            query.AddOrder("accountid", OrderType.Ascending);
            return query;
        }
        var expression = new QueryExpression("account") { ColumnSet = new ColumnSet("accountid"), PageInfo = info };
        expression.Criteria.AddCondition("name", ConditionOperator.Equal, prefix);
        expression.AddOrder("accountnumber", OrderType.Ascending);
        expression.AddOrder("accountid", OrderType.Ascending);
        return expression;
    }

    private static RowShape Snapshot(Entity row) => new(row.LogicalName, row.Id,
        row.Attributes.OrderBy(pair => pair.Key).Select(pair =>
        {
            var alias = pair.Value as AliasedValue;
            var value = alias is null ? pair.Value : alias.Value;
            var scalar = value is Money money ? money.Value : value;
            return new AttributeShape(pair.Key, pair.Value?.GetType().Name, alias?.EntityLogicalName, alias?.AttributeLogicalName,
                value?.GetType().Name, scalar is null ? null : scalar is decimal number ? number.ToString("G29", CultureInfo.InvariantCulture) : Convert.ToString(scalar, CultureInfo.InvariantCulture));
        }).ToArray());

    private sealed record RowShape(string EntityName, Guid Id, AttributeShape[] Attributes);
    private sealed record AttributeShape(string Key, string? Type, string? Entity, string? Attribute, string? ValueType, string? Value);
    private sealed record PageShape(Guid[] Ids, bool More, int Total, bool LimitExceeded, bool Cookie);
}
