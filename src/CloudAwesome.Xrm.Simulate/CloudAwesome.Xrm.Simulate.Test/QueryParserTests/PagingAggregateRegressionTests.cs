using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.Metadata;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.QueryParserTests;

[TestFixture]
public sealed class PagingAggregateRegressionTests
{
    [TestCase("QueryExpression", 0)]
    [TestCase("QueryExpression", -1)]
    [TestCase("QueryExpression", 5001)]
    [TestCase("FetchXML", 0)]
    [TestCase("FetchXML", 5001)]
    [TestCase("QueryByAttribute", 0)]
    [TestCase("QueryByAttribute", -1)]
    [TestCase("QueryByAttribute", 5001)]
    public void Page_Size_Is_Defaulted_And_Capped_At_5000(string form, int size)
    {
        var service = Service(5001);
        var result = service.RetrieveMultiple(Page(form, 1, size));
        result.Entities.Should().HaveCount(5000);
        result.MoreRecords.Should().BeTrue();
        result.TotalRecordCount.Should().Be(5000);
        result.TotalRecordCountLimitExceeded.Should().BeTrue();
        var next = service.RetrieveMultiple(Page(form, 2, size, result.PagingCookie));
        next.Entities.Should().ContainSingle();
        next.MoreRecords.Should().BeFalse();
        next.PagingCookie.Should().NotBeNullOrWhiteSpace();
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(5000, false)]
    [TestCase(5001, true)]
    public void Total_Count_Limit_Flag_Changes_Only_After_Boundary(int count, bool exceeded)
    {
        var result = Service(count).RetrieveMultiple(Page("QueryExpression", 1, 1));
        result.TotalRecordCount.Should().Be(Math.Min(count, 5000));
        result.TotalRecordCountLimitExceeded.Should().Be(exceeded);
    }

    [TestCase("QueryExpression")]
    [TestCase("FetchXML")]
    [TestCase("QueryByAttribute")]
    public void Cookie_Uses_Unselected_Ordering_Values_After_Anchor_Deletion(string form)
    {
        var service = Service(5);
        var data = service.Simulated().Data();
        var first = service.RetrieveMultiple(Page(form, 1, 2));
        first.Entities.All(row => !row.Contains("ca_sort")).Should().BeTrue();
        foreach (var row in first.Entities) data.Delete("ca_item", row.Id);
        data.Add(Item(0));
        var next = service.RetrieveMultiple(Page(form, 2, 2, first.PagingCookie));
        next.Entities.Select(row => row.GetAttributeValue<string>("ca_name")).Should().Equal("Item 3", "Item 4");
        next.TotalRecordCount.Should().Be(4);
        next.MoreRecords.Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Cookie_Continuation_Handles_Nulls_Descending_And_Xml_Sensitive_Strings(bool descending)
    {
        var service = Service(0);
        var rows = new[] { Item(1), Item(2), Item(3), Item(4) };
        var values = new[] { null, "A & <one> 'quoted'", "B", "C" };
        for (var i = 0; i < rows.Length; i++) { rows[i]["ca_text"] = values[i]; service.Simulated().Data().Add(rows[i]); }
        var query = new QueryExpression("ca_item") { ColumnSet = new ColumnSet("ca_name"), PageInfo = new PagingInfo { Count = 1, PageNumber = 1 } };
        query.AddOrder("ca_text", descending ? OrderType.Descending : OrderType.Ascending);
        var found = new List<Guid>();
        for (var i = 0; i < rows.Length; i++)
        {
            var result = service.RetrieveMultiple(query);
            found.Add(result.Entities.Single().Id);
            query.PageInfo.PagingCookie = result.PagingCookie;
            query.PageInfo.PageNumber++;
        }
        found.Should().Equal((descending ? rows.Reverse() : rows).Select(row => row.Id));
    }

    [TestCase("Money")]
    [TestCase("Decimal")]
    [TestCase("Double")]
    [TestCase("DateTime")]
    [TestCase("DateTimeOffset")]
    [TestCase("Single")]
    [TestCase("Guid")]
    [TestCase("Boolean")]
    [TestCase("OptionSetValue")]
    [TestCase("EntityReference")]
    public void Cookie_RoundTrips_Supported_Sdk_Ordering_Types(string kind)
    {
        var service = Service(3);
        foreach (var row in service.Simulated().Data().Get("ca_item"))
        {
            var n = row.GetAttributeValue<int>("ca_sort");
            row["ca_key"] = kind switch
            {
                "Money" => new Money(n), "Decimal" => (decimal)n, "Double" => (double)n,
                "DateTime" => new DateTime(2026, 1, n, 12, 0, 0, DateTimeKind.Utc),
                "DateTimeOffset" => new DateTimeOffset(2026, 1, n, 12, 0, 0, TimeSpan.FromHours(2)),
                "Single" => (float)n,
                "Guid" => new Guid($"00000000-0000-0000-0000-{n:D12}"),
                "Boolean" => n > 1, "OptionSetValue" => new OptionSetValue(n),
                _ => new EntityReference("account", new Guid($"00000000-0000-0000-0000-{n:D12}"))
            };
        }
        var query = new QueryExpression("ca_item") { ColumnSet = new ColumnSet("ca_name"), PageInfo = new PagingInfo { Count = 1, PageNumber = 1 } };
        query.AddOrder("ca_key", OrderType.Ascending);
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var result = service.RetrieveMultiple(query);
            ids.Add(result.Entities.Single().Id);
            query.PageInfo.PagingCookie = result.PagingCookie;
            query.PageInfo.PageNumber++;
        }
        ids.Should().OnlyHaveUniqueItems().And.HaveCount(3);
    }

    [TestCase("not XML")]
    [TestCase("<cookie/>")]
    [TestCase("<!DOCTYPE x [<!ENTITY x SYSTEM 'file:///does-not-exist'>]><cookie>&x;</cookie>")]
    public void Malformed_Or_External_Cookies_Are_Rejected(string cookie)
    {
        Action retrieve = () => Service(3).RetrieveMultiple(Page("QueryExpression", 2, 1, cookie));
        retrieve.Should().Throw<ArgumentException>();
    }

    [TestCase("table")]
    [TestCase("order")]
    [TestCase("page")]
    [TestCase("direction")]
    public void Cookie_Cannot_Be_Reused_For_Different_Query_Shape(string change)
    {
        var service = Service(3);
        var first = service.RetrieveMultiple(Page("QueryExpression", 1, 1));
        var query = (QueryExpression)Page("QueryExpression", 2, 1, first.PagingCookie);
        if (change == "table") query.EntityName = "ca_other";
        if (change == "order") query.Orders[0].AttributeName = "ca_name";
        if (change == "page") query.PageInfo.PageNumber = 4;
        if (change == "direction") query.Orders[0].OrderType = OrderType.Descending;
        Action retrieve = () => service.RetrieveMultiple(query);
        retrieve.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Security_Is_Reevaluated_Before_Cookie_Continuation_And_Counts()
    {
        var user = new Entity("systemuser", Guid.NewGuid());
        var bu = Guid.NewGuid();
        var model = SimulatedSecurityModel.Create().WithBusinessUnit(bu, "Root").WithUser(user.Id, bu)
            .WithRole("Read", role => role.CanRead("ca_item", PrivilegeDepthEnum.User)).AssignRoleToUser("Read", user.Id);
        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions { AuthenticatedUser = user, SimulatedSecurityModel = model });
        for (var i = 1; i <= 3; i++) { var item = Item(i); item["ownerid"] = user.ToEntityReference(); service.Simulated().Data().Add(item); }
        var first = service.RetrieveMultiple(Page("QueryExpression", 1, 1));
        model.Roles.Single().CanRead("ca_item", PrivilegeDepthEnum.None);
        var next = service.RetrieveMultiple(Page("QueryExpression", 2, 1, first.PagingCookie));
        next.Entities.Should().BeEmpty();
        next.TotalRecordCount.Should().Be(0);
        next.MoreRecords.Should().BeFalse();
    }

    [Test]
    public void Distinct_Uses_Simple_Pages_Without_Source_Identity_Cookies()
    {
        var service = Service(4);
        foreach (var row in service.Simulated().Data().Get("ca_item")) row["ca_group"] = row.GetAttributeValue<int>("ca_sort") % 2;
        var query = new QueryExpression("ca_item") { Distinct = true, ColumnSet = new ColumnSet("ca_group"), PageInfo = new PagingInfo { Count = 1, PageNumber = 1, ReturnTotalRecordCount = true } };
        query.AddOrder("ca_group", OrderType.Ascending);
        var first = service.RetrieveMultiple(query);
        first.Entities.Single().Id.Should().Be(Guid.Empty);
        first.PagingCookie.Should().BeNullOrEmpty();
        first.TotalRecordCount.Should().Be(2);
        query.PageInfo.PageNumber = 2;
        service.RetrieveMultiple(query).Entities.Single()["ca_group"].Should().Be(1);
    }

    [TestCase(1, -1)]
    public void Negative_Paging_Values_Are_Rejected(int count, int page)
    {
        Action retrieve = () => Service(0).RetrieveMultiple(Page("QueryExpression", page, count));
        retrieve.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(-2147220989);
    }

    [Test]
    public void Top_Count_Cannot_Be_Combined_With_Explicit_Paging()
    {
        var query = (QueryExpression)Page("QueryExpression", 1, 1);
        query.TopCount = 1;
        Action retrieve = () => Service(3).RetrieveMultiple(query);
        retrieve.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail.ErrorCode.Should().Be(-2147220891);
    }

    public static IEnumerable<TestCaseData> NumericCases()
    {
        foreach (var type in new[] { "Int32", "Int64", "Decimal", "Double", "Money" })
        foreach (var operation in new[] { XrmAggregateType.Sum, XrmAggregateType.Avg, XrmAggregateType.Min, XrmAggregateType.Max, XrmAggregateType.Count, XrmAggregateType.CountColumn })
        foreach (var empty in new[] { false, true })
            yield return new TestCaseData(type, operation, empty);
    }

    [TestCaseSource(nameof(NumericCases))]
    public void Aggregates_Return_Typed_Aliases_And_Empty_Input_Row(string kind, XrmAggregateType operation, bool empty)
    {
        var service = Service(0);
        object Typed(int n) => kind switch { "Int32" => n, "Int64" => (long)n, "Double" => (double)n, "Money" => new Money(n), _ => (decimal)n };
        for (var i = 1; i <= 3; i++) { var row = Item(i); row["ca_value"] = i == 3 ? null : Typed(i); service.Simulated().Data().Add(row); }
        var query = Aggregate(operation);
        if (empty) query.Criteria.AddCondition("ca_sort", ConditionOperator.Equal, -1);
        var rows = service.RetrieveMultiple(query).Entities;
        var result = rows.Should().ContainSingle().Subject;
        result.Id.Should().Be(Guid.Empty);
        result.Attributes.Keys.Should().Equal("n");
        var alias = result.GetAttributeValue<AliasedValue>("n");
        alias.EntityLogicalName.Should().Be("ca_item");
        alias.AttributeLogicalName.Should().Be("ca_value");
        var count = operation is XrmAggregateType.Count or XrmAggregateType.CountColumn;
        if (empty && !count && kind is not ("Int32" or "Int64"))
        {
            alias.Value.Should().BeNull();
            return;
        }
        alias.Value.GetType().Name.Should().Be(count ? "Int32" : kind);
        var numeric = Convert.ToDecimal(alias.Value is Money money ? money.Value : alias.Value);
        var expected = empty ? 0m : operation switch
        {
            XrmAggregateType.Count => 3m, XrmAggregateType.CountColumn => 2m, XrmAggregateType.Sum => 3m,
            XrmAggregateType.Min => 1m, XrmAggregateType.Max => 2m,
            _ => kind is "Int32" or "Int64" ? 1m : 1.5m
        };
        numeric.Should().Be(expected);
    }

    [Test]
    public void Grouped_Empty_Input_Returns_No_Rows_And_Null_Group_Omits_Alias()
    {
        var service = Service(1);
        var query = Aggregate(XrmAggregateType.Count);
        query.ColumnSet.AttributeExpressions.Add(new XrmAttributeExpression("ca_group", XrmAggregateType.None, "g") { HasGroupBy = true });
        var row = service.RetrieveMultiple(query).Entities.Single();
        row.Contains("g").Should().BeFalse();
        row.GetAttributeValue<AliasedValue>("n").Value.Should().Be(1);
        query.Criteria.AddCondition("ca_sort", ConditionOperator.Equal, -1);
        service.RetrieveMultiple(query).Entities.Should().BeEmpty();
    }

    [TestCase("Money")]
    [TestCase("OptionSetValue")]
    [TestCase("EntityReference")]
    public void Grouped_Values_Preserve_Sdk_Wrappers_And_Isolation(string kind)
    {
        var service = Service(1);
        var source = service.Simulated().Data().Get("ca_item").Single();
        source["ca_group"] = kind switch { "Money" => new Money(5m), "OptionSetValue" => new OptionSetValue(5), _ => new EntityReference("account", Guid.NewGuid()) };
        var query = Aggregate(XrmAggregateType.Count);
        query.ColumnSet.AttributeExpressions.Add(new XrmAttributeExpression("ca_group", XrmAggregateType.None, "g") { HasGroupBy = true });
        var value = service.RetrieveMultiple(query).Entities.Single().GetAttributeValue<AliasedValue>("g").Value;
        value.GetType().Name.Should().Be(kind);
        value.Should().NotBeSameAs(source["ca_group"]);
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(50001)]
    [TestCase(int.MaxValue)]
    public void Invalid_Aggregate_Limit_Cannot_Overflow_Or_Truncate_Silently(int limit)
    {
        Action retrieve = () => Service(0).RetrieveMultiple(new FetchExpression($"<fetch aggregate='true' aggregatelimit='{limit}'><entity name='ca_item'><attribute name='ca_value' alias='n' aggregate='count'/></entity></fetch>"));
        var fault = retrieve.Should().Throw<FaultException<OrganizationServiceFault>>().Which.Detail;
        fault.ErrorCode.Should().Be(-2147220989);
        fault.Message.Should().Be("Expected value between 1 and 50000 inclusive.");
    }

    [Test]
    public void Duplicate_Aggregate_Aliases_Are_Rejected()
    {
        var query = Aggregate(XrmAggregateType.Count);
        query.ColumnSet.AttributeExpressions.Add(new XrmAttributeExpression("ca_sort", XrmAggregateType.Sum, "n"));
        Action retrieve = () => Service(1).RetrieveMultiple(query);
        retrieve.Should().Throw<ArgumentException>().WithMessage("*aliases*unique*");
    }

    [TestCase("ca_money")]
    [TestCase("ca_decimal")]
    [TestCase("ca_double")]
    [TestCase("ca_integer")]
    public void Empty_Aggregates_Use_Metadata_Without_Seeded_Table(string attribute)
    {
        var metadata = SimulatedMetadata.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "paging-aggregate-metadata.json"));
        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions { Metadata = metadata });
        var query = new QueryExpression("ca_item") { ColumnSet = new ColumnSet(false) };
        query.ColumnSet.AttributeExpressions.Add(new XrmAttributeExpression(attribute, XrmAggregateType.Sum, "n"));
        var alias = service.RetrieveMultiple(query).Entities.Single().GetAttributeValue<AliasedValue>("n");
        alias.AttributeLogicalName.Should().Be(attribute);
        if (attribute == "ca_integer") alias.Value.Should().Be(0);
        else alias.Value.Should().BeNull();
    }

    [Test]
    public void Cookie_Uses_Metadata_Primary_Id_Without_Leaking_Unselected_Keys()
    {
        var metadata = SimulatedMetadata.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestMetadata", "paging-aggregate-metadata.json"));
        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions { Metadata = metadata });
        var ids = Enumerable.Range(1, 3).Select(i => new Guid($"00000000-0000-0000-0000-{i:D12}")).ToArray();
        foreach (var id in ids.Reverse()) service.Simulated().Data().Add(new Entity("ca_item", id) { ["ca_name"] = "same" });
        var query = new QueryExpression("ca_item") { ColumnSet = new ColumnSet("ca_name"), PageInfo = new PagingInfo { PageNumber = 1, Count = 1 } };
        query.AddOrder("ca_key", OrderType.Ascending);
        var first = service.RetrieveMultiple(query);
        first.Entities.Single().Id.Should().Be(ids[0]);
        first.Entities.Single().Contains("ca_itemid").Should().BeFalse();
        query.PageInfo.PagingCookie = first.PagingCookie;
        query.PageInfo.PageNumber++;
        service.RetrieveMultiple(query).Entities.Single().Id.Should().Be(ids[1]);
    }

    [TestCase("Money")]
    [TestCase("OptionSetValue")]
    [TestCase("EntityReference")]
    public void Distinct_Column_Count_Uses_Sdk_Values_And_Excludes_Nulls(string kind)
    {
        var service = Service(0);
        var referenceId = Guid.NewGuid();
        for (var i = 1; i <= 4; i++)
        {
            var row = Item(i);
            row["ca_value"] = i == 4 ? null : kind switch
            {
                "Money" => new Money(i == 3 ? 2m : 1m),
                "OptionSetValue" => new OptionSetValue(i == 3 ? 2 : 1),
                _ => new EntityReference("account", i == 3 ? Guid.NewGuid() : referenceId)
            };
            service.Simulated().Data().Add(row);
        }
        var result = service.RetrieveMultiple(new FetchExpression("<fetch aggregate='true'><entity name='ca_item'><attribute name='ca_value' alias='n' aggregate='countcolumn' distinct='true'/></entity></fetch>"));
        result.Entities.Single().GetAttributeValue<AliasedValue>("n").Value.Should().Be(2);
    }

    [TestCase("top")]
    [TestCase("count")]
    [TestCase("page")]
    [TestCase("aggregatelimit")]
    public void Non_Integer_Fetch_Options_Are_Rejected(string option)
    {
        Action retrieve = () => Service(1).RetrieveMultiple(new FetchExpression($"<fetch {option}='invalid'><entity name='ca_item'/></fetch>"));
        retrieve.Should().Throw<ArgumentException>().WithMessage("*valid integer*");
    }

    [Test]
    public void Aggregate_Order_Uses_Alias_Over_Source_Column()
    {
        var service = Service(3);
        service.Simulated().Data().Get("ca_item")[0]["ca_group"] = "a";
        service.Simulated().Data().Get("ca_item")[1]["ca_group"] = "b";
        service.Simulated().Data().Get("ca_item")[2]["ca_group"] = "a";
        var query = Aggregate(XrmAggregateType.Count);
        query.ColumnSet.AttributeExpressions.Add(new XrmAttributeExpression("ca_group", XrmAggregateType.None, "g") { HasGroupBy = true });
        query.Orders.Add(new OrderExpression(attributeName: "ca_value", orderType: OrderType.Descending, alias: "n"));
        var result = service.RetrieveMultiple(query);
        result.Entities.Select(row => (string)row.GetAttributeValue<AliasedValue>("g").Value).Should().Equal("a", "b");
    }

    [Test]
    public void RetrieveMultipleRequest_Uses_Same_Paging_Path()
    {
        var service = Service(3);
        var first = ((RetrieveMultipleResponse)service.Execute(new RetrieveMultipleRequest { Query = Page("QueryExpression", 1, 1) })).EntityCollection;
        var next = ((RetrieveMultipleResponse)service.Execute(new RetrieveMultipleRequest { Query = Page("QueryExpression", 2, 1, first.PagingCookie) })).EntityCollection;
        next.Entities.Single()["ca_name"].Should().Be("Item 2");
    }

    private static IOrganizationService Service(int count)
    {
        var service = ((IOrganizationService)null!).Simulate();
        for (var i = 1; i <= count; i++) service.Simulated().Data().Add(Item(i));
        return service;
    }
    private static Entity Item(int i) => new("ca_item", Guid.NewGuid()) { ["ca_name"] = "Item " + i, ["ca_sort"] = i };
    private static QueryExpression Aggregate(XrmAggregateType operation)
    {
        var query = new QueryExpression("ca_item") { ColumnSet = new ColumnSet(false) };
        query.ColumnSet.AttributeExpressions.Add(new XrmAttributeExpression("ca_value", operation, "n"));
        return query;
    }
    private static QueryBase Page(string form, int page, int count, string? cookie = null)
    {
        var info = new PagingInfo { Count = count, PageNumber = page, ReturnTotalRecordCount = true, PagingCookie = cookie };
        if (form == "FetchXML")
            return new FetchExpression($"<fetch page='{page}' count='{count}' returntotalrecordcount='true'{(cookie is null ? "" : $" paging-cookie='{SecurityElement.Escape(cookie)}'")}><entity name='ca_item'><attribute name='ca_name'/><order attribute='ca_sort'/></entity></fetch>");
        if (form == "QueryByAttribute")
        {
            var query = new QueryByAttribute("ca_item") { ColumnSet = new ColumnSet("ca_name"), PageInfo = info };
            query.AddOrder("ca_sort", OrderType.Ascending);
            return query;
        }
        var expression = new QueryExpression("ca_item") { ColumnSet = new ColumnSet("ca_name"), PageInfo = info };
        expression.AddOrder("ca_sort", OrderType.Ascending);
        return expression;
    }
}
