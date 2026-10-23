using System.Xml;
using CloudAwesome.Xrm.Simulate.DataServices;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

/// <summary>
/// 
/// </summary>
public static class FetchExpressionParser
{
    public static IEnumerable<Entity> Parse(FetchExpression query, Dictionary<string,
        List<Entity>> data, MockedEntityDataService dataService)
    {
        ArgumentNullException.ThrowIfNull(query);
        var queryExpression = ConvertFetchXmlToQueryExpression(query.Query);
        return QueryExpressionParser.Parse(queryExpression, data, dataService);
    }

    public static QueryExpression ConvertFetchXmlToQueryExpression(string fetchXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fetchXml);
        var doc = new XmlDocument { XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(new StringReader(fetchXml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            doc.Load(reader);
        }
        catch (XmlException exception)
        {
            throw new ArgumentException("FetchXML must be well-formed XML.", nameof(fetchXml), exception);
        }

        var fetchNode = doc.DocumentElement;
        if (fetchNode?.Name != "fetch" || fetchNode.SelectNodes("entity")!.Count != 1)
        {
            throw new ArgumentException("FetchXML must contain a <fetch> root with exactly one <entity>.",
                nameof(fetchXml));
        }

        var entityNode = fetchNode.SelectSingleNode("entity")!;
        var entityName = RequiredAttribute(entityNode, "name");
        var query = new QueryExpression(entityName);

        var topCount = GetIntegerXmlAttribute(fetchNode, "top");
        if (topCount.HasValue)
        {
            query.TopCount = topCount.Value;
        }

        var pageNumber = GetIntegerXmlAttribute(fetchNode, "page");
        var count = GetIntegerXmlAttribute(fetchNode, "count");
        var pagingCookie = GetStringXmlAttribute(fetchNode, "paging-cookie");
        var returnTotalRecordCount = GetBooleanXmlAttribute(fetchNode, "returntotalrecordcount");

        if (pageNumber.HasValue || count.HasValue || !string.IsNullOrWhiteSpace(pagingCookie) || returnTotalRecordCount)
        {
            query.PageInfo = new PagingInfo
            {
                PageNumber = pageNumber ?? 0,
                Count = count ?? 0,
                PagingCookie = pagingCookie,
                ReturnTotalRecordCount = returnTotalRecordCount
            };
        }

        if (bool.TryParse(fetchNode.Attributes["distinct"]?.Value, out bool distinct))
        {
            query.Distinct = distinct;
        }

        var isAggregate = GetBooleanXmlAttribute(fetchNode, "aggregate");
        var aggregateLimit = GetIntegerXmlAttribute(fetchNode, "aggregatelimit");

        if (isAggregate)
        {
            query.ColumnSet = new ColumnSet(false);

            foreach (XmlNode attrNode in entityNode.SelectNodes("attribute")!)
            {
                var name = RequiredAttribute(attrNode, "name");

                var alias = GetStringXmlAttribute(attrNode, "alias");
                var hasAggregate = attrNode.Attributes?["aggregate"] != null;
                var isGroupBy = GetBooleanXmlAttribute(attrNode, "groupby") ||
                                attrNode.Attributes?["dategrouping"] != null;

                if (hasAggregate)
                {
                    var attributeAggregate = GetStringXmlAttribute(attrNode, "aggregate")?.ToLowerInvariant();
                    var distinctAggregate = GetBooleanXmlAttribute(attrNode, "distinct");

                    var aggType = attributeAggregate switch
                    {
                        "sum" => XrmAggregateType.Sum,
                        "avg" => XrmAggregateType.Avg,
                        "min" => XrmAggregateType.Min,
                        "max" => XrmAggregateType.Max,
                        "count" => XrmAggregateType.Count,
                        "countcolumn" => XrmAggregateType.CountColumn,
                        _ => throw new NotSupportedException($"Unknown aggregate '{attributeAggregate}'.")
                    };

                    var expression = new XrmAttributeExpression(
                        attributeName: name,
                        alias: string.IsNullOrWhiteSpace(alias) ? null : alias,
                        aggregateType: aggType
                    );

                    query.ColumnSet.AttributeExpressions.Add(
                        FetchXmlAggregateOptions.Attach(expression, distinctAggregate, aggregateLimit));
                }
                else if (isGroupBy)
                {
                    var dateGroupingAttribute = GetStringXmlAttribute(attrNode, "dategrouping");
                    var dateGrouping = MapDateGrouping(dateGroupingAttribute);

                    var xrmAttributeExpression = new XrmAttributeExpression(
                        attributeName: name,
                        alias: string.IsNullOrWhiteSpace(alias) ? null : alias,
                        aggregateType: XrmAggregateType.None,
                        dateTimeGrouping: dateGrouping
                    );
                    xrmAttributeExpression.HasGroupBy = true;

                    query.ColumnSet.AttributeExpressions.Add(
                        FetchXmlAggregateOptions.Attach(xrmAttributeExpression, false, aggregateLimit));
                }
                // else: ignore bare attributes in aggregate fetches (FetchXML would ignore them too)
            }
        }
        else
        {
            query.ColumnSet = ParseColumns(entityNode, defaultAllColumns: true);
        }

        query.Criteria = ParseFilters(entityNode);

        foreach (XmlNode orderNode in entityNode.SelectNodes("order")!)
        {
            query.Orders.Add(ParseOrder(orderNode));
        }

        foreach (XmlNode linkEntityNode in entityNode.SelectNodes("link-entity")!)
        {
            query.LinkEntities.Add(ParseLinkEntity(linkEntityNode, entityName));
        }

        return query;
    }

    private static FilterExpression ParseFilter(XmlNode filterNode)
    {
        var logicalOperator = GetStringXmlAttribute(filterNode, "type")?.ToLowerInvariant() switch
        {
            null or "" or "and" => LogicalOperator.And,
            "or" => LogicalOperator.Or,
            var type => throw new NotSupportedException($"FetchXML filter type '{type}' is not supported.")
        };
        var filter = new FilterExpression(logicalOperator);

        foreach (XmlNode conditionNode in filterNode.SelectNodes("condition")!)
        {
            filter.Conditions.Add(ParseCondition(conditionNode));
        }

        foreach (XmlNode childFilter in filterNode.SelectNodes("filter")!)
        {
            filter.Filters.Add(ParseFilter(childFilter));
        }

        return filter;
    }

    private static ConditionExpression ParseCondition(XmlNode conditionNode)
    {
        var attributeName = RequiredAttribute(conditionNode, "attribute");
        var operatorName = RequiredAttribute(conditionNode, "operator");
        if (!OperatorMappings.TryGetValue(operatorName, out var conditionOperator))
        {
            throw new NotSupportedException($"FetchXML condition operator '{operatorName}' is not supported.");
        }

        if (conditionNode.Attributes?["entityname"] != null || conditionNode.Attributes?["valueof"] != null)
        {
            throw new NotSupportedException("FetchXML conditions with 'entityname' or 'valueof' are not supported.");
        }

        if (conditionNode.SelectNodes("value")!.Count != 0)
        {
            throw new NotSupportedException("FetchXML conditions with <value> elements are not supported.");
        }

        var valueNode = conditionNode.Attributes?["value"];
        var requiresValue = conditionOperator is
            ConditionOperator.Equal or ConditionOperator.NotEqual or
            ConditionOperator.GreaterEqual or ConditionOperator.GreaterThan or
            ConditionOperator.LessEqual or ConditionOperator.LessThan or
            ConditionOperator.Like or ConditionOperator.NotLike or
            ConditionOperator.On or ConditionOperator.OnOrAfter or ConditionOperator.OnOrBefore or
            ConditionOperator.LastXDays or ConditionOperator.LastXHours or ConditionOperator.LastXMonths or
            ConditionOperator.LastXWeeks or ConditionOperator.LastXYears or
            ConditionOperator.NextXDays or ConditionOperator.NextXHours or ConditionOperator.NextXMonths or
            ConditionOperator.NextXWeeks or ConditionOperator.NextXYears;
        if (requiresValue != (valueNode is not null))
        {
            throw new ArgumentException(
                $"FetchXML operator '{operatorName}' requires {(requiresValue ? "one value" : "no value")}.");
        }

        var condition = new ConditionExpression(attributeName, conditionOperator);
        if (valueNode is not null)
        {
            var value = valueNode.Value;
            if (conditionOperator is ConditionOperator.Like or ConditionOperator.NotLike)
            {
                value = value.Replace('%', '*');
            }

            condition.Values.Add(value);
        }

        return condition;
    }

    private static OrderExpression ParseOrder(XmlNode orderNode)
    {
        var attributeName = orderNode.Attributes?["attribute"]?.Value;
        var aliasName = orderNode.Attributes?["alias"]?.Value;

        var name = !string.IsNullOrWhiteSpace(attributeName)
            ? attributeName
            : !string.IsNullOrWhiteSpace(aliasName)
                ? aliasName
                : throw new ArgumentException("FetchXML <order> must specify either 'attribute' or 'alias'.");

        var descendingRaw = orderNode.Attributes?["descending"]?.Value;
        var isDescending = false;
        if (!string.IsNullOrWhiteSpace(descendingRaw))
            bool.TryParse(descendingRaw, out isDescending);

        var orderType = isDescending ? OrderType.Descending : OrderType.Ascending;

        return new OrderExpression(name, orderType);
    }

    private static LinkEntity? ParseLinkEntity(XmlNode linkEntityNode, string baseEntityName)
    {
        if (linkEntityNode.Attributes == null) return null;

        var linkEntity = new LinkEntity()
        {
            LinkFromEntityName = baseEntityName,
            LinkFromAttributeName = RequiredAttribute(linkEntityNode, "to"),
            LinkToEntityName = RequiredAttribute(linkEntityNode, "name"),
            LinkToAttributeName = RequiredAttribute(linkEntityNode, "from"),
            EntityAlias = linkEntityNode.Attributes["alias"]?.Value,
            JoinOperator = ParseJoinOperator(linkEntityNode.Attributes["link-type"]?.Value)
        };

        linkEntity.Columns = ParseColumns(linkEntityNode, defaultAllColumns: false);

        foreach (XmlNode linkEntityChildNode in linkEntityNode.SelectNodes("link-entity")!)
        {
            linkEntity.LinkEntities.Add(ParseLinkEntity(linkEntityChildNode,
                linkEntity.LinkToEntityName));
        }

        foreach (XmlNode orderNode in linkEntityNode.SelectNodes("order")!)
        {
            linkEntity.Orders.Add(ParseOrder(orderNode));
        }

        linkEntity.LinkCriteria = ParseFilters(linkEntityNode);

        return linkEntity;
    }

    private static JoinOperator ParseJoinOperator(string? linkType)
    {
        return linkType?.ToLowerInvariant() switch
        {
            null or "" => JoinOperator.Inner,
            "inner" => JoinOperator.Inner,
            "outer" => JoinOperator.LeftOuter,
            _ => throw new NotSupportedException($"FetchXML link-type '{linkType}' is not supported.")
        };
    }

    private static readonly Dictionary<string, ConditionOperator> OperatorMappings =
        new Dictionary<string, ConditionOperator>(StringComparer.OrdinalIgnoreCase)
        {
            { "eq", ConditionOperator.Equal },
            { "ge", ConditionOperator.GreaterEqual },
            { "gt", ConditionOperator.GreaterThan },
            { "last-seven-days", ConditionOperator.Last7Days },
            { "last-week", ConditionOperator.LastWeek },
            { "last-x-days", ConditionOperator.LastXDays },
            { "last-x-hours", ConditionOperator.LastXHours },
            { "last-x-months", ConditionOperator.LastXMonths },
            { "last-x-weeks", ConditionOperator.LastXWeeks },
            { "last-x-years", ConditionOperator.LastXYears },
            { "last-year", ConditionOperator.LastYear },
            { "le", ConditionOperator.LessEqual },
            { "lt", ConditionOperator.LessThan },
            { "like", ConditionOperator.Like },
            { "next-week", ConditionOperator.NextWeek },
            { "next-x-days", ConditionOperator.NextXDays },
            { "next-x-hours", ConditionOperator.NextXHours },
            { "next-x-months", ConditionOperator.NextXMonths },
            { "next-x-weeks", ConditionOperator.NextXWeeks },
            { "next-x-years", ConditionOperator.NextXYears },
            { "next-year", ConditionOperator.NextYear },
            { "ne", ConditionOperator.NotEqual },
            { "not-like", ConditionOperator.NotLike },
            { "not-null", ConditionOperator.NotNull },
            { "null", ConditionOperator.Null },
            { "on", ConditionOperator.On },
            { "on-or-after", ConditionOperator.OnOrAfter },
            { "on-or-before", ConditionOperator.OnOrBefore },
            { "this-month", ConditionOperator.ThisMonth },
            { "this-week", ConditionOperator.ThisWeek },
            { "this-year", ConditionOperator.ThisYear },
            { "today", ConditionOperator.Today },
            { "tomorrow", ConditionOperator.Tomorrow },
            { "yesterday", ConditionOperator.Yesterday },

            // etc., add all needed mappings here
        };

    private static FilterExpression ParseFilters(XmlNode parent)
    {
        var filters = parent.SelectNodes("filter")!.Cast<XmlNode>().Select(ParseFilter).ToList();
        if (filters.Count == 1) return filters[0];
        var result = new FilterExpression(LogicalOperator.And);
        result.Filters.AddRange(filters);
        return result;
    }

    private static ColumnSet ParseColumns(XmlNode entityNode, bool defaultAllColumns)
    {
        var attributes = entityNode.SelectNodes("attribute")!.Cast<XmlNode>().ToList();
        if (attributes.Any(attribute => attribute.Attributes?["alias"] != null))
        {
            throw new NotSupportedException("FetchXML non-aggregate attribute aliases are not supported.");
        }

        var columns = new ColumnSet(attributes.Select(attribute => RequiredAttribute(attribute, "name")).ToArray());
        columns.AllColumns = entityNode.SelectSingleNode("all-attributes") != null ||
                             (defaultAllColumns && attributes.Count == 0);
        return columns;
    }

    private static string RequiredAttribute(XmlNode node, string name)
    {
        var value = GetStringXmlAttribute(node, name);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"FetchXML <{node.Name}> requires a '{name}' attribute.");
    }

    private static string? GetStringXmlAttribute(XmlNode n, string name) => n.Attributes?[name]?.Value;

    private static bool GetBooleanXmlAttribute(XmlNode n, string name)
        => bool.TryParse(n.Attributes?[name]?.Value, out var b) && b;

    private static int? GetIntegerXmlAttribute(XmlNode n, string name)
    {
        var raw = n.Attributes?[name]?.Value;
        if (raw is null) return null;
        return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value : throw new ArgumentException($"FetchXML '{name}' must be a valid integer.");
    }

    private static XrmDateTimeGrouping MapDateGrouping(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return XrmDateTimeGrouping.None;
        switch (s.ToLowerInvariant())
        {
            case "day": return XrmDateTimeGrouping.Day;
            case "week": return XrmDateTimeGrouping.Week;
            case "month": return XrmDateTimeGrouping.Month;
            case "quarter": return XrmDateTimeGrouping.Quarter;
            case "year": return XrmDateTimeGrouping.Year;
            case "fiscal-period": return XrmDateTimeGrouping.FiscalPeriod;
            case "fiscal-year": return XrmDateTimeGrouping.FiscalYear;
            default: throw new NotSupportedException($"Unknown dategrouping '{s}'.");
        }
    }
}
