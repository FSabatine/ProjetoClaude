using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Operations;

namespace Fleet.Domain.Intelligence;

/// <summary>What a rule watches. Scheduled triggers are conditions re-evaluated by the scan; OperationalEvent reacts to the outbox.</summary>
public enum AutomationTrigger
{
    MaintenanceOverdue,
    MaintenanceDueSoon,
    FuelConsumptionAbnormal,
    VehicleCostAboveAverage,
    BudgetThreshold,
    TireTreadLow,
    ExpenseOverdue,
    DocumentExpiring,
    OperationalEvent,
}

public enum AutomationTriggerKind
{
    Scheduled,
    Event,
}

public enum AutomationNotifyMode
{
    /// <summary>Only the alert (no personal notification).</summary>
    None,
    /// <summary>Every active user who can see the alert.</summary>
    EveryoneWhoCanSee,
    /// <summary>One chosen user (only if that user can see the alert).</summary>
    SpecificUser,
}

public enum AutomationRunSource
{
    Scheduled,
    Manual,
}

/// <summary>
/// WHEN (trigger + condition parameters) → THEN (create alert and/or notify) — spec §8, ADR-045. Rules are data per
/// company; the behaviour of each trigger is code (one detector per trigger), so a new automation is a new rule row,
/// and a new kind of condition is one new detector, never a hardcoded job.
/// </summary>
public class AutomationRule : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AutomationTrigger Trigger { get; set; }
    /// <summary>Only for <see cref="AutomationTrigger.OperationalEvent"/>.</summary>
    public OperationalEventType? EventType { get; set; }
    /// <summary>Meaning depends on the trigger (percent, days…) — see <see cref="AutomationTriggerCatalog"/>.</summary>
    public decimal? Threshold { get; set; }
    public int? LookbackDays { get; set; }
    public FleetAlertSeverity Severity { get; set; } = FleetAlertSeverity.Warning;
    public bool CreateAlert { get; set; } = true;
    public AutomationNotifyMode NotifyMode { get; set; } = AutomationNotifyMode.None;
    public Guid? NotifyUserId { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Created by the system for every company; can be tuned or deactivated, not deleted.</summary>
    public bool IsSystemDefault { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>One evaluation of one rule (the "Execution" of spec §8). Append-only log, not audited.</summary>
public class AutomationExecution : ITenantScoped
{
    public const int ErrorMaxLength = 500;

    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AutomationRuleId { get; set; }
    public AutomationRunSource Source { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public int Evaluated { get; set; }
    public int AlertsCreated { get; set; }
    public int AlertsUpdated { get; set; }
    public int AlertsResolved { get; set; }
    public int NotificationsCreated { get; set; }
    public string? Error { get; set; }
}

public sealed record AutomationTriggerDefinition(
    AutomationTrigger Trigger,
    AutomationTriggerKind Kind,
    FleetAlertCategory Category,
    string Name,
    string Description,
    FleetAlertSeverity DefaultSeverity,
    string? ThresholdLabel,
    string? ThresholdUnit,
    decimal? DefaultThreshold,
    decimal? MinThreshold,
    decimal? MaxThreshold,
    string? LookbackLabel,
    int? DefaultLookbackDays,
    AutomationNotifyMode DefaultNotifyMode);

/// <summary>Catalog of triggers: labels/limits for the rule form and the default rules every company gets.</summary>
public static class AutomationTriggerCatalog
{
    public const int MaxLookbackDays = 365;

    public static readonly IReadOnlyList<AutomationTriggerDefinition> All =
    [
        new(AutomationTrigger.MaintenanceOverdue, AutomationTriggerKind.Scheduled, FleetAlertCategory.Maintenance,
            "Manutenção preventiva atrasada",
            "Um item do plano de manutenção passou do vencimento (km, horas ou data), já contando a carência do plano.",
            FleetAlertSeverity.Critical, null, null, null, null, null, null, null, AutomationNotifyMode.EveryoneWhoCanSee),
        new(AutomationTrigger.MaintenanceDueSoon, AutomationTriggerKind.Scheduled, FleetAlertCategory.Maintenance,
            "Manutenção preventiva próxima",
            "Um item do plano de manutenção está na janela de aviso (vencendo) ou vence hoje.",
            FleetAlertSeverity.Warning, null, null, null, null, null, null, null, AutomationNotifyMode.None),
        new(AutomationTrigger.FuelConsumptionAbnormal, AutomationTriggerKind.Scheduled, FleetAlertCategory.Fuel,
            "Consumo de combustível fora do padrão do veículo",
            "Compara o consumo médio (tanque cheio a tanque cheio) do período com a média anterior do próprio veículo.",
            FleetAlertSeverity.Warning, "Variação mínima", "%", 15m, 5m, 100m, "Período analisado", 30, AutomationNotifyMode.None),
        new(AutomationTrigger.VehicleCostAboveAverage, AutomationTriggerKind.Scheduled, FleetAlertCategory.Finance,
            "Custo do veículo acima da média do tipo",
            "Compara o custo total do veículo no período (combustível, manutenção, pneus e despesas) com a média dos veículos ativos do mesmo tipo.",
            FleetAlertSeverity.Warning, "Acima da média em", "%", 30m, 10m, 500m, "Período analisado", 90, AutomationNotifyMode.None),
        new(AutomationTrigger.BudgetThreshold, AutomationTriggerKind.Scheduled, FleetAlertCategory.Finance,
            "Orçamento perto do limite",
            "Avisa quando o realizado de um orçamento do mês ou do ano atinge o percentual configurado. Acima de 100% o alerta é crítico.",
            FleetAlertSeverity.Warning, "Utilização a partir de", "%", 90m, 50m, 200m, null, null, AutomationNotifyMode.EveryoneWhoCanSee),
        new(AutomationTrigger.TireTreadLow, AutomationTriggerKind.Scheduled, FleetAlertCategory.Tires,
            "Pneu com sulco baixo",
            "Pneu instalado com sulco no limite de aviso configurado pela empresa (crítico quando chega ao mínimo).",
            FleetAlertSeverity.Warning, null, null, null, null, null, null, null, AutomationNotifyMode.None),
        new(AutomationTrigger.ExpenseOverdue, AutomationTriggerKind.Scheduled, FleetAlertCategory.Finance,
            "Despesa em atraso",
            "Despesa não paga com vencimento passado há pelo menos o número de dias configurado.",
            FleetAlertSeverity.Warning, "Dias de atraso a partir de", "dias", 1m, 0m, 90m, null, null, AutomationNotifyMode.None),
        new(AutomationTrigger.DocumentExpiring, AutomationTriggerKind.Scheduled, FleetAlertCategory.Documents,
            "Documento vencido ou vencendo",
            "Documento de veículo, implemento, motorista ou da empresa vencido ou dentro do aviso configurado no tipo de documento.",
            FleetAlertSeverity.Warning, null, null, null, null, null, null, null, AutomationNotifyMode.None),
        new(AutomationTrigger.OperationalEvent, AutomationTriggerKind.Event, FleetAlertCategory.Operations,
            "Quando um fato acontecer",
            "Reage a um acontecimento registrado pelo sistema (checklist reprovado, ocorrência registrada, leitura suspeita…).",
            FleetAlertSeverity.Warning, null, null, null, null, null, null, null, AutomationNotifyMode.EveryoneWhoCanSee),
    ];

    /// <summary>Events offered as triggers: facts someone may need to act on (not every bookkeeping event).</summary>
    public static readonly IReadOnlyList<OperationalEventType> NotifiableEvents =
    [
        OperationalEventType.ChecklistFailed,
        OperationalEventType.OccurrenceCreated,
        OperationalEventType.MileageAnomalyDetected,
        OperationalEventType.HourMeterAnomalyDetected,
        OperationalEventType.MaintenanceRequestCreated,
        OperationalEventType.WorkOrderCompleted,
        OperationalEventType.FuelingMarkedForReview,
        OperationalEventType.FuelConsumptionAnomalyDetected,
        OperationalEventType.TireInspectionFailed,
        OperationalEventType.TireAnomalyDetected,
        OperationalEventType.DocumentExpired,
        OperationalEventType.BudgetExceeded,
    ];

    public static AutomationTriggerDefinition Get(AutomationTrigger trigger) => All.Single(d => d.Trigger == trigger);

    /// <summary>The rules every company starts with (tunable, deactivatable).</summary>
    public static IEnumerable<AutomationRule> DefaultRules()
    {
        foreach (var d in All.Where(d => d.Kind == AutomationTriggerKind.Scheduled))
        {
            yield return new AutomationRule
            {
                Name = d.Name, Description = d.Description, Trigger = d.Trigger, Threshold = d.DefaultThreshold,
                LookbackDays = d.DefaultLookbackDays, Severity = d.DefaultSeverity, CreateAlert = true,
                NotifyMode = d.DefaultNotifyMode, IsActive = true, IsSystemDefault = true,
            };
        }
        yield return new AutomationRule
        {
            Name = "Checklist reprovado", Description = "Avisa quem cuida da operação quando um checklist tem item reprovado.",
            Trigger = AutomationTrigger.OperationalEvent, EventType = OperationalEventType.ChecklistFailed,
            Severity = FleetAlertSeverity.Warning, CreateAlert = false, NotifyMode = AutomationNotifyMode.EveryoneWhoCanSee,
            IsActive = true, IsSystemDefault = true,
        };
    }

    /// <summary>Who may see what an event says (the event summary may name a driver or a vehicle).</summary>
    public static AlertAudience EventAudience(OperationalEventType type) => type switch
    {
        OperationalEventType.VehicleAssigned or OperationalEventType.VehicleAssignmentEnded => AlertAudience.Assignments,
        OperationalEventType.ChecklistCompleted or OperationalEventType.ChecklistFailed => AlertAudience.Checklists,
        OperationalEventType.OccurrenceCreated or OperationalEventType.OccurrenceStatusChanged => AlertAudience.Occurrences,
        OperationalEventType.DocumentCreated or OperationalEventType.DocumentRenewed or OperationalEventType.DocumentDeleted
            or OperationalEventType.DocumentExpiring or OperationalEventType.DocumentExpired => AlertAudience.DriverDocuments,
        OperationalEventType.MileageRecorded or OperationalEventType.MileageAnomalyDetected or OperationalEventType.MileageCorrected
            or OperationalEventType.MileageReviewed => AlertAudience.Mileage,
        >= OperationalEventType.HourMeterRecorded and <= OperationalEventType.WorkOrderCompleted => AlertAudience.Maintenance,
        >= OperationalEventType.FuelingRecorded and <= OperationalEventType.FuelPriceChanged => AlertAudience.Fuel,
        >= OperationalEventType.TireRegistered and <= OperationalEventType.TireLayoutChanged => AlertAudience.Tires,
        >= OperationalEventType.ExpenseCreated and <= OperationalEventType.BudgetExceeded => AlertAudience.FinanceView,
        _ => AlertAudience.Vehicles,
    };

    public static FleetAlertCategory EventCategory(OperationalEventType type) => EventAudience(type) switch
    {
        AlertAudience.Maintenance => FleetAlertCategory.Maintenance,
        AlertAudience.Fuel => FleetAlertCategory.Fuel,
        AlertAudience.Tires => FleetAlertCategory.Tires,
        AlertAudience.FinanceView => FleetAlertCategory.Finance,
        AlertAudience.DriverDocuments => FleetAlertCategory.Documents,
        _ => FleetAlertCategory.Operations,
    };
}

/// <summary>Permissions behind each audience. A user sees an alert only when holding ALL of them (plus alerts.view).</summary>
public static class AlertAudiences
{
    public static IReadOnlyList<string> RequiredPermissions(AlertAudience audience) => audience switch
    {
        AlertAudience.Maintenance => [Permissions.Maintenance.View],
        AlertAudience.Fuel => [Permissions.Fuel.View],
        AlertAudience.Tires => [Permissions.Tires.View],
        AlertAudience.FinanceView => [Permissions.Finance.View],
        AlertAudience.FleetCosts =>
        [
            Permissions.Finance.ViewCosts, Permissions.Fuel.ViewCosts, Permissions.Maintenance.ViewCosts, Permissions.Tires.ViewCosts,
        ],
        AlertAudience.Documents => [Permissions.Documents.View],
        AlertAudience.DriverDocuments => [Permissions.Documents.View, Permissions.Drivers.View],
        AlertAudience.Occurrences => [Permissions.Occurrences.View],
        AlertAudience.Checklists => [Permissions.Checklists.View],
        AlertAudience.Mileage => [Permissions.Vehicles.View],
        AlertAudience.Assignments => [Permissions.Assignments.View],
        _ => [Permissions.Vehicles.View],
    };

    public static IReadOnlyList<AlertAudience> VisibleTo(IReadOnlySet<string> permissions)
    {
        if (!permissions.Contains(Permissions.Alerts.View)) return [];
        return Enum.GetValues<AlertAudience>().Where(a => RequiredPermissions(a).All(permissions.Contains)).ToList();
    }
}
