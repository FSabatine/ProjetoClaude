using Fleet.Application.Common;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Operations;
using Fleet.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Intelligence;

public sealed record AutomationRuleRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public AutomationTrigger? Trigger { get; init; }
    public OperationalEventType? EventType { get; init; }
    public decimal? Threshold { get; init; }
    public int? LookbackDays { get; init; }
    public FleetAlertSeverity Severity { get; init; } = FleetAlertSeverity.Warning;
    public bool CreateAlert { get; init; } = true;
    public AutomationNotifyMode NotifyMode { get; init; } = AutomationNotifyMode.None;
    public Guid? NotifyUserId { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record AutomationExecutionResponse(
    DateTime StartedAt, DateTime FinishedAt, AutomationRunSource Source, int Evaluated, int AlertsCreated, int AlertsUpdated,
    int AlertsResolved, int NotificationsCreated, string? Error);

public sealed record AutomationRuleResponse(
    Guid Id, string Name, string? Description, AutomationTrigger Trigger, AutomationTriggerKind Kind, OperationalEventType? EventType,
    decimal? Threshold, int? LookbackDays, FleetAlertSeverity Severity, bool CreateAlert, AutomationNotifyMode NotifyMode,
    Guid? NotifyUserId, string? NotifyUserName, bool IsActive, bool IsSystemDefault, int OpenAlerts, AutomationExecutionResponse? LastExecution);

public sealed record AutomationCatalogResponse(
    IReadOnlyList<AutomationTriggerDefinition> Triggers, IReadOnlyList<OperationalEventType> Events);

public sealed class AutomationRuleRequestValidator : AbstractValidator<AutomationRuleRequest>
{
    public AutomationRuleRequestValidator()
    {
        RuleFor(x => x.Name).Required("Nome").MaxLen(AutomationRule.NameMaxLength);
        RuleFor(x => x.Description).MaxLen(AutomationRule.DescriptionMaxLength);
        RuleFor(x => x.Trigger).NotNull().WithMessage("Quando: escolha o que a regra deve observar.");
        RuleFor(x => x.Severity).IsInEnum().WithMessage("Gravidade inválida.");
        RuleFor(x => x.NotifyMode).IsInEnum().WithMessage("Forma de aviso inválida.");
        RuleFor(x => x.EventType).NotNull().WithMessage("Fato: escolha o acontecimento que dispara a regra.")
            .Must(e => e is null || AutomationTriggerCatalog.NotifiableEvents.Contains(e.Value))
            .WithMessage("Este acontecimento não pode disparar regras. Escolha um da lista.")
            .When(x => x.Trigger == AutomationTrigger.OperationalEvent);
        RuleFor(x => x.NotifyUserId).NotNull().WithMessage("Usuário: escolha quem será avisado.")
            .When(x => x.NotifyMode == AutomationNotifyMode.SpecificUser);
        RuleFor(x => x.Threshold).Must((x, value) => ThresholdInRange(x.Trigger, value))
            .WithMessage(x => ThresholdMessage(x.Trigger)).When(x => x.Trigger is not null && x.Trigger != AutomationTrigger.OperationalEvent);
        RuleFor(x => x.LookbackDays).InclusiveBetween(7, AutomationTriggerCatalog.MaxLookbackDays)
            .WithMessage($"Período analisado: de 7 a {AutomationTriggerCatalog.MaxLookbackDays} dias.").When(x => x.LookbackDays.HasValue);
        RuleFor(x => x).Must(x => x.CreateAlert || x.NotifyMode != AutomationNotifyMode.None)
            .OverridePropertyName("notifyMode").WithMessage("Então: a regra precisa criar um alerta, avisar alguém, ou os dois.");
        RuleFor(x => x.CreateAlert).Equal(true)
            .WithMessage("Regras de condição sempre criam um alerta (é ele que evita avisos repetidos).")
            .When(x => x.Trigger is not null && x.Trigger != AutomationTrigger.OperationalEvent);
    }

    private static bool ThresholdInRange(AutomationTrigger? trigger, decimal? value)
    {
        var d = AutomationTriggerCatalog.Get(trigger!.Value);
        if (d.DefaultThreshold is null) return value is null;
        return value is null || (value >= d.MinThreshold && value <= d.MaxThreshold);
    }

    private static string ThresholdMessage(AutomationTrigger? trigger)
    {
        var d = AutomationTriggerCatalog.Get(trigger!.Value);
        return d.DefaultThreshold is null
            ? "Esta regra não usa limite."
            : $"{d.ThresholdLabel}: informe de {d.MinThreshold:0.##} a {d.MaxThreshold:0.##} {d.ThresholdUnit}.";
    }
}

/// <summary>Automation rules of the company (ADR-045). The default rules are created on first use, per company.</summary>
public sealed class AutomationRuleService(IFleetDbContext db, ICurrentUser currentUser, IClock clock, IValidator<AutomationRuleRequest> validator)
{
    public const string DeactivatedNote = "Encerrado porque a regra que o gerou foi desativada ou excluída.";

    public static AutomationCatalogResponse Catalog() => new(AutomationTriggerCatalog.All, AutomationTriggerCatalog.NotifiableEvents);

    public async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        // IgnoreQueryFilters: a deleted default must not be re-created (only deactivation is offered, but stay safe).
        // Tenant is kept by the explicit CompanyId condition.
        var companyId = currentUser.CompanyId;
        var existing = await db.AutomationRules.IgnoreQueryFilters()
            .Where(r => r.CompanyId == companyId && r.IsSystemDefault)
            .Select(r => new { r.Trigger, r.EventType })
            .ToListAsync(ct);
        var missing = AutomationTriggerCatalog.DefaultRules()
            .Where(d => !existing.Any(e => e.Trigger == d.Trigger && e.EventType == d.EventType))
            .ToList();
        if (missing.Count == 0) return;
        db.AutomationRules.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AutomationRuleResponse>> ListAsync(CancellationToken ct)
    {
        await EnsureDefaultsAsync(ct);
        var rules = await db.AutomationRules.OrderByDescending(r => r.IsActive).ThenBy(r => r.Name).ToListAsync(ct);
        return await ToResponsesAsync(rules, ct);
    }

    public async Task<AutomationRuleResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await ToResponsesAsync([await FindAsync(id, ct)], ct))[0];

    public async Task<AutomationRuleResponse> CreateAsync(AutomationRuleRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await EnsureRecipientAsync(request, ct);
        var rule = new AutomationRule();
        Apply(rule, request);
        db.AutomationRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return await GetAsync(rule.Id, ct);
    }

    public async Task<AutomationRuleResponse> UpdateAsync(Guid id, AutomationRuleRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var rule = await FindAsync(id, ct);
        if (rule.IsSystemDefault && (request.Trigger != rule.Trigger || request.EventType != rule.EventType))
            throw new BusinessRuleException("Não é possível trocar o gatilho de uma regra padrão. Crie uma regra nova para observar outra coisa.");
        await EnsureRecipientAsync(request, ct);
        var wasActive = rule.IsActive;
        Apply(rule, request);
        if (wasActive && !rule.IsActive) await CloseOpenAlertsAsync(rule.Id, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(rule.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var rule = await FindAsync(id, ct);
        if (rule.IsSystemDefault)
            throw new BusinessRuleException("Regras padrão não podem ser excluídas. Desative a regra se não quiser mais esses alertas.");
        await CloseOpenAlertsAsync(rule.Id, ct);
        db.AutomationRules.Remove(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AutomationExecutionResponse>> ExecutionsAsync(Guid ruleId, CancellationToken ct)
    {
        await FindAsync(ruleId, ct);
        return await db.AutomationExecutions.Where(e => e.AutomationRuleId == ruleId)
            .OrderByDescending(e => e.StartedAt).Take(20)
            .Select(e => new AutomationExecutionResponse(e.StartedAt, e.FinishedAt, e.Source, e.Evaluated, e.AlertsCreated,
                e.AlertsUpdated, e.AlertsResolved, e.NotificationsCreated, e.Error))
            .ToListAsync(ct);
    }

    private async Task CloseOpenAlertsAsync(Guid ruleId, CancellationToken ct)
    {
        var open = await db.FleetAlerts
            .Where(a => a.AutomationRuleId == ruleId && FleetAlertWorkflow.OpenStatuses.Contains(a.Status)).ToListAsync(ct);
        foreach (var alert in open)
        {
            alert.Status = FleetAlertStatus.Dismissed;
            alert.ClosedAt = clock.UtcNow;
            alert.ClosedBy = currentUser.UserId;
            alert.ClosingNotes = DeactivatedNote;
        }
    }

    /// <summary>The chosen user must be an active user of this company (users are not tenant-filtered).</summary>
    private async Task EnsureRecipientAsync(AutomationRuleRequest request, CancellationToken ct)
    {
        if (request.NotifyMode != AutomationNotifyMode.SpecificUser) return;
        var exists = await db.Users.AnyAsync(u => u.Id == request.NotifyUserId && u.CompanyId == currentUser.CompanyId && u.Status == UserStatus.Active, ct);
        if (!exists) throw new ValidationException([new FluentValidation.Results.ValidationFailure("notifyUserId", "Usuário não encontrado ou inativo nesta empresa.")]);
    }

    private static void Apply(AutomationRule rule, AutomationRuleRequest r)
    {
        var definition = AutomationTriggerCatalog.Get(r.Trigger!.Value);
        rule.Name = r.Name!.Trim();
        rule.Description = r.Description.TrimToNull();
        rule.Trigger = r.Trigger!.Value;
        rule.EventType = r.Trigger == AutomationTrigger.OperationalEvent ? r.EventType : null;
        rule.Threshold = definition.DefaultThreshold is null ? null : r.Threshold ?? definition.DefaultThreshold;
        rule.LookbackDays = definition.DefaultLookbackDays is null ? null : r.LookbackDays ?? definition.DefaultLookbackDays;
        rule.Severity = r.Severity;
        rule.CreateAlert = r.CreateAlert;
        rule.NotifyMode = r.NotifyMode;
        rule.NotifyUserId = r.NotifyMode == AutomationNotifyMode.SpecificUser ? r.NotifyUserId : null;
        rule.IsActive = r.IsActive;
    }

    private async Task<AutomationRule> FindAsync(Guid id, CancellationToken ct) =>
        await db.AutomationRules.SingleOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException("Regra de automação não encontrada. Ela pode ter sido excluída.");

    private async Task<IReadOnlyList<AutomationRuleResponse>> ToResponsesAsync(IReadOnlyList<AutomationRule> rules, CancellationToken ct)
    {
        var ids = rules.Select(r => r.Id).ToList();
        var openCounts = await db.FleetAlerts
            .Where(a => a.AutomationRuleId != null && ids.Contains(a.AutomationRuleId.Value) && FleetAlertWorkflow.OpenStatuses.Contains(a.Status))
            .GroupBy(a => a.AutomationRuleId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        var lastStarts = await db.AutomationExecutions.Where(e => ids.Contains(e.AutomationRuleId))
            .GroupBy(e => e.AutomationRuleId).Select(g => new { g.Key, Last = g.Max(e => e.StartedAt) })
            .ToListAsync(ct);
        var lastExecutions = new Dictionary<Guid, AutomationExecutionResponse>();
        foreach (var last in lastStarts)
        {
            var e = await db.AutomationExecutions.Where(x => x.AutomationRuleId == last.Key && x.StartedAt == last.Last).FirstAsync(ct);
            lastExecutions[last.Key] = new AutomationExecutionResponse(e.StartedAt, e.FinishedAt, e.Source, e.Evaluated, e.AlertsCreated,
                e.AlertsUpdated, e.AlertsResolved, e.NotificationsCreated, e.Error);
        }
        var userIds = rules.Where(r => r.NotifyUserId != null).Select(r => r.NotifyUserId!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id) && u.CompanyId == currentUser.CompanyId)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return rules.Select(r => new AutomationRuleResponse(
            r.Id, r.Name, r.Description, r.Trigger, AutomationTriggerCatalog.Get(r.Trigger).Kind, r.EventType, r.Threshold, r.LookbackDays,
            r.Severity, r.CreateAlert, r.NotifyMode, r.NotifyUserId, r.NotifyUserId is { } u ? names.GetValueOrDefault(u) : null,
            r.IsActive, r.IsSystemDefault, openCounts.GetValueOrDefault(r.Id), lastExecutions.GetValueOrDefault(r.Id))).ToList();
    }
}
