using Fleet.Application.Common;
using Fleet.Application.Occurrences;
using Fleet.Domain.Authorization;
using Fleet.Domain.Drivers;
using Fleet.Domain.Fuel;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Intelligence;

/// <param name="Source">"alert" = open alerts of one trigger (refreshed by the scan); "queue" = live work waiting for someone.</param>
public sealed record AttentionItem(
    string Key, string Label, int Count, FleetAlertSeverity Severity, FleetAlertCategory Category, string Link, string Source);

public sealed record AttentionResponse(IReadOnlyList<AttentionItem> Items, int TotalCount, DateTime? AlertsCheckedAt);

/// <summary>
/// "Requer atenção" (spec §11): one prioritized list of what needs someone, each line leading to the records behind it.
/// Combines the open alerts grouped by trigger (no double counting with the alert center) with live work queues that are
/// not alerts (reviews and approvals waiting for a person). Every line respects the module permission behind it.
/// </summary>
public sealed class AttentionService(IFleetDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<AttentionResponse> GetAsync(CancellationToken ct)
    {
        var items = new List<AttentionItem>();
        items.AddRange(await AlertGroupsAsync(ct));
        items.AddRange(await QueuesAsync(ct));

        var ordered = items.Where(i => i.Count > 0)
            .OrderByDescending(i => i.Severity).ThenByDescending(i => i.Count).ThenBy(i => i.Label).ToList();
        var checkedAt = await db.AutomationExecutions.MaxAsync(e => (DateTime?)e.FinishedAt, ct);
        return new AttentionResponse(ordered, ordered.Sum(i => i.Count), checkedAt);
    }

    private async Task<IReadOnlyList<AttentionItem>> AlertGroupsAsync(CancellationToken ct)
    {
        var audiences = AlertAudiences.VisibleTo(currentUser.Permissions);
        if (audiences.Count == 0) return [];
        var groups = await db.FleetAlerts
            .Where(a => audiences.Contains(a.Audience) && FleetAlertWorkflow.OpenStatuses.Contains(a.Status))
            .GroupBy(a => new { a.Trigger, a.Category })
            .Select(g => new { g.Key.Trigger, g.Key.Category, Count = g.Count(), Severity = g.Max(a => a.Severity) })
            .ToListAsync(ct);
        return groups.Select(g => new AttentionItem(
            $"alert:{g.Trigger}:{g.Category}", AlertGroupLabel(g.Trigger, g.Category, g.Count), g.Count, g.Severity, g.Category,
            $"/alertas?trigger={g.Trigger}", "alert")).ToList();
    }

    private static string AlertGroupLabel(AutomationTrigger trigger, FleetAlertCategory category, int n) => trigger switch
    {
        AutomationTrigger.MaintenanceOverdue => n == 1 ? "manutenção preventiva atrasada" : "manutenções preventivas atrasadas",
        AutomationTrigger.MaintenanceDueSoon => n == 1 ? "manutenção preventiva próxima do vencimento" : "manutenções preventivas próximas do vencimento",
        AutomationTrigger.FuelConsumptionAbnormal => n == 1 ? "veículo com consumo fora do padrão" : "veículos com consumo fora do padrão",
        AutomationTrigger.VehicleCostAboveAverage => n == 1 ? "veículo com custo acima da média do tipo" : "veículos com custo acima da média do tipo",
        AutomationTrigger.BudgetThreshold => n == 1 ? "orçamento perto ou acima do limite" : "orçamentos perto ou acima do limite",
        AutomationTrigger.TireTreadLow => n == 1 ? "pneu com sulco baixo" : "pneus com sulco baixo",
        AutomationTrigger.ExpenseOverdue => n == 1 ? "despesa em atraso" : "despesas em atraso",
        AutomationTrigger.DocumentExpiring => n == 1 ? "documento vencido ou vencendo" : "documentos vencidos ou vencendo",
        _ => category switch
        {
            FleetAlertCategory.Maintenance => "alerta(s) de manutenção",
            FleetAlertCategory.Fuel => "alerta(s) de combustível",
            FleetAlertCategory.Tires => "alerta(s) de pneus",
            FleetAlertCategory.Finance => "alerta(s) financeiros",
            FleetAlertCategory.Documents => "alerta(s) de documentos",
            _ => "alerta(s) operacionais",
        },
    };

    private bool Can(string permission) => currentUser.HasPermission(permission);

    private async Task<IReadOnlyList<AttentionItem>> QueuesAsync(CancellationToken ct)
    {
        var items = new List<AttentionItem>();
        if (Can(Permissions.Fuel.ReviewAnomalies))
        {
            var n = await db.Fuelings.CountAsync(f => f.Status == FuelingStatus.PendingReview, ct);
            items.Add(new("queue:fueling-review", n == 1 ? "abastecimento aguardando revisão" : "abastecimentos aguardando revisão", n,
                FleetAlertSeverity.Warning, FleetAlertCategory.Fuel, "/abastecimentos?status=PendingReview", "queue"));
        }
        if (Can(Permissions.Mileage.Manage) && Can(Permissions.Vehicles.View))
        {
            var n = await db.OdometerReadings.CountAsync(r => r.Status == OdometerReadingStatus.PendingReview, ct);
            items.Add(new("queue:mileage-review", n == 1 ? "leitura de hodômetro suspeita aguardando revisão" : "leituras de hodômetro suspeitas aguardando revisão", n,
                FleetAlertSeverity.Warning, FleetAlertCategory.Operations, "/veiculos?pendingMileageReview=true", "queue"));
        }
        if (Can(Permissions.Maintenance.ManageWorkOrders))
        {
            var n = await db.MaintenanceRequests.CountAsync(r => r.Status == MaintenanceRequestStatus.Open, ct);
            items.Add(new("queue:maintenance-requests", n == 1 ? "solicitação de manutenção aguardando aprovação" : "solicitações de manutenção aguardando aprovação", n,
                FleetAlertSeverity.Warning, FleetAlertCategory.Maintenance, "/solicitacoes-manutencao?status=Open", "queue"));
        }
        if (Can(Permissions.Occurrences.View))
        {
            var n = await OccurrenceService.Open(db.Occurrences).CountAsync(o => o.Severity == OccurrenceSeverity.Critical, ct);
            items.Add(new("queue:critical-occurrences", n == 1 ? "ocorrência crítica em aberto" : "ocorrências críticas em aberto", n,
                FleetAlertSeverity.Critical, FleetAlertCategory.Operations, "/ocorrencias?openOnly=true&severity=Critical", "queue"));
        }
        if (Can(Permissions.Drivers.View))
        {
            var today = clock.Today;
            var n = await db.Drivers.CountAsync(d => d.Status != DriverStatus.Inactive && d.LicenseExpiresOn < today, ct);
            items.Add(new("queue:license-expired", n == 1 ? "motorista ativo com CNH vencida" : "motoristas ativos com CNH vencida", n,
                FleetAlertSeverity.Critical, FleetAlertCategory.Operations, "/motoristas?licenseAlert=Expired", "queue"));
        }
        return items;
    }
}
