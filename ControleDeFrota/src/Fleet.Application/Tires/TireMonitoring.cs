using Fleet.Application.Common;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tires;

/// <summary>Everything measured about a tire at one moment (inspection, removal or retread return).</summary>
public sealed record TireMeasurementInput(
    TireInspectionSource Source,
    int? OdometerKm,
    decimal? TreadDepthMm,
    decimal? Pressure,
    TirePressureUnit? PressureUnit,
    TireCondition Condition,
    TireWearPattern WearPattern,
    IReadOnlyCollection<TireDamageType> Damages,
    string? Notes,
    Guid? OccurrenceId = null);

/// <summary>
/// Measurement history and the anomaly foundation (seções 18, 34, 35). A measurement is always a new TireInspection row —
/// the tread history is never overwritten; the tire's fast-read fields follow the newest measurement only.
/// </summary>
public sealed class TireMonitoring(IFleetDbContext db, TireLifecycle lifecycle)
{
    /// <summary>Adds the measurement to the unit of work (no save) and returns it with the pressure check filled.</summary>
    public async Task<TireInspection> RecordAsync(Tire tire, TireInstallation? stint, TireAsset? asset, DateTime at, TireMeasurementInput input,
        TireSettings settings, CancellationToken ct)
    {
        var position = stint is null ? null : asset?.Position(stint.PositionCode);
        var inspection = new TireInspection
        {
            TireId = tire.Id,
            Tire = tire,
            InstallationId = stint?.Id,
            VehicleId = stint?.VehicleId,
            ImplementId = stint?.ImplementId,
            PositionCode = stint?.PositionCode,
            PositionLabel = stint?.PositionLabel,
            InspectedAt = at,
            Source = input.Source,
            OdometerKm = input.OdometerKm,
            TireKm = TireLifecycle.TireKmAt(tire, stint, input.OdometerKm),
            TreadDepthMm = input.TreadDepthMm,
            Pressure = input.Pressure,
            PressureUnit = input.Pressure is null ? null : input.PressureUnit ?? settings.PressureUnit,
            Condition = input.Condition,
            WearPattern = input.WearPattern,
            Damages = input.Damages.Distinct().Select(d => new TireInspectionDamage { Type = d }).ToList(),
            Notes = input.Notes,
            OccurrenceId = input.OccurrenceId,
        };
        if (input.Pressure is { } pressure)
            inspection.PressureCheck = TirePressure.Check(pressure, inspection.PressureUnit!.Value, position?.RecommendedPressurePsi,
                settings.PressureTolerancePercent);
        db.TireInspections.Add(inspection);

        if (input.TreadDepthMm is { } tread)
        {
            await CheckRapidWearAsync(tire, inspection, tread, settings, ct);
            if (tire.TreadMeasuredAt is null || at >= tire.TreadMeasuredAt)
            {
                tire.CurrentTreadDepthMm = tread;
                tire.TreadMeasuredAt = at;
            }
        }
        // A measurement typed after a newer one is history, not the current condition.
        if (input.Source != TireInspectionSource.Retread && (tire.LastInspectedAt is null || at >= tire.LastInspectedAt))
        {
            tire.LastInspectedAt = at;
            tire.LastWearPattern = input.WearPattern;
            tire.LastInspectionHasDamage = inspection.Damages.Count > 0;
            if (inspection.PressureCheck is { } check) tire.LastPressureCheck = check;
            if (tire.InspectionReferenceAt is null || at > tire.InspectionReferenceAt) tire.InspectionReferenceAt = at;
        }
        if (inspection.Damages.Any(d => d.Type == TireDamageType.Puncture)) await CheckRepeatedPuncturesAsync(tire, at, fromRepair: false, ct);
        return inspection;
    }

    private async Task CheckRapidWearAsync(Tire tire, TireInspection current, decimal tread, TireSettings settings, CancellationToken ct)
    {
        var previous = await db.TireInspections
            .Where(i => i.TireId == tire.Id && i.TreadDepthMm != null && i.InspectedAt <= current.InspectedAt)
            .OrderByDescending(i => i.InspectedAt).ThenByDescending(i => i.CreatedAt)
            .Select(i => new TreadMeasurement(i.InspectedAt, i.TreadDepthMm!.Value, i.TireKm))
            .FirstOrDefaultAsync(ct);
        var message = TireAnomalyRules.RapidTreadLoss(previous, new TreadMeasurement(current.InspectedAt, tread, current.TireKm), settings);
        if (message is not null) await AddAnomalyAsync(tire, TireAnomalyType.RapidTreadLoss, message, current.InspectedAt, ct);
    }

    /// <summary>
    /// Punctures over the window. The inspection that finds a puncture and the repair that fixes it are two records of one
    /// fact, so the count is the larger side (with the record being saved now), not the sum.
    /// </summary>
    public async Task CheckRepeatedPuncturesAsync(Tire tire, DateTime at, bool fromRepair, CancellationToken ct)
    {
        var since = at.AddDays(-TireAnomalyRules.RepeatedPuncturesWindowDays);
        var fromInspections = await db.TireInspectionDamages
            .Where(d => d.Type == TireDamageType.Puncture &&
                        db.TireInspections.Any(i => i.Id == d.InspectionId && i.TireId == tire.Id && i.InspectedAt >= since))
            .CountAsync(ct);
        var fromRepairs = await db.TireServiceOrders
            .CountAsync(o => o.TireId == tire.Id && o.RepairType == TireRepairType.Puncture && o.Status == TireServiceStatus.Completed &&
                             o.CompletedAt >= since, ct);
        var count = fromRepair ? Math.Max(fromInspections, fromRepairs + 1) : Math.Max(fromInspections + 1, fromRepairs);
        if (TireAnomalyRules.RepeatedPunctures(count) is { } message)
            await AddAnomalyAsync(tire, TireAnomalyType.RepeatedPunctures, message, at, ct);
    }

    public async Task CheckRepeatedRepairsAsync(Tire tire, DateTime at, CancellationToken ct)
    {
        var since = at.AddDays(-TireAnomalyRules.RepeatedRepairsWindowDays);
        var count = await db.TireServiceOrders.CountAsync(o => o.TireId == tire.Id && o.Kind == TireServiceKind.Repair &&
                                                               o.Status == TireServiceStatus.Completed && o.CompletedAt >= since, ct) + 1;
        if (TireAnomalyRules.RepeatedRepairs(count) is { } message)
            await AddAnomalyAsync(tire, TireAnomalyType.RepeatedRepairs, message, at, ct);
    }

    /// <summary>The same position losing tires to damage again and again points at the position, not at the tire.</summary>
    public async Task CheckRecurringPositionDamageAsync(Tire tire, TireInstallation stint, TireAsset asset, DateTime at, CancellationToken ct)
    {
        var since = at.AddDays(-TireAnomalyRules.RecurringPositionWindowDays);
        var removals = db.TireInstallations.Where(i => i.PositionCode == stint.PositionCode && i.RemovalReason == TireRemovalReason.Damage &&
                                                       i.RemovedAt >= since && i.Id != stint.Id);
        removals = asset.VehicleId is { } vid ? removals.Where(i => i.VehicleId == vid) : removals.Where(i => i.ImplementId == asset.ImplementId);
        var count = await removals.CountAsync(ct) + 1;
        if (TireAnomalyRules.RecurringPositionDamage(stint.PositionLabel, asset.Label, count) is { } message)
            await AddAnomalyAsync(tire, TireAnomalyType.RecurringPositionDamage, message, at, ct);
    }

    public async Task CheckShortLifecycleAsync(Tire tire, TireSettings settings, DateTime at, CancellationToken ct)
    {
        if (TireAnomalyRules.ShortLifecycle(tire.AccumulatedKm, tire.HasUnmeasuredDistance, settings) is { } message)
            await AddAnomalyAsync(tire, TireAnomalyType.ShortLifecycle, message, at, ct);
    }

    /// <summary>One open anomaly per type and tire: a repeated signal does not pile up, it is already waiting for review.</summary>
    private async Task AddAnomalyAsync(Tire tire, TireAnomalyType type, string message, DateTime at, CancellationToken ct)
    {
        var pending = db.TireAnomalies.Local.Any(a => a.TireId == tire.Id && a.Type == type && a.ReviewedAt is null) ||
                      await db.TireAnomalies.AnyAsync(a => a.TireId == tire.Id && a.Type == type && a.ReviewedAt == null, ct);
        if (pending) return;
        db.TireAnomalies.Add(new TireAnomaly
        {
            TireId = tire.Id,
            Type = type,
            Message = message.Length <= TireAnomaly.MessageMaxLength ? message : message[..TireAnomaly.MessageMaxLength],
            DetectedAt = lifecycle.Now,
        });
        lifecycle.Record(OperationalEventType.TireAnomalyDetected, tire, null, at, $"Pneu {tire.Code} requer revisão: {message}",
            new { tireId = tire.Id, type }, tireOnly: true);
    }
}
