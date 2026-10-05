using Fleet.Application.Common;
using Fleet.Application.Documents;
using Fleet.Application.Finance;
using Fleet.Application.Maintenance;
using Fleet.Domain.Common;
using Fleet.Domain.Documents;
using Fleet.Domain.Finance;
using Fleet.Domain.Fuel;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Tires;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Intelligence;

/// <summary>
/// One finding of a scheduled trigger. The dedup key identifies "the same problem" across scans: while it keeps being
/// found the open alert is refreshed, when it stops being found the alert closes by itself.
/// </summary>
public sealed record AlertCandidate(
    string DedupKey,
    AlertAudience Audience,
    string EntityType,
    Guid EntityId,
    Guid? VehicleId,
    string Title,
    string Explanation,
    string Evidence,
    string RecommendedAction,
    int Impact,
    int Urgency,
    string? Tab = null,
    FleetAlertSeverity? Severity = null);

/// <summary>
/// The condition behind a scheduled trigger (ADR-045). Deterministic and rule-based: every number in the texts is
/// computed here from the modules' own data and policies — nothing is estimated by an AI.
/// </summary>
public interface IAlertDetector
{
    AutomationTrigger Trigger { get; }
    Task<IReadOnlyList<AlertCandidate>> DetectAsync(AutomationRule rule, CancellationToken ct);
}

internal static class DetectorText
{
    public static string Plate(string plate) => LicensePlate.Format(plate);

    public static string Km(decimal km) => $"{BrazilianFormat.Number(km)} km";

    public static string Days(int days) => days == 1 ? "1 dia" : $"{days} dias";
}

/// <summary>
/// Per-scope memo of each vehicle's maintenance schedule: the overdue and due-soon rules run in the same scan and would
/// otherwise evaluate every vehicle twice (same result, half the queries).
/// </summary>
public sealed class MaintenanceScheduleSnapshot(MaintenanceScheduleService schedules)
{
    private readonly Dictionary<Guid, IReadOnlyList<MaintenanceScheduleItemResponse>> _byVehicle = [];

    public async Task<IReadOnlyList<MaintenanceScheduleItemResponse>> ForVehicleAsync(Guid vehicleId, CancellationToken ct)
    {
        if (!_byVehicle.TryGetValue(vehicleId, out var items)) _byVehicle[vehicleId] = items = await schedules.ForVehicleAsync(vehicleId, ct);
        return items;
    }
}

/// <summary>Preventive maintenance overdue (MaintenanceOverdue) or approaching (MaintenanceDueSoon), per vehicle and plan item.</summary>
public sealed class MaintenanceDueDetector(IFleetDbContext db, IClock clock, MaintenanceScheduleSnapshot schedules, AutomationTrigger trigger) : IAlertDetector
{
    public AutomationTrigger Trigger => trigger;

    public async Task<IReadOnlyList<AlertCandidate>> DetectAsync(AutomationRule rule, CancellationToken ct)
    {
        var today = clock.Today;
        var vehicles = await db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive)
            .Select(v => new { v.Id, v.LicensePlate, v.CurrentOdometerKm, v.HourMeter })
            .ToListAsync(ct);
        var wanted = trigger == AutomationTrigger.MaintenanceOverdue
            ? new[] { MaintenanceScheduleStatus.Overdue }
            : [MaintenanceScheduleStatus.DueSoon, MaintenanceScheduleStatus.Due];

        var result = new List<AlertCandidate>();
        foreach (var vehicle in vehicles)
        {
            // Per vehicle on purpose: ForVehicleAsync is the single source of the plan precedence and the baseline of
            // never-serviced items (the dashboard's cheaper aggregate skips those).
            foreach (var item in (await schedules.ForVehicleAsync(vehicle.Id, ct)).Where(i => wanted.Contains(i.Status)))
            {
                var plate = DetectorText.Plate(vehicle.LicensePlate);
                var remaining = Remaining(item, today, vehicle.CurrentOdometerKm, vehicle.HourMeter);
                var overdue = item.Status == MaintenanceScheduleStatus.Overdue;
                result.Add(new AlertCandidate(
                    $"maint:{vehicle.Id}:{item.MaintenancePlanItemId}:{item.NextDueKm}:{item.NextDueOn}:{item.NextDueHours}",
                    AlertAudience.Maintenance, "Vehicle", vehicle.Id, vehicle.Id,
                    overdue ? $"{plate}: {item.ServiceName} atrasada" : $"{plate}: {item.ServiceName} se aproximando",
                    overdue
                        ? $"A manutenção preventiva \"{item.ServiceName}\" passou do vencimento previsto no plano, já contando a carência configurada."
                        : $"A manutenção preventiva \"{item.ServiceName}\" {remaining.Sentence}.",
                    remaining.Evidence,
                    overdue
                        ? "Abra uma ordem de serviço para este item ou registre a manutenção já feita, para que o plano volte a contar a partir dela."
                        : "Programe a manutenção com a oficina antes do vencimento para evitar indisponibilidade não planejada.",
                    Impact: ItemImpact(item.Priority),
                    Urgency: remaining.Urgency,
                    Tab: "manutencao",
                    Severity: overdue && item.Priority == MaintenancePriority.Critical ? FleetAlertSeverity.Critical : null));
            }
        }
        return result;
    }

    private static int ItemImpact(MaintenancePriority priority) => priority switch
    {
        MaintenancePriority.Critical => 30,
        MaintenancePriority.High => 20,
        MaintenancePriority.Medium => 10,
        _ => 5,
    };

    private sealed record RemainingInfo(string Sentence, string Evidence, int Urgency);

    /// <summary>Distance/time to the due point on each axis of the plan item; the most urgent one leads the sentence.</summary>
    private static RemainingInfo Remaining(MaintenanceScheduleItemResponse item, DateOnly today, int odometerKm, decimal? hourMeter)
    {
        var parts = new List<string>();
        var sentences = new List<(int Urgency, string Text)>();
        if (item.NextDueKm is { } dueKm)
        {
            var left = dueKm - odometerKm;
            parts.Add($"previsto para {DetectorText.Km(dueKm)}; hodômetro atual {DetectorText.Km(odometerKm)}");
            sentences.Add(left >= 0
                ? (Math.Clamp(30 - left / 100, 0, 30), $"vence em aproximadamente {DetectorText.Km(left)}")
                : (30, $"passou {DetectorText.Km(-left)} do previsto"));
        }
        if (item.NextDueOn is { } dueOn)
        {
            var days = dueOn.DayNumber - today.DayNumber;
            parts.Add($"data prevista {BrazilianFormat.Date(dueOn)}");
            sentences.Add(days >= 0
                ? (Math.Clamp(30 - days, 0, 30), days == 0 ? "vence hoje" : $"vence em {DetectorText.Days(days)}")
                : (30, $"venceu há {DetectorText.Days(-days)}"));
        }
        if (item.NextDueHours is { } dueHours && hourMeter is { } hours)
        {
            var left = dueHours - hours;
            parts.Add($"previsto para {BrazilianFormat.Compact(dueHours)} h; horímetro atual {BrazilianFormat.Compact(hours)} h");
            sentences.Add(left >= 0
                ? (Math.Clamp(30 - (int)(left / 10), 0, 30), $"vence em aproximadamente {BrazilianFormat.Compact(left)} horas de uso")
                : (30, $"passou {BrazilianFormat.Compact(-left)} horas do previsto"));
        }
        var lead = sentences.OrderByDescending(s => s.Urgency).FirstOrDefault();
        return new RemainingInfo(lead.Text ?? "está na janela de aviso do plano",
            parts.Count == 0 ? "Sem dados de vencimento no plano." : string.Join("; ", parts) + ".", lead.Urgency);
    }
}

/// <summary>
/// Consumption of the period vs the vehicle's own previous average (weighted: total km / total fuel of the measured
/// segments, the same rule as ConsumptionSample). Only a worsening above the threshold is flagged.
/// </summary>
public sealed class FuelConsumptionDetector(IFleetDbContext db, IClock clock) : IAlertDetector
{
    public const int HistoryDays = 180;
    public const int MinPeriodSegments = 2;

    public AutomationTrigger Trigger => AutomationTrigger.FuelConsumptionAbnormal;

    public async Task<IReadOnlyList<AlertCandidate>> DetectAsync(AutomationRule rule, CancellationToken ct)
    {
        var threshold = rule.Threshold ?? AutomationTriggerCatalog.Get(Trigger).DefaultThreshold!.Value;
        var days = rule.LookbackDays ?? AutomationTriggerCatalog.Get(Trigger).DefaultLookbackDays!.Value;
        var today = clock.Today;
        var periodStart = today.AddDays(-days + 1);
        var historyStart = periodStart.AddDays(-HistoryDays);

        var period = await SamplesAsync(periodStart, today, ct);
        var history = await SamplesAsync(historyStart, periodStart.AddDays(-1), ct);
        var plates = await db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive)
            .ToDictionaryAsync(v => v.Id, v => v.LicensePlate, ct);

        var result = new List<AlertCandidate>();
        foreach (var (key, current) in period)
        {
            if (!plates.TryGetValue(key.VehicleId, out var plate)) continue;
            if (!history.TryGetValue(key, out var previous)) continue;
            if (current.Segments < MinPeriodSegments || previous.Segments < ConsumptionBaseline.MinVehicleSegments) continue;
            if (current.Average is not { } now || previous.Average is not { } before || before <= 0) continue;

            var change = (now - before) / before * 100m;
            if (-change < threshold) continue;

            var unit = UnitLabel(key.Unit);
            var p = DetectorText.Plate(plate);
            result.Add(new AlertCandidate(
                $"fuel:{key.VehicleId}:{key.Unit}", AlertAudience.Fuel, "Vehicle", key.VehicleId, key.VehicleId,
                $"{p}: consumo {BrazilianFormat.Compact(-change)}% pior que a média do veículo",
                $"Nos últimos {DetectorText.Days(days)} o veículo rodou {BrazilianFormat.Compact(now)} {unit}, abaixo da média anterior do próprio veículo " +
                $"({BrazilianFormat.Compact(before)} {unit}). Requer revisão: pode haver causa mecânica, de rota/carga ou de registro.",
                $"Período: {current.Segments} trecho(s) medido(s), {DetectorText.Km(current.DistanceKm)} e {BrazilianFormat.Number(current.Quantity, 1)} de combustível. " +
                $"Referência: {previous.Segments} trecho(s) nos {HistoryDays} dias anteriores ({DetectorText.Km(previous.DistanceKm)}). " +
                "Consumo medido de tanque cheio a tanque cheio.",
                "Confira os abastecimentos do período, a calibragem dos pneus e se houve mudança de rota ou carga; se persistir, solicite uma avaliação mecânica.",
                Impact: Math.Clamp((int)(-change), 0, 30), Urgency: 10, Tab: "combustivel"));
        }
        return result;
    }

    private sealed record SampleKey(Guid VehicleId, FuelUnit Unit);

    private async Task<Dictionary<SampleKey, ConsumptionSample>> SamplesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        // ADR-033: sum the columns (km and quantity), never an expression — the ratio is taken in memory.
        var rows = await db.Fuelings
            .Where(f => f.Status != FuelingStatus.Cancelled && f.ConsumptionResult == ConsumptionResult.Calculated &&
                        f.FueledOn >= from && f.FueledOn <= to)
            .GroupBy(f => new { f.VehicleId, f.FuelType.Unit })
            .Select(g => new { g.Key.VehicleId, g.Key.Unit, Km = g.Sum(f => f.SegmentDistanceKm), Quantity = g.Sum(f => f.SegmentQuantity), Count = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => new SampleKey(r.VehicleId, r.Unit), r => new ConsumptionSample(r.Km ?? 0, r.Quantity ?? 0m, r.Count));
    }

    private static string UnitLabel(FuelUnit unit) => unit switch
    {
        FuelUnit.CubicMeter => "km/m³",
        FuelUnit.KilowattHour => "km/kWh",
        _ => "km/l",
    };
}

/// <summary>Vehicle total cost of the period vs the average of the other active vehicles of the same type (internal benchmark).</summary>
public sealed class VehicleCostDetector(IFleetDbContext db, IClock clock, CostAggregationService costs) : IAlertDetector
{
    public const int MinPeers = 3;

    public AutomationTrigger Trigger => AutomationTrigger.VehicleCostAboveAverage;

    public async Task<IReadOnlyList<AlertCandidate>> DetectAsync(AutomationRule rule, CancellationToken ct)
    {
        var threshold = rule.Threshold ?? AutomationTriggerCatalog.Get(Trigger).DefaultThreshold!.Value;
        var days = rule.LookbackDays ?? AutomationTriggerCatalog.Get(Trigger).DefaultLookbackDays!.Value;
        var to = clock.Today;
        var from = to.AddDays(-days + 1);

        var byVehicle = await costs.GetVehicleCostsAsync(from, to, ct);
        var vehicles = await db.Vehicles.Where(v => v.Status != VehicleStatus.Inactive)
            .Select(v => new { v.Id, v.LicensePlate, v.Type })
            .ToListAsync(ct);

        var result = new List<AlertCandidate>();
        foreach (var group in vehicles.GroupBy(v => v.Type))
        {
            var withCost = group.Where(v => byVehicle.TryGetValue(v.Id, out var c) && c.TotalCost > 0)
                .Select(v => (Vehicle: v, Cost: byVehicle[v.Id])).ToList();
            foreach (var (vehicle, cost) in withCost)
            {
                var peers = withCost.Where(p => p.Vehicle.Id != vehicle.Id).Select(p => p.Cost).ToList();
                if (peers.Count < MinPeers) continue;
                var average = peers.Average(p => p.TotalCost);
                if (average <= 0) continue;
                var above = (cost.TotalCost - average) / average * 100m;
                if (above < threshold) continue;

                var (driverName, driverValue, driverPeerAverage) = MainDriver(cost, peers);
                var p = DetectorText.Plate(vehicle.LicensePlate);
                result.Add(new AlertCandidate(
                    $"cost:{vehicle.Id}", AlertAudience.FleetCosts, "Vehicle", vehicle.Id, vehicle.Id,
                    $"{p}: custo {BrazilianFormat.Compact(above)}% acima da média do tipo",
                    $"Nos últimos {DetectorText.Days(days)} o veículo custou {BrazilianFormat.Currency(cost.TotalCost)}, " +
                    $"{BrazilianFormat.Compact(above)}% acima da média dos outros {peers.Count} veículos ativos do mesmo tipo " +
                    $"({BrazilianFormat.Currency(average)}). O principal fator foi {driverName}.",
                    $"{Capitalize(driverName)}: {BrazilianFormat.Currency(driverValue)} (média do tipo {BrazilianFormat.Currency(driverPeerAverage)}). " +
                    $"Combustível {BrazilianFormat.Currency(cost.FuelCost)}, manutenção {BrazilianFormat.Currency(cost.MaintenanceCost)}, " +
                    $"pneus {BrazilianFormat.Currency(cost.TireCost)}, outras despesas {BrazilianFormat.Currency(cost.OtherCost)}.",
                    $"Veja a aba Financeiro do veículo e os lançamentos de {driverName} do período para entender o que puxou o custo.",
                    Impact: Math.Clamp((int)(above / 5), 0, 30), Urgency: 5, Tab: "financeiro"));
            }
        }
        return result;
    }

    private static (string Name, decimal Value, decimal PeerAverage) MainDriver(VehicleCostBreakdown cost, IReadOnlyList<VehicleCostBreakdown> peers)
    {
        var slices = new (string Name, decimal Value, decimal PeerAverage)[]
        {
            ("combustível", cost.FuelCost, peers.Average(p => p.FuelCost)),
            ("manutenção", cost.MaintenanceCost, peers.Average(p => p.MaintenanceCost)),
            ("pneus", cost.TireCost, peers.Average(p => p.TireCost)),
            ("outras despesas", cost.OtherCost, peers.Average(p => p.OtherCost)),
        };
        // The slice furthest above its own peer average explains the gap better than the largest slice.
        return slices.OrderByDescending(s => s.Value - s.PeerAverage).First();
    }

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}

/// <summary>Budget lines of the current month and year whose actual reached the configured utilization.</summary>
public sealed class BudgetThresholdDetector(IClock clock, BudgetService budgets) : IAlertDetector
{
    public AutomationTrigger Trigger => AutomationTrigger.BudgetThreshold;

    public async Task<IReadOnlyList<AlertCandidate>> DetectAsync(AutomationRule rule, CancellationToken ct)
    {
        var threshold = rule.Threshold ?? AutomationTriggerCatalog.Get(Trigger).DefaultThreshold!.Value;
        var today = clock.Today;
        var lines = (await budgets.ListVsActualAsync(today.Year, today.Month, ct))
            .Concat(await budgets.ListVsActualAsync(today.Year, null, ct));

        var result = new List<AlertCandidate>();
        foreach (var line in lines)
        {
            if (line.UtilizationPercent is not { } used || line.Actual is not { } actual || used < threshold) continue;
            var b = line.Budget;
            var scope = b.LicensePlate is not null ? $" do veículo {DetectorText.Plate(b.LicensePlate)}"
                : b.CostCenterName is not null ? $" do centro de custo {b.CostCenterName}" : "";
            var period = b.Month is { } m ? $"{m:D2}/{b.Year}" : $"{b.Year} (anual)";
            var exceeded = used > 100m;
            result.Add(new AlertCandidate(
                $"budget:{b.Id}", AlertAudience.FleetCosts, "Budget", b.Id, b.VehicleId,
                exceeded
                    ? $"Orçamento de {b.CategoryName}{scope} ultrapassado ({BrazilianFormat.Compact(used)}%)"
                    : $"Orçamento de {b.CategoryName}{scope} em {BrazilianFormat.Compact(used)}%",
                exceeded
                    ? $"O realizado de {b.CategoryName}{scope} em {period} passou do orçado em {BrazilianFormat.Compact(used - 100m)}%."
                    : $"O realizado de {b.CategoryName}{scope} em {period} já usou {BrazilianFormat.Compact(used)}% do orçado.",
                $"Realizado {BrazilianFormat.Currency(actual)} de {BrazilianFormat.Currency(b.Amount)} orçados." +
                (line.IsPartial ? " Parte das fontes de custo não pôde ser somada." : ""),
                exceeded
                    ? "Revise os lançamentos da categoria no período e, se o gasto for justificado, ajuste o orçamento."
                    : "Acompanhe os próximos lançamentos da categoria para não ultrapassar o orçamento.",
                Impact: Math.Clamp((int)(used - threshold), 0, 30), Urgency: b.Month is null ? 5 : 15,
                Severity: exceeded ? FleetAlertSeverity.Critical : null));
        }
        return result;
    }
}

/// <summary>Installed tires at the company's tread warning depth (critical at the minimum).</summary>
public sealed class TireTreadDetector(IFleetDbContext db, IClock clock) : IAlertDetector
{
    public AutomationTrigger Trigger => AutomationTrigger.TireTreadLow;

    public async Task<IReadOnlyList<AlertCandidate>> DetectAsync(AutomationRule rule, CancellationToken ct)
    {
        var settings = await db.TireSettings.AsNoTracking().SingleOrDefaultAsync(ct) ?? TireSettings.Defaults();
        var tires = await db.Tires
            .Where(t => t.Status == TireStatus.Installed && t.CurrentTreadDepthMm != null && t.CurrentTreadDepthMm <= settings.TreadWarningDepthMm)
            .Select(t => new { t.Id, t.Code, t.CurrentTreadDepthMm, t.TreadMeasuredAt })
            .ToListAsync(ct);
        var ids = tires.Select(t => t.Id).ToList();
        var places = await db.TireInstallations.Where(i => ids.Contains(i.TireId) && i.RemovedAt == null)
            .Select(i => new { i.TireId, i.PositionLabel, i.VehicleId, Plate = i.Vehicle != null ? i.Vehicle.LicensePlate : i.Implement!.LicensePlate })
            .ToDictionaryAsync(i => i.TireId, ct);

        return tires.Select(t =>
        {
            var mm = t.CurrentTreadDepthMm!.Value;
            var atMinimum = mm <= settings.MinTreadDepthMm;
            var place = places.GetValueOrDefault(t.Id);
            var where = place is null ? "" : $" ({DetectorText.Plate(place.Plate)}, {place.PositionLabel})";
            return new AlertCandidate(
                $"tire:{t.Id}:{(atMinimum ? "min" : "warn")}", AlertAudience.Tires, "Tire", t.Id, place?.VehicleId,
                atMinimum ? $"Pneu {t.Code}{where} no sulco mínimo" : $"Pneu {t.Code}{where} com sulco baixo",
                atMinimum
                    ? "O sulco medido chegou ao mínimo configurado pela empresa para substituição."
                    : "O sulco medido está no limite de aviso configurado pela empresa e se aproxima do mínimo.",
                $"Sulco na última medição: {BrazilianFormat.Compact(mm)} mm" +
                (t.TreadMeasuredAt is { } at ? $" em {BrazilianFormat.Date(clock.ToBusinessDate(at))}" : "") +
                $". Aviso da empresa: {BrazilianFormat.Compact(settings.TreadWarningDepthMm)} mm; mínimo: {BrazilianFormat.Compact(settings.MinTreadDepthMm)} mm.",
                atMinimum
                    ? "Programe a substituição ou a recapagem do pneu."
                    : "Programe a inspeção e a substituição ou recapagem antes de atingir o mínimo.",
                Impact: atMinimum ? 25 : 10, Urgency: atMinimum ? 25 : 10,
                Severity: atMinimum ? FleetAlertSeverity.Critical : null);
        }).ToList();
    }
}

/// <summary>Unpaid expenses past their due date by at least N days. Count/date only — no R$ (finance.view audience).</summary>
public sealed class ExpenseOverdueDetector(IFleetDbContext db, IClock clock) : IAlertDetector
{
    public const int MaxAlerts = 200;

    public AutomationTrigger Trigger => AutomationTrigger.ExpenseOverdue;

    public async Task<IReadOnlyList<AlertCandidate>> DetectAsync(AutomationRule rule, CancellationToken ct)
    {
        var minDays = (int)(rule.Threshold ?? AutomationTriggerCatalog.Get(Trigger).DefaultThreshold!.Value);
        var today = clock.Today;
        var limit = today.AddDays(-minDays);
        var expenses = await db.Expenses
            .Where(e => e.CancelledAt == null && e.DueDate != null && e.DueDate < today && e.DueDate <= limit && e.PaidAmount < e.Amount)
            .OrderBy(e => e.DueDate).Take(MaxAlerts)
            .Select(e => new { e.Id, e.Description, e.DueDate, e.VehicleId, Plate = e.Vehicle != null ? e.Vehicle.LicensePlate : null, Category = e.ExpenseCategory.Name })
            .ToListAsync(ct);

        return expenses.Select(e =>
        {
            var late = today.DayNumber - e.DueDate!.Value.DayNumber;
            var subject = (e.Plate is null ? "" : $"{DetectorText.Plate(e.Plate)}: ") + e.Description;
            return new AlertCandidate(
                $"expense:{e.Id}", AlertAudience.FinanceView, "Expense", e.Id, e.VehicleId,
                $"Despesa em atraso: {Short(subject)}",
                $"A despesa \"{e.Description}\" ({e.Category}) venceu em {BrazilianFormat.Date(e.DueDate.Value)} e ainda não foi totalmente paga.",
                $"Vencimento {BrazilianFormat.Date(e.DueDate.Value)}, {DetectorText.Days(late)} de atraso.",
                "Registre o pagamento se ele já foi feito, ou combine o pagamento com o fornecedor.",
                Impact: 10, Urgency: Math.Clamp(late, 0, 30));
        }).ToList();
    }

    private static string Short(string text) => text.Length <= 90 ? text : text[..89] + "…";
}

/// <summary>Expired or expiring documents (the expiry window comes from each document type).</summary>
public sealed class DocumentExpiringDetector(IFleetDbContext db, IClock clock) : IAlertDetector
{
    public const int MaxAlerts = 300;

    public AutomationTrigger Trigger => AutomationTrigger.DocumentExpiring;

    public async Task<IReadOnlyList<AlertCandidate>> DetectAsync(AutomationRule rule, CancellationToken ct)
    {
        var today = clock.Today;
        var documents = await db.Documents.WhereAlert(today)
            .OrderBy(d => d.ExpiresOn).Take(MaxAlerts)
            .Select(d => new
            {
                d.Id, d.OwnerType, d.VehicleId, d.DriverId, d.ImplementId, d.ExpiresOn, TypeName = d.DocumentType.Name,
                Owner = d.Vehicle != null ? d.Vehicle.LicensePlate
                    : d.Implement != null ? d.Implement.LicensePlate
                    : d.Driver != null ? d.Driver.FullName : null,
            })
            .ToListAsync(ct);

        return documents.Select(d =>
        {
            var expiresOn = d.ExpiresOn!.Value;
            var expired = expiresOn < today;
            var days = Math.Abs(expiresOn.DayNumber - today.DayNumber);
            var (entityType, entityId, owner, audience) = d.OwnerType switch
            {
                DocumentOwnerType.Vehicle => ("Vehicle", d.VehicleId!.Value, $"do veículo {DetectorText.Plate(d.Owner ?? "")}", AlertAudience.Documents),
                DocumentOwnerType.Implement => ("Implement", d.ImplementId!.Value, $"do implemento {DetectorText.Plate(d.Owner ?? "")}", AlertAudience.Documents),
                // Driver names are personal data: this audience also requires drivers.view.
                DocumentOwnerType.Driver => ("Driver", d.DriverId!.Value, $"de {d.Owner}", AlertAudience.DriverDocuments),
                _ => ("Company", Guid.Empty, "da empresa", AlertAudience.Documents),
            };
            return new AlertCandidate(
                $"doc:{d.Id}:{(expired ? "expired" : "expiring")}", audience, entityType, entityId, d.VehicleId,
                expired ? $"{d.TypeName} {owner} vencido" : $"{d.TypeName} {owner} vencendo",
                expired
                    ? $"O documento {d.TypeName} {owner} venceu em {BrazilianFormat.Date(expiresOn)}."
                    : $"O documento {d.TypeName} {owner} vence em {BrazilianFormat.Date(expiresOn)}, dentro do aviso configurado no tipo de documento.",
                expired ? $"Vencido há {DetectorText.Days(days)}." : (days == 0 ? "Vence hoje." : $"Faltam {DetectorText.Days(days)}."),
                "Providencie a renovação e registre o novo documento (botão Renovar), anexando o arquivo.",
                Impact: d.OwnerType == DocumentOwnerType.Company ? 15 : 10,
                Urgency: expired ? 30 : Math.Clamp(30 - days, 0, 30),
                Tab: "documentos",
                Severity: expired ? FleetAlertSeverity.Critical : null);
        }).ToList();
    }
}
