using Microsoft.EntityFrameworkCore;

namespace Lms.Auth.Application.DTOs;

/// <summary>
/// A reusable paginated list that wraps any IQueryable.
/// It executes the query efficiently: one count and one page fetch.
/// </summary>
public class PaginatedList<T>
{
    public IReadOnlyCollection<T> Items { get; }
    public int PageIndex { get; }
    public int TotalPages { get; }
    public int TotalCount { get; }

    public PaginatedList(IReadOnlyCollection<T> items, int count, int pageIndex, int pageSize)
    {
        PageIndex = pageIndex;
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        TotalCount = count;
        Items = items;
    }

    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    /// <summary>
    /// Creates a PaginatedList from an IQueryable by counting and taking the page.
    /// </summary>
    public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
    {
        var count = await source.CountAsync();
        var items = await source.Skip((pageIndex - 1) * pageSize)
                                .Take(pageSize)
                                .ToListAsync();
        return new PaginatedList<T>(items, count, pageIndex, pageSize);
    }
}