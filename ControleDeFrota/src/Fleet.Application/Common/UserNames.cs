using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Common;

public static class UserNames
{
    /// <summary>
    /// Display names for "registered by"/"reviewed by" columns. Deleted users must still be named in history, hence
    /// IgnoreQueryFilters — read-only, and the ids come from records the caller was already allowed to read.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> LoadAsync(IFleetDbContext db, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, string>();
        return await db.Users.IgnoreQueryFilters()
            .Where(u => distinct.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    public static string? Get(this IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id is { } value ? names.GetValueOrDefault(value) : null;
}
