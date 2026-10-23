using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using CloudAwesome.Xrm.Simulate.ServiceRequests;

namespace CloudAwesome.Xrm.Simulate.QueryParsers;

/// <summary>Standard-table paging, with value-based continuation for base-row queries.</summary>
internal sealed class QueryPaging
{
    private const int MaximumPageSize = 5000;
    private readonly PagingInfo _info;
    private readonly int _size;
    private readonly int _page;
    private int _total;
    private bool _more;
    private string? _cookie;

    internal QueryPaging(PagingInfo? info, int? topCount)
    {
        _info = info ?? new PagingInfo();
        if (_info.PageNumber < 0)
            throw DataverseServiceFaults.InvalidPageNumber(_info.PageNumber);
        if (topCount < 0)
            throw new ArgumentException("TopCount cannot be negative.");
        if (topCount.HasValue && (_info.Count != 0 || _info.PageNumber != 0 || _info.ReturnTotalRecordCount || !string.IsNullOrEmpty(_info.PagingCookie)))
            throw DataverseServiceFaults.TopCountWithPaging(topCount.Value);
        _page = Math.Max(_info.PageNumber, 1);
        _size = _info.Count <= 0 ? MaximumPageSize : Math.Min(_info.Count, MaximumPageSize);
    }

    internal IEnumerable<Entity> Apply(IEnumerable<Entity> input, QueryExpression query, string? primaryId)
    {
        var rows = input.ToList();
        _total = rows.Count;
        primaryId ??= query.EntityName + "id";
        var supportsCookie = !query.Distinct && query.LinkEntities.Count == 0
            && query.ColumnSet?.AttributeExpressions.Any(expression => expression.HasGroupBy || expression.AggregateType != XrmAggregateType.None) != true
            && query.Orders.All(order => string.IsNullOrEmpty(order.EntityName));
        var orders = query.Orders.Select(order => new SortKey(
            string.IsNullOrEmpty(order.Alias) ? order.AttributeName : order.Alias, order.OrderType == OrderType.Descending)).ToList();
        if (supportsCookie && orders.All(order => !string.Equals(order.Attribute, primaryId, StringComparison.OrdinalIgnoreCase)))
            orders.Add(new SortKey(primaryId, false));

        object? Value(Entity row, string attribute) => string.Equals(attribute, primaryId, StringComparison.OrdinalIgnoreCase)
            ? row.Id : QueryValueComparer.GetDataverseValue(row.GetAttributeValue<object>(attribute));
        int Compare(Entity row, IReadOnlyList<object?> boundary)
        {
            for (var i = 0; i < orders.Count; i++)
            {
                var result = QueryValueComparer.Compare(Value(row, orders[i].Attribute), boundary[i]);
                if (result != 0) return orders[i].Descending ? -result : result;
            }
            return 0;
        }
        if (supportsCookie)
            rows = rows.OrderBy(row => row, Comparer<Entity>.Create((left, right) => Compare(left, orders.Select(order => Value(right, order.Attribute)).ToArray()))).ToList();

        IEnumerable<Entity> remaining = rows;
        if (!string.IsNullOrEmpty(_info.PagingCookie))
        {
            if (!supportsCookie)
                throw new NotSupportedException("Paging cookies are supported only for non-distinct base-row queries without aggregates or links.");
            var boundary = ReadCookie(_info.PagingCookie, query.EntityName, orders);
            remaining = rows.Where(row => Compare(row, boundary) > 0);
        }
        else
        {
            var skip = ((long)_page - 1) * _size;
            if (skip >= 50000)
                throw new NotSupportedException("Simple paging is limited to 50,000 rows. Use a supported paging cookie for larger results.");
            remaining = rows.Skip((int)skip);
        }
        var page = remaining.Take(_size + 1).ToList();
        _more = page.Count > _size;
        if (_more) page.RemoveAt(page.Count - 1);
        if (supportsCookie && !query.TopCount.HasValue && page.Count > 0)
        {
            var root = new XElement("cookie", new XAttribute("version", 1), new XAttribute("entity", query.EntityName), new XAttribute("page", _page));
            foreach (var order in orders)
            {
                var value = Value(page[^1], order.Attribute);
                root.Add(new XElement("key", new XAttribute("attribute", order.Attribute), new XAttribute("descending", order.Descending),
                    new XAttribute("type", value?.GetType().Name ?? "null"),
                    new XAttribute("value", value switch
                    {
                        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
                        DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
                        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
                    })));
            }
            _cookie = root.ToString(SaveOptions.DisableFormatting);
        }
        return page;
    }

    internal EntityCollection ToCollection(IEnumerable<Entity> projected) => new(projected.ToList())
    {
        MoreRecords = _more,
        PagingCookie = _cookie,
        TotalRecordCount = _info.ReturnTotalRecordCount ? Math.Min(_total, MaximumPageSize) : -1,
        TotalRecordCountLimitExceeded = _info.ReturnTotalRecordCount && _total > MaximumPageSize
    };

    private object?[] ReadCookie(string text, string entityName, IReadOnlyList<SortKey> orders)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024
            });
            var root = XElement.Load(reader);
            var keys = root.Elements("key").ToList();
            if (root.Name != "cookie" || (string?)root.Attribute("version") != "1" || (string?)root.Attribute("entity") != entityName
                || (int?)root.Attribute("page") != _page - 1 || keys.Count != orders.Count || root.Elements().Count() != keys.Count)
                throw new ArgumentException("Paging cookie does not match this query, ordering, or page.");
            return keys.Select((key, i) =>
            {
                if ((string?)key.Attribute("attribute") != orders[i].Attribute || (bool?)key.Attribute("descending") != orders[i].Descending)
                    throw new ArgumentException("Paging cookie ordering does not match this query.");
                var value = (string?)key.Attribute("value") ?? throw new ArgumentException("Paging cookie key is missing its value.");
                return (string?)key.Attribute("type") switch
                {
                    "null" => (object?)null, "String" => value, "Guid" => Guid.Parse(value),
                    "Int32" => int.Parse(value, CultureInfo.InvariantCulture), "Int64" => long.Parse(value, CultureInfo.InvariantCulture),
                    "Decimal" => decimal.Parse(value, CultureInfo.InvariantCulture), "Double" => double.Parse(value, CultureInfo.InvariantCulture),
                    "Single" => float.Parse(value, CultureInfo.InvariantCulture),
                    "Boolean" => bool.Parse(value), "DateTime" => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    "DateTimeOffset" => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    _ => throw new ArgumentException("Paging cookie contains an unsupported key type.")
                };
            }).ToArray();
        }
        catch (Exception exception) when (exception is XmlException or FormatException or OverflowException)
        {
            throw new ArgumentException("Paging cookie is malformed or is not a simulator-generated cookie.", nameof(text), exception);
        }
    }

    private sealed record SortKey(string Attribute, bool Descending);
}
