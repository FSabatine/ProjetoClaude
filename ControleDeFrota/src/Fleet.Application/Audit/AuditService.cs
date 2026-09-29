using System.Text.Json;
using Fleet.Application.Common;
using Fleet.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Audit;

public sealed record AuditFieldChange(string Field, JsonElement? Old, JsonElement? New);

public sealed record AuditEntryResponse(
    long Id,
    AuditAction Action,
    DateTime OccurredAt,
    Guid? UserId,
    string? UserName,
    IReadOnlyList<AuditFieldChange> Changes);

public sealed class AuditService(IFleetDbContext db, ICurrentUser currentUser)
{
    public const int MaxEntries = 200;

    /// <summary>
    /// History of one record. Restricted to logs of the caller's company (Company logs carry the company's
    /// own id as CompanyId), so ids of other tenants return an empty list.
    /// </summary>
    public async Task<IReadOnlyList<AuditEntryResponse>> GetHistoryAsync(string entityName, Guid entityId, CancellationToken ct)
    {
        var id = entityId.ToString();
        var logs = await db.AuditLogs
            .Where(a => a.EntityName == entityName && a.EntityId == id && a.CompanyId == currentUser.CompanyId)
            .OrderByDescending(a => a.Id)
            .Take(MaxEntries)
            .ToListAsync(ct);

        var userIds = logs.Where(l => l.UserId != null).Select(l => l.UserId!.Value).Distinct().ToList();
        // Deleted users must still be named in history, hence IgnoreQueryFilters (read-only, ids come from the logs above).
        var names = await db.Users.IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return logs.Select(l => new AuditEntryResponse(
            l.Id, l.Action, l.OccurredAt, l.UserId,
            l.UserId is { } uid ? names.GetValueOrDefault(uid) : null,
            ParseChanges(l.Changes))).ToList();
    }

    private static List<AuditFieldChange> ParseChanges(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .Select(p => new AuditFieldChange(
                p.Name,
                p.Value.TryGetProperty("old", out var o) ? o.Clone() : null,
                p.Value.TryGetProperty("new", out var n) ? n.Clone() : null))
            .ToList();
    }
}
