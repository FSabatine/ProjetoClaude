using System.Linq.Expressions;
using Fleet.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Common;

public enum SortDirection
{
    Asc,
    Desc,
}

/// <summary>Common list query string: ?search=&amp;page=&amp;pageSize=&amp;sortBy=&amp;sortDirection=</summary>
public class ListRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
    public string? SortBy { get; set; }
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
    public string? SearchTerm => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Whitelist of sortable columns. Unknown keys fall back to the default, so the client can never
/// sort by an arbitrary expression (A03).
/// </summary>
public sealed class SortMap<T> where T : AuditableEntity
{
    private readonly Dictionary<string, Func<IQueryable<T>, bool, IOrderedQueryable<T>>> _sorts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly string _defaultKey;

    public SortMap(string defaultKey) => _defaultKey = defaultKey;

    public SortMap<T> Add<TKey>(string key, Expression<Func<T, TKey>> selector)
    {
        _sorts[key] = (query, descending) => descending ? query.OrderByDescending(selector) : query.OrderBy(selector);
        return this;
    }

    public IQueryable<T> Apply(IQueryable<T> query, string? sortBy, SortDirection direction)
    {
        var sort = sortBy is not null && _sorts.TryGetValue(sortBy, out var chosen) ? chosen : _sorts[_defaultKey];
        // Id as tie-breaker keeps pagination stable when the sorted column has duplicates.
        return sort(query, direction == SortDirection.Desc).ThenBy(e => e.Id);
    }
}

public static class PagingExtensions
{
    public static async Task<PagedResult<TResult>> ToPagedResultAsync<T, TResult>(
        this IQueryable<T> query, ListRequest request, Expression<Func<T, TResult>> selector, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((request.SafePage - 1) * request.SafePageSize)
            .Take(request.SafePageSize)
            .Select(selector)
            .ToListAsync(ct);
        return new PagedResult<TResult>(items, request.SafePage, request.SafePageSize, total);
    }
}
