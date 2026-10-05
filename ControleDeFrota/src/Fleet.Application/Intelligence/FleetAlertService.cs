using Fleet.Application.Common;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Intelligence;

public sealed class FleetAlertListRequest : ListRequest
{
    /// <summary>Default: only open alerts (new, read, in progress).</summary>
    public FleetAlertStatus? Status { get; set; }
    public bool IncludeClosed { get; set; }
    public FleetAlertCategory? Category { get; set; }
    public FleetAlertSeverity? Severity { get; set; }
    public AutomationTrigger? Trigger { get; set; }
    public Guid? RuleId { get; set; }
    public Guid? VehicleId { get; set; }
    public bool AssignedToMe { get; set; }
}

public sealed record FleetAlertResponse(
    Guid Id, AutomationTrigger Trigger, FleetAlertCategory Category, FleetAlertSeverity Severity, FleetAlertStatus Status, int Priority,
    string Title, string Explanation, string Evidence, string RecommendedAction,
    string EntityType, Guid EntityId, Guid? VehicleId, string? LicensePlate, string? Tab,
    Guid? RuleId, string? RuleName, DateTime DetectedAt, DateTime LastDetectedAt, int DetectionCount, int RecurrenceCount,
    DateTime? ReadAt, Guid? AssignedToUserId, string? AssignedToName, DateTime? ClosedAt, string? ClosedByName, string? ClosingNotes,
    bool AutoResolved, IReadOnlyList<FleetAlertStatus> NextStatuses);

public sealed record FleetAlertStatusRequest
{
    public FleetAlertStatus? Status { get; init; }
    public string? Notes { get; init; }
}

public sealed record AlertCount(string Key, int Count);

public sealed record FleetAlertSummary(
    int Open, int New, int Critical, int Warning, int Info, int AssignedToMe,
    IReadOnlyList<AlertCount> ByCategory, IReadOnlyList<AlertCount> ByTrigger, DateTime? LastCheckedAt);

public sealed class FleetAlertStatusRequestValidator : AbstractValidator<FleetAlertStatusRequest>
{
    public FleetAlertStatusRequestValidator()
    {
        RuleFor(x => x.Status).NotNull().WithMessage("Situação: campo obrigatório.").IsInEnum().WithMessage("Situação inválida.");
        RuleFor(x => x.Notes).MaxLen(FleetAlert.NotesMaxLength);
        RuleFor(x => x.Notes).Required("Motivo").When(x => x.Status is { } s && FleetAlertWorkflow.RequiresNotes(s))
            .WithMessage("Motivo: informe por que o alerta está sendo descartado.");
    }
}

/// <summary>
/// Alert center (spec §9–§11). Every read is filtered by the alert's audience (ADR-045): a user only sees alerts whose
/// audience permissions they hold — the same data rules as the module the alert comes from. Hidden = 404.
/// </summary>
public sealed class FleetAlertService(IFleetDbContext db, ICurrentUser currentUser, IClock clock, IValidator<FleetAlertStatusRequest> validator)
{
    private IQueryable<FleetAlert> Visible()
    {
        var audiences = AlertAudiences.VisibleTo(currentUser.Permissions);
        return db.FleetAlerts.Where(a => audiences.Contains(a.Audience));
    }

    public async Task<PagedResult<FleetAlertResponse>> ListAsync(FleetAlertListRequest request, CancellationToken ct)
    {
        var query = Visible();
        if (request.Status is { } status) query = query.Where(a => a.Status == status);
        else if (!request.IncludeClosed) query = query.Where(a => FleetAlertWorkflow.OpenStatuses.Contains(a.Status));
        if (request.Category is { } category) query = query.Where(a => a.Category == category);
        if (request.Severity is { } severity) query = query.Where(a => a.Severity == severity);
        if (request.Trigger is { } trigger) query = query.Where(a => a.Trigger == trigger);
        if (request.RuleId is { } ruleId) query = query.Where(a => a.AutomationRuleId == ruleId);
        if (request.VehicleId is { } vehicleId) query = query.Where(a => a.VehicleId == vehicleId);
        if (request.AssignedToMe) query = query.Where(a => a.AssignedToUserId == currentUser.UserId);
        if (request.SearchTerm is { } term)
        {
            var plate = LicensePlate.Normalize(term);
            query = query.Where(a => a.Title.Contains(term) || a.Explanation.Contains(term) ||
                                     (plate.Length > 0 && db.Vehicles.Any(v => v.Id == a.VehicleId && v.LicensePlate.Contains(plate))));
        }

        var ordered = request.SortBy?.ToLowerInvariant() switch
        {
            "detectedat" => request.SortDirection == SortDirection.Asc ? query.OrderBy(a => a.DetectedAt) : query.OrderByDescending(a => a.DetectedAt),
            "severity" => request.SortDirection == SortDirection.Asc ? query.OrderBy(a => a.Severity) : query.OrderByDescending(a => a.Severity),
            // Default (spec §10): most important first.
            _ => query.OrderByDescending(a => a.Priority).ThenByDescending(a => a.DetectedAt),
        };
        var page = await ordered.ThenBy(a => a.Id).ToPagedResultAsync(request, a => a, ct);
        var items = await ToResponsesAsync(page.Items, ct);
        return new PagedResult<FleetAlertResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<FleetAlertResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await ToResponsesAsync([await FindAsync(id, ct)], ct))[0];

    public async Task<FleetAlertSummary> SummaryAsync(CancellationToken ct)
    {
        var open = Visible().Where(a => FleetAlertWorkflow.OpenStatuses.Contains(a.Status));
        var bySeverity = await open.GroupBy(a => a.Severity).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var byCategory = await open.GroupBy(a => a.Category).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var byTrigger = await open.GroupBy(a => a.Trigger).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var newCount = await open.CountAsync(a => a.Status == FleetAlertStatus.New, ct);
        var mine = await open.CountAsync(a => a.AssignedToUserId == currentUser.UserId, ct);
        var lastChecked = await db.AutomationExecutions.MaxAsync(e => (DateTime?)e.FinishedAt, ct);
        return new FleetAlertSummary(
            bySeverity.Values.Sum(), newCount, bySeverity.GetValueOrDefault(FleetAlertSeverity.Critical),
            bySeverity.GetValueOrDefault(FleetAlertSeverity.Warning), bySeverity.GetValueOrDefault(FleetAlertSeverity.Info), mine,
            byCategory.OrderByDescending(c => c.Count).Select(c => new AlertCount(c.Key.ToString(), c.Count)).ToList(),
            byTrigger.OrderByDescending(c => c.Count).Select(c => new AlertCount(c.Key.ToString(), c.Count)).ToList(),
            lastChecked);
    }

    /// <summary>Top open alerts for the dashboard (already ordered by priority).</summary>
    public async Task<IReadOnlyList<FleetAlertResponse>> TopAsync(int count, CancellationToken ct)
    {
        var alerts = await Visible().Where(a => FleetAlertWorkflow.OpenStatuses.Contains(a.Status))
            .OrderByDescending(a => a.Priority).ThenByDescending(a => a.DetectedAt).ThenBy(a => a.Id).Take(count).ToListAsync(ct);
        return await ToResponsesAsync(alerts, ct);
    }

    public async Task<FleetAlertResponse> ChangeStatusAsync(Guid id, FleetAlertStatusRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var alert = await FindAsync(id, ct);
        var target = request.Status!.Value;
        if (!FleetAlertWorkflow.CanTransition(alert.Status, target))
            throw new BusinessRuleException("Esta mudança de situação não é permitida para o alerta. Atualize a página e confira a situação atual.");

        var now = clock.UtcNow;
        switch (target)
        {
            case FleetAlertStatus.Read:
                alert.ReadAt = now;
                alert.ReadBy = currentUser.UserId;
                break;
            case FleetAlertStatus.InProgress:
                alert.ReadAt ??= now;
                alert.ReadBy ??= currentUser.UserId;
                alert.AssignedToUserId = currentUser.UserId;
                break;
            default:
                alert.ClosedAt = now;
                alert.ClosedBy = currentUser.UserId;
                alert.ClosingNotes = request.Notes.TrimToNull();
                break;
        }
        alert.Status = target;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Opening an alert marks it as read (the user saw it) — only from New, never reopening anything.</summary>
    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var alert = await FindAsync(id, ct);
        if (alert.Status != FleetAlertStatus.New) return;
        alert.Status = FleetAlertStatus.Read;
        alert.ReadAt = clock.UtcNow;
        alert.ReadBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
    }

    private async Task<FleetAlert> FindAsync(Guid id, CancellationToken ct) =>
        await Visible().SingleOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException("Alerta não encontrado. Ele pode ter sido encerrado ou você não tem acesso a esse tipo de informação.");

    private async Task<IReadOnlyList<FleetAlertResponse>> ToResponsesAsync(IReadOnlyList<FleetAlert> alerts, CancellationToken ct)
    {
        var vehicleIds = alerts.Where(a => a.VehicleId != null).Select(a => a.VehicleId!.Value).Distinct().ToList();
        var plates = await db.Vehicles.Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.LicensePlate, ct);
        var ruleIds = alerts.Where(a => a.AutomationRuleId != null).Select(a => a.AutomationRuleId!.Value).Distinct().ToList();
        // Deleted rules keep naming their past alerts.
        var rules = await db.AutomationRules.IgnoreQueryFilters()
            .Where(r => ruleIds.Contains(r.Id) && r.CompanyId == currentUser.CompanyId).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var userIds = alerts.SelectMany(a => new[] { a.AssignedToUserId, a.ClosedBy }).OfType<Guid>().Distinct().ToList();
        var users = await db.Users.Where(u => userIds.Contains(u.Id) && u.CompanyId == currentUser.CompanyId).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return alerts.Select(a => new FleetAlertResponse(
            a.Id, a.Trigger, a.Category, a.Severity, a.Status, a.Priority, a.Title, a.Explanation, a.Evidence, a.RecommendedAction,
            a.EntityType, a.EntityId, a.VehicleId, a.VehicleId is { } v ? plates.GetValueOrDefault(v) : null, a.Tab,
            a.AutomationRuleId, a.AutomationRuleId is { } r ? rules.GetValueOrDefault(r) : null,
            a.DetectedAt, a.LastDetectedAt, a.DetectionCount, a.RecurrenceCount, a.ReadAt,
            a.AssignedToUserId, a.AssignedToUserId is { } u ? users.GetValueOrDefault(u) : null,
            a.ClosedAt, a.ClosedBy is { } c ? users.GetValueOrDefault(c) : (a.AutoResolved ? "Sistema" : null), a.ClosingNotes,
            a.AutoResolved, currentUser.HasPermission(Domain.Authorization.Permissions.Alerts.Manage) ? FleetAlertWorkflow.NextStatuses(a.Status) : [])).ToList();
    }
}

public sealed record NotificationResponse(
    Guid Id, FleetAlertSeverity Severity, string Title, string Message, string? Link, DateTime CreatedAt, DateTime? ReadAt);

public sealed record NotificationPage(IReadOnlyList<NotificationResponse> Items, int UnreadCount, bool HasMore);

/// <summary>The current user's own notifications (the bell). Nobody reads another user's inbox.</summary>
public sealed class NotificationService(IFleetDbContext db, ICurrentUser currentUser, IClock clock)
{
    public const int PageSize = 20;

    private IQueryable<UserNotification> Mine() => db.UserNotifications.Where(n => n.UserId == currentUser.UserId);

    public async Task<NotificationPage> ListAsync(bool unreadOnly, int page, CancellationToken ct)
    {
        var query = Mine();
        if (unreadOnly) query = query.Where(n => n.ReadAt == null);
        var skip = (Math.Max(1, page) - 1) * PageSize;
        var items = await query.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id).Skip(skip).Take(PageSize + 1)
            .Select(n => new NotificationResponse(n.Id, n.Severity, n.Title, n.Message, n.Link, n.CreatedAt, n.ReadAt))
            .ToListAsync(ct);
        return new NotificationPage(items.Take(PageSize).ToList(), await UnreadCountAsync(ct), items.Count > PageSize);
    }

    public Task<int> UnreadCountAsync(CancellationToken ct) => Mine().CountAsync(n => n.ReadAt == null, ct);

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var notification = await Mine().SingleOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new NotFoundException("Notificação não encontrada.");
        notification.ReadAt ??= clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        foreach (var notification in await Mine().Where(n => n.ReadAt == null).ToListAsync(ct)) notification.ReadAt = now;
        await db.SaveChangesAsync(ct);
    }
}
