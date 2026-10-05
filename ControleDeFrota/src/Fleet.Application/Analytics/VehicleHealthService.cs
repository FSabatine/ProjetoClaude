using Fleet.Application.Common;
using Fleet.Application.Documents;
using Fleet.Application.Maintenance;
using Fleet.Application.Occurrences;
using Fleet.Application.Vehicles;
using Fleet.Domain.Authorization;
using Fleet.Domain.Documents;
using Fleet.Domain.Fuel;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Tires;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Analytics;

public sealed record VehicleHealthResponse(
    Guid VehicleId, int Score, HealthLevel Level, IReadOnlyList<HealthFactor> Factors, bool IsPartial);

/// <summary>
/// Vehicle health (spec §16, <see cref="VehicleHealthPolicy"/>): each area is evaluated with the module's own rules and
/// explained in words; areas the reader cannot see are reported as NotVisible and do not count.
/// </summary>
public sealed class VehicleHealthService(IFleetDbContext db, IClock clock, ICurrentUser currentUser, MaintenanceScheduleService schedules)
{
    private bool Can(params string[] permissions) => permissions.All(currentUser.HasPermission);

    public async Task<VehicleHealthResponse> GetAsync(Guid vehicleId, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");

        var factors = new List<HealthFactor>
        {
            Can(Permissions.Maintenance.View) ? await MaintenanceAsync(vehicleId, ct) : Hidden(HealthFactorArea.Maintenance),
            Can(Permissions.Fuel.View) ? await FuelAsync(vehicleId, ct) : Hidden(HealthFactorArea.Fuel),
            Can(Permissions.Tires.View) ? await TiresAsync(vehicleId, ct) : Hidden(HealthFactorArea.Tires),
            Can([.. AlertAudiences.RequiredPermissions(AlertAudience.FleetCosts)]) ? await CostsAsync(vehicleId, ct) : Hidden(HealthFactorArea.Costs),
            Can(Permissions.Documents.View) ? await DocumentsAsync(vehicleId, ct) : Hidden(HealthFactorArea.Documents),
            Can(Permissions.Occurrences.View) ? await OccurrencesAsync(vehicleId, ct) : Hidden(HealthFactorArea.Occurrences),
            await MileageAsync(vehicleId, ct),
        };
        var score = VehicleHealthPolicy.Score(factors);
        return new VehicleHealthResponse(vehicleId, score, VehicleHealthPolicy.Level(score), factors,
            factors.Any(f => f.Status == HealthFactorStatus.NotVisible));
    }

    private static HealthFactor Hidden(HealthFactorArea area) =>
        new(area, HealthFactorStatus.NotVisible, "Você não tem acesso a esta área, então ela não entra na conta.", null);

    private async Task<HealthFactor> MaintenanceAsync(Guid vehicleId, CancellationToken ct)
    {
        var items = await schedules.ForVehicleAsync(vehicleId, ct);
        var overdue = items.Where(i => i.Status == MaintenanceScheduleStatus.Overdue).ToList();
        var soon = items.Where(i => i.Status is MaintenanceScheduleStatus.Due or MaintenanceScheduleStatus.DueSoon).ToList();
        if (overdue.Count > 0)
            return new(HealthFactorArea.Maintenance, HealthFactorStatus.Critical,
                $"{overdue.Count} item(ns) da preventiva atrasado(s): {string.Join(", ", overdue.Select(i => i.ServiceName).Take(3))}.", "manutencao");
        if (soon.Count > 0)
            return new(HealthFactorArea.Maintenance, HealthFactorStatus.Attention,
                $"{soon.Count} item(ns) da preventiva vencendo: {string.Join(", ", soon.Select(i => i.ServiceName).Take(3))}.", "manutencao");
        return new(HealthFactorArea.Maintenance, HealthFactorStatus.Good,
            items.Count == 0 ? "Sem plano de manutenção preventiva aplicável." : "Preventiva em dia.", "manutencao");
    }

    private async Task<HealthFactor> FuelAsync(Guid vehicleId, CancellationToken ct)
    {
        var pendingReview = await db.Fuelings.CountAsync(f => f.VehicleId == vehicleId && f.Status == FuelingStatus.PendingReview, ct);
        var abnormal = await db.FleetAlerts.Where(a => a.VehicleId == vehicleId && a.Trigger == AutomationTrigger.FuelConsumptionAbnormal &&
                                                       FleetAlertWorkflow.OpenStatuses.Contains(a.Status))
            .Select(a => a.Title).FirstOrDefaultAsync(ct);
        if (abnormal is not null)
            return new(HealthFactorArea.Fuel, HealthFactorStatus.Attention, $"Consumo fora do padrão do próprio veículo ({abnormal}).", "combustivel");
        if (pendingReview > 0)
            return new(HealthFactorArea.Fuel, HealthFactorStatus.Attention, $"{pendingReview} abastecimento(s) aguardando revisão.", "combustivel");
        return new(HealthFactorArea.Fuel, HealthFactorStatus.Good, "Consumo dentro do padrão e sem abastecimentos pendentes de revisão.", "combustivel");
    }

    private async Task<HealthFactor> TiresAsync(Guid vehicleId, CancellationToken ct)
    {
        var settings = await db.TireSettings.AsNoTracking().SingleOrDefaultAsync(ct) ?? TireSettings.Defaults();
        var installed = await db.TireInstallations.Where(i => i.VehicleId == vehicleId && i.RemovedAt == null)
            .Select(i => new { i.Tire.CurrentTreadDepthMm, i.Tire.LastInspectionHasDamage }).ToListAsync(ct);
        var atMinimum = installed.Count(t => t.CurrentTreadDepthMm is { } mm && mm <= settings.MinTreadDepthMm);
        var low = installed.Count(t => t.CurrentTreadDepthMm is { } mm && mm > settings.MinTreadDepthMm && mm <= settings.TreadWarningDepthMm);
        var damaged = installed.Count(t => t.LastInspectionHasDamage);
        if (atMinimum > 0)
            return new(HealthFactorArea.Tires, HealthFactorStatus.Critical, $"{atMinimum} pneu(s) no sulco mínimo configurado pela empresa.", "pneus");
        if (low > 0 || damaged > 0)
            return new(HealthFactorArea.Tires, HealthFactorStatus.Attention,
                string.Join(" ", new[] { low > 0 ? $"{low} pneu(s) com sulco baixo." : null, damaged > 0 ? $"{damaged} pneu(s) com dano na última inspeção." : null }
                    .Where(s => s is not null)), "pneus");
        return new(HealthFactorArea.Tires, HealthFactorStatus.Good,
            installed.Count == 0 ? "Nenhum pneu instalado registrado." : "Pneus instalados sem sinal de atenção.", "pneus");
    }

    private async Task<HealthFactor> CostsAsync(Guid vehicleId, CancellationToken ct)
    {
        var title = await db.FleetAlerts.Where(a => a.VehicleId == vehicleId && a.Trigger == AutomationTrigger.VehicleCostAboveAverage &&
                                                    FleetAlertWorkflow.OpenStatuses.Contains(a.Status))
            .Select(a => a.Title).FirstOrDefaultAsync(ct);
        return title is null
            ? new(HealthFactorArea.Costs, HealthFactorStatus.Good, "Custo do período dentro do padrão dos veículos do mesmo tipo.", "financeiro")
            : new(HealthFactorArea.Costs, HealthFactorStatus.Attention, title + ".", "financeiro");
    }

    private async Task<HealthFactor> DocumentsAsync(Guid vehicleId, CancellationToken ct)
    {
        var today = clock.Today;
        var vehicleDocs = db.Documents.Where(d => d.VehicleId == vehicleId);
        var expired = await vehicleDocs.WhereStatus(DocumentStatus.Expired, today).CountAsync(ct);
        var expiring = await vehicleDocs.WhereStatus(DocumentStatus.ExpiringSoon, today).CountAsync(ct);
        if (expired > 0) return new(HealthFactorArea.Documents, HealthFactorStatus.Critical, $"{expired} documento(s) vencido(s).", "documentos");
        if (expiring > 0) return new(HealthFactorArea.Documents, HealthFactorStatus.Attention, $"{expiring} documento(s) vencendo.", "documentos");
        return new(HealthFactorArea.Documents, HealthFactorStatus.Good, "Documentos em dia.", "documentos");
    }

    private async Task<HealthFactor> OccurrencesAsync(Guid vehicleId, CancellationToken ct)
    {
        var open = OccurrenceService.Open(db.Occurrences).Where(o => o.VehicleId == vehicleId);
        var critical = await open.CountAsync(o => o.Severity == OccurrenceSeverity.Critical, ct);
        var total = await open.CountAsync(ct);
        if (critical > 0) return new(HealthFactorArea.Occurrences, HealthFactorStatus.Critical, $"{critical} ocorrência(s) crítica(s) em aberto.", "ocorrencias");
        if (total > 0) return new(HealthFactorArea.Occurrences, HealthFactorStatus.Attention, $"{total} ocorrência(s) em aberto.", "ocorrencias");
        return new(HealthFactorArea.Occurrences, HealthFactorStatus.Good, "Nenhuma ocorrência em aberto.", "ocorrencias");
    }

    private async Task<HealthFactor> MileageAsync(Guid vehicleId, CancellationToken ct)
    {
        var pending = await db.OdometerReadings.AnyAsync(r => r.VehicleId == vehicleId && r.Status == OdometerReadingStatus.PendingReview, ct);
        var stale = await VehicleService.WhereStaleMileage(db.Vehicles.Where(v => v.Id == vehicleId), clock.UtcNow).AnyAsync(ct);
        if (pending) return new(HealthFactorArea.Mileage, HealthFactorStatus.Attention, "Leitura de hodômetro suspeita aguardando revisão.", "quilometragem");
        if (stale)
            return new(HealthFactorArea.Mileage, HealthFactorStatus.Attention,
                $"Hodômetro sem atualização há mais de {OdometerPolicy.StaleAfterDays} dias — os cálculos por km ficam menos confiáveis.", "quilometragem");
        return new(HealthFactorArea.Mileage, HealthFactorStatus.Good, "Hodômetro atualizado.", "quilometragem");
    }
}
