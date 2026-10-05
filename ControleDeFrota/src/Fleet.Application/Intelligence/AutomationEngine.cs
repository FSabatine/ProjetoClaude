using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Operations;
using Fleet.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Application.Intelligence;

public sealed record AutomationRunResult(
    int RulesEvaluated, int AlertsCreated, int AlertsUpdated, int AlertsResolved, int NotificationsCreated, int EventsProcessed,
    int RulesFailed, DateTime FinishedAt);

/// <summary>
/// Runs the automation rules of the current company (ADR-045):
/// scheduled rules → detector → alerts created / refreshed / closed automatically, then notifications;
/// event rules → read the OperationalEvents outbox (PublishedAt = null) → alert and/or notification → stamp PublishedAt.
/// Must run with every permission of the company (system context): the produced alerts are filtered per reader later.
/// </summary>
public sealed class AutomationEngine(
    IFleetDbContext db, IClock clock, ICurrentUser currentUser, IEnumerable<IAlertDetector> detectors,
    AutomationRuleService rules, ILogger<AutomationEngine> logger)
{
    /// <summary>A dismissed finding stays quiet this long before the scan may raise it again.</summary>
    public const int DismissSuppressionDays = 30;
    /// <summary>Above this many new alerts of one rule in a run, each user gets one summary notification instead.</summary>
    public const int MaxIndividualNotifications = 3;
    public const int MaxNewAlertsPerRule = 200;
    public const int EventBatchSize = 500;
    public const int MaxEventBatches = 20;

    public const string AutoResolvedNote =
        "Encerrado automaticamente: na última verificação a condição deste alerta não se aplicava mais (foi resolvida ou evoluiu para outro alerta).";

    private int _created, _updated, _resolved, _notified, _events, _evaluated, _failed;

    public async Task<AutomationRunResult> RunAsync(AutomationRunSource source, CancellationToken ct)
    {
        var companyId = currentUser.CompanyId ?? throw new InvalidOperationException("Automation runs for one company.");
        await rules.EnsureDefaultsAsync(ct);

        var active = await db.AutomationRules.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync(ct);
        var recipients = await RecipientsAsync(companyId, ct);

        foreach (var rule in active.Where(r => r.Trigger != AutomationTrigger.OperationalEvent))
            await RunScheduledRuleAsync(rule, source, recipients, ct);

        await RunEventRulesAsync(active.Where(r => r.Trigger == AutomationTrigger.OperationalEvent).ToList(), source, recipients, ct);

        var result = new AutomationRunResult(_evaluated, _created, _updated, _resolved, _notified, _events, _failed, clock.UtcNow);
        logger.LogInformation(
            "Automation run for {CompanyId}: {Rules} rules, {Created} alerts created, {Resolved} closed, {Notifications} notifications, {Events} events",
            companyId, result.RulesEvaluated, result.AlertsCreated, result.AlertsResolved, result.NotificationsCreated, result.EventsProcessed);
        return result;
    }

    private async Task RunScheduledRuleAsync(AutomationRule rule, AutomationRunSource source, IReadOnlyList<Recipient> recipients, CancellationToken ct)
    {
        var detector = detectors.FirstOrDefault(d => d.Trigger == rule.Trigger);
        if (detector is null) return;
        var execution = new AutomationExecution { AutomationRuleId = rule.Id, Source = source, StartedAt = clock.UtcNow };
        _evaluated++;
        try
        {
            var candidates = (await detector.DetectAsync(rule, ct))
                .GroupBy(c => c.DedupKey).Select(g => g.First()).ToList();
            execution.Evaluated = candidates.Count;
            var created = await ApplyCandidatesAsync(rule, candidates, execution, ct);
            execution.NotificationsCreated = Notify(rule, created, recipients);
            execution.FinishedAt = clock.UtcNow;
            db.AutomationExecutions.Add(execution);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One failing rule must not stop the others; nothing of it was saved, so its alerts stay as they were.
            _failed++;
            logger.LogError(ex, "Automation rule {RuleId} ({Trigger}) failed", rule.Id, rule.Trigger);
            DiscardPendingChanges();
            db.AutomationExecutions.Add(new AutomationExecution
            {
                AutomationRuleId = rule.Id, Source = source, StartedAt = execution.StartedAt, FinishedAt = clock.UtcNow,
                Error = "A verificação desta regra falhou. Ela será tentada de novo na próxima execução.",
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<List<FleetAlert>> ApplyCandidatesAsync(
        AutomationRule rule, IReadOnlyList<AlertCandidate> candidates, AutomationExecution execution, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var definition = AutomationTriggerCatalog.Get(rule.Trigger);
        var open = await db.FleetAlerts
            .Where(a => a.AutomationRuleId == rule.Id && FleetAlertWorkflow.OpenStatuses.Contains(a.Status))
            .ToListAsync(ct);
        var openByKey = open.ToDictionary(a => a.DedupKey);

        var newKeys = candidates.Select(c => c.DedupKey).Where(k => !openByKey.ContainsKey(k)).ToList();
        var history = await db.FleetAlerts
            .Where(a => a.AutomationRuleId == rule.Id && newKeys.Contains(a.DedupKey) && !FleetAlertWorkflow.OpenStatuses.Contains(a.Status))
            .GroupBy(a => a.DedupKey)
            .Select(g => new
            {
                Key = g.Key,
                Resolved = g.Count(a => a.Status == FleetAlertStatus.Resolved),
                LastDismissedAt = g.Where(a => a.Status == FleetAlertStatus.Dismissed).Max(a => a.ClosedAt),
            })
            .ToDictionaryAsync(h => h.Key, ct);

        var created = new List<FleetAlert>();
        foreach (var candidate in candidates)
        {
            var severity = candidate.Severity ?? rule.Severity;
            if (openByKey.TryGetValue(candidate.DedupKey, out var existing))
            {
                Fill(existing, candidate, severity);
                existing.LastDetectedAt = now;
                existing.DetectionCount++;
                existing.Priority = AlertPriority.Compute(severity, candidate.Impact, candidate.Urgency, existing.RecurrenceCount);
                execution.AlertsUpdated++;
                _updated++;
                continue;
            }

            var past = history.GetValueOrDefault(candidate.DedupKey);
            if (past?.LastDismissedAt is { } dismissedAt && dismissedAt > now.AddDays(-DismissSuppressionDays)) continue;
            if (created.Count >= MaxNewAlertsPerRule) continue;

            var alert = new FleetAlert
            {
                AutomationRuleId = rule.Id, Trigger = rule.Trigger, Category = definition.Category,
                DedupKey = candidate.DedupKey, DetectedAt = now, LastDetectedAt = now, RecurrenceCount = past?.Resolved ?? 0,
            };
            Fill(alert, candidate, severity);
            alert.Priority = AlertPriority.Compute(severity, candidate.Impact, candidate.Urgency, alert.RecurrenceCount);
            db.FleetAlerts.Add(alert);
            created.Add(alert);
        }

        var stillFound = candidates.Select(c => c.DedupKey).ToHashSet();
        foreach (var gone in open.Where(a => !stillFound.Contains(a.DedupKey)))
        {
            gone.Status = FleetAlertStatus.Resolved;
            gone.AutoResolved = true;
            gone.ClosedAt = now;
            gone.ClosingNotes = AutoResolvedNote;
            execution.AlertsResolved++;
            _resolved++;
        }

        execution.AlertsCreated = created.Count;
        _created += created.Count;
        return created;
    }

    private static void Fill(FleetAlert alert, AlertCandidate c, FleetAlertSeverity severity)
    {
        alert.Audience = c.Audience;
        alert.Severity = severity;
        alert.Title = Cut(c.Title, FleetAlert.TitleMaxLength);
        alert.Explanation = Cut(c.Explanation, FleetAlert.TextMaxLength);
        alert.Evidence = Cut(c.Evidence, FleetAlert.TextMaxLength);
        alert.RecommendedAction = Cut(c.RecommendedAction, FleetAlert.ActionMaxLength);
        alert.EntityType = c.EntityType;
        alert.EntityId = c.EntityId;
        alert.VehicleId = c.VehicleId;
        alert.Tab = c.Tab;
    }

    // ---- Event rules (outbox)

    private async Task RunEventRulesAsync(
        IReadOnlyList<AutomationRule> eventRules, AutomationRunSource source, IReadOnlyList<Recipient> recipients, CancellationToken ct)
    {
        var executions = eventRules.ToDictionary(r => r.Id, r => new AutomationExecution
        {
            AutomationRuleId = r.Id, Source = source, StartedAt = clock.UtcNow,
        });
        for (var batch = 0; batch < MaxEventBatches; batch++)
        {
            var events = await db.OperationalEvents.Where(e => e.PublishedAt == null)
                .OrderBy(e => e.Id).Take(EventBatchSize).ToListAsync(ct);
            if (events.Count == 0) break;

            var now = clock.UtcNow;
            foreach (var evt in events)
            {
                // Rules only react to what happens after they exist: turning a rule on must not replay old history.
                foreach (var rule in eventRules.Where(r => r.EventType == evt.Type && evt.OccurredAt >= r.CreatedAt))
                {
                    var execution = executions[rule.Id];
                    execution.Evaluated++;
                    var alert = rule.CreateAlert ? CreateEventAlert(rule, evt, now) : null;
                    if (alert is not null) execution.AlertsCreated++;
                    execution.NotificationsCreated += NotifyEvent(rule, evt, alert, recipients);
                }
                evt.PublishedAt = now;
            }
            _events += events.Count;
            await db.SaveChangesAsync(ct);
        }

        foreach (var execution in executions.Values.Where(e => e.Evaluated > 0))
        {
            execution.FinishedAt = clock.UtcNow;
            db.AutomationExecutions.Add(execution);
            _evaluated++;
        }
        await db.SaveChangesAsync(ct);
    }

    private FleetAlert CreateEventAlert(AutomationRule rule, OperationalEvent evt, DateTime now)
    {
        var alert = new FleetAlert
        {
            AutomationRuleId = rule.Id, Trigger = AutomationTrigger.OperationalEvent,
            Category = AutomationTriggerCatalog.EventCategory(evt.Type), Audience = AutomationTriggerCatalog.EventAudience(evt.Type),
            Severity = rule.Severity, DedupKey = $"event:{evt.Id}", DetectedAt = now, LastDetectedAt = now,
            Title = Cut(rule.Name, FleetAlert.TitleMaxLength),
            Explanation = Cut(evt.Summary, FleetAlert.TextMaxLength),
            Evidence = $"Registrado pelo sistema em {clock.FormatDateTime(evt.OccurredAt)}.",
            RecommendedAction = "Abra o registro para analisar o que aconteceu e tome a providência necessária.",
            EntityType = evt.SubjectType, EntityId = evt.SubjectId, VehicleId = evt.VehicleId,
        };
        alert.Priority = AlertPriority.Compute(rule.Severity, 10, 15, 0);
        db.FleetAlerts.Add(alert);
        _created++;
        return alert;
    }

    // ---- Notifications

    private sealed record Recipient(Guid UserId, IReadOnlySet<string> Permissions);

    /// <summary>Active users of the company with their effective permissions (users are not tenant-filtered — filtered here).</summary>
    private async Task<IReadOnlyList<Recipient>> RecipientsAsync(Guid companyId, CancellationToken ct)
    {
        var rows = await (
                from user in db.Users
                where user.CompanyId == companyId && user.Status == UserStatus.Active
                join userRole in db.UserRoles on user.Id equals userRole.UserId
                join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
                select new { user.Id, rolePermission.Permission.Key })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.Id)
            .Select(g => new Recipient(g.Key, g.Select(r => r.Key).ToHashSet(StringComparer.Ordinal)))
            .ToList();
    }

    private static IEnumerable<Recipient> WhoCanSee(AutomationRule rule, AlertAudience audience, IReadOnlyList<Recipient> recipients)
    {
        var required = AlertAudiences.RequiredPermissions(audience).Append(Permissions.Alerts.View).ToList();
        var allowed = recipients.Where(r => required.All(r.Permissions.Contains));
        return rule.NotifyMode switch
        {
            AutomationNotifyMode.EveryoneWhoCanSee => allowed,
            AutomationNotifyMode.SpecificUser => allowed.Where(r => r.UserId == rule.NotifyUserId),
            _ => [],
        };
    }

    private int Notify(AutomationRule rule, IReadOnlyList<FleetAlert> created, IReadOnlyList<Recipient> recipients)
    {
        if (rule.NotifyMode == AutomationNotifyMode.None || created.Count == 0) return 0;
        var perUser = created
            .SelectMany(a => WhoCanSee(rule, a.Audience, recipients).Select(r => (r.UserId, Alert: a)))
            .GroupBy(x => x.UserId);
        var count = 0;
        foreach (var group in perUser)
        {
            var alerts = group.Select(x => x.Alert).ToList();
            if (alerts.Count > MaxIndividualNotifications)
            {
                Add(group.Key, null, alerts.Max(a => a.Severity), $"{alerts.Count} novos alertas: {rule.Name}",
                    "Abra a central de alertas para ver a lista completa, do mais importante para o menos.", $"/alertas?ruleId={rule.Id}");
                count++;
                continue;
            }
            foreach (var alert in alerts)
            {
                Add(group.Key, alert.Id, alert.Severity, alert.Title, NotificationMessage(alert), $"/alertas/{alert.Id}");
                count++;
            }
        }
        return count;
    }

    private int NotifyEvent(AutomationRule rule, OperationalEvent evt, FleetAlert? alert, IReadOnlyList<Recipient> recipients)
    {
        var count = 0;
        foreach (var recipient in WhoCanSee(rule, AutomationTriggerCatalog.EventAudience(evt.Type), recipients))
        {
            // Operational event summaries never carry R$ values (convention since Phase 4), so they can be quoted.
            Add(recipient.UserId, alert?.Id, rule.Severity, rule.Name, evt.Summary, alert is null ? EventLinks.For(evt) : $"/alertas/{alert.Id}");
            count++;
        }
        return count;
    }

    private static string NotificationMessage(FleetAlert alert) => alert.Audience == AlertAudience.FleetCosts
        // The bell is outside the cost permissions' control: money stays inside the alert, behind its audience.
        ? "Abra o alerta para ver os detalhes e os valores."
        : alert.Explanation;

    private void Add(Guid userId, Guid? alertId, FleetAlertSeverity severity, string title, string message, string? link)
    {
        db.UserNotifications.Add(new UserNotification
        {
            Id = Guid.NewGuid(), UserId = userId, FleetAlertId = alertId, Severity = severity,
            Title = Cut(title, UserNotification.TitleMaxLength), Message = Cut(message, UserNotification.MessageMaxLength),
            Link = link, CreatedAt = clock.UtcNow,
        });
        _notified++;
    }

    private void DiscardPendingChanges()
    {
        if (db is not DbContext context) return;
        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
        {
            if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
            else entry.Reload();
        }
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}

/// <summary>Route of the record behind an event (in-app links only).</summary>
public static class EventLinks
{
    public static string For(OperationalEvent evt) => evt.SubjectType switch
    {
        "Occurrence" => $"/ocorrencias/{evt.SubjectId}",
        "ChecklistExecution" => $"/checklists/{evt.SubjectId}",
        "Fueling" => $"/abastecimentos/{evt.SubjectId}",
        "WorkOrder" => $"/ordens-servico/{evt.SubjectId}",
        "MaintenanceRequest" => "/solicitacoes-manutencao",
        "Expense" => $"/financeiro/despesas/{evt.SubjectId}/editar",
        "Budget" => "/financeiro/orcamentos",
        _ when evt.TireId is { } tireId => $"/pneus/{tireId}",
        _ when evt.VehicleId is { } vehicleId => $"/veiculos/{vehicleId}",
        _ when evt.DriverId is { } driverId => $"/motoristas/{driverId}",
        _ => "/",
    };
}
