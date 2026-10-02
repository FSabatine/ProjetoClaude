using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Common;
using Fleet.Domain.Fuel;
using Fleet.Domain.Mileage;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Fuel;

/// <summary>
/// Gathers the facts the Domain rules need (ADR-032): record-level checks of one fueling and the tank-to-tank
/// consumption of the segments a change affects. The rules themselves live in <see cref="FuelAnomalyRules"/>,
/// <see cref="ConsumptionCalculator"/> and <see cref="ConsumptionBaseline"/>.
/// Results are stored on the fuelings (snapshots), so reports never recompute history from current settings.
/// </summary>
public sealed class FuelConsumptionService(IFleetDbContext db, IClock clock, OperationalEventLog events)
{
    /// <summary>Days of company fuelings used as the reference price when the station has no reference price in force.</summary>
    public const int ReferencePriceDays = 30;
    public const int MinReferencePriceSamples = 3;
    /// <summary>Upper bound of rows loaded to recalculate a segment chain (partial fills between two full tanks are few).</summary>
    private const int ChainWindow = 200;

    /// <summary>
    /// Re-runs the record-level rules and merges the result into <paramref name="fueling"/>.Anomalies: types no longer
    /// detected disappear, types still detected keep their review, new types are added. Returns the new types.
    /// </summary>
    public async Task<IReadOnlyList<FuelingAnomaly>> EvaluateRecordAsync(
        Fueling fueling, Vehicle vehicle, FuelType type, string? mileageAnomaly, FuelSettings settings, CancellationToken ct)
    {
        var tank = vehicle.FuelTankCapacity is null && vehicle.SecondaryFuelTankCapacity is null
            ? (decimal?)null
            : (vehicle.FuelTankCapacity ?? 0) + (vehicle.SecondaryFuelTankCapacity ?? 0);
        var check = new FuelingCheck(
            fueling.Quantity, type.Unit, fueling.UnitPrice, tank,
            await ReferencePriceAsync(fueling, ct),
            await HoursFromNearestFuelingAsync(fueling, ct),
            vehicle.FuelType, type.Category, type.Name, mileageAnomaly);
        return Merge(fueling, FuelAnomalyRules.RecordTypes, FuelAnomalyRules.EvaluateRecord(check, settings));
    }

    /// <summary>
    /// Recalculates the segments touched by a change at <paramref name="from"/>: the first two full tanks at or after it
    /// (the segment that contains the change, and the next one when the changed fueling is itself a full tank).
    /// Call after SaveChanges, so the chain read from the database includes the change.
    /// </summary>
    public async Task RecalculateAsync(Vehicle vehicle, DateTime from, FuelSettings settings, CancellationToken ct)
    {
        var chainQuery = db.Fuelings.Where(f => f.VehicleId == vehicle.Id && f.Status != FuelingStatus.Cancelled);
        var anchorAt = await chainQuery.Where(f => f.IsFullTank && f.FueledAt < from)
            .OrderByDescending(f => f.FueledAt).ThenByDescending(f => f.OdometerKm)
            .Select(f => (DateTime?)f.FueledAt).FirstOrDefaultAsync(ct);

        var chain = await chainQuery.Where(f => anchorAt == null || f.FueledAt >= anchorAt)
            .Include(f => f.Anomalies).Include(f => f.FuelType)
            .OrderBy(f => f.FueledAt).ThenBy(f => f.OdometerKm).ThenBy(f => f.CreatedAt)
            .Take(ChainWindow)
            .ToListAsync(ct);
        if (chain.Count == 0) return;

        // Corrections made from a fueling fix that fueling's own odometer; only other corrections break a segment.
        var windowStart = chain[0].FueledAt;
        var windowEnd = chain[^1].FueledAt;
        var corrections = await db.OdometerReadings
            .Where(r => r.VehicleId == vehicle.Id && r.Source == OdometerReadingSource.Correction && r.FuelingId == null &&
                        r.Status == OdometerReadingStatus.Valid && r.ReadAt > windowStart && r.ReadAt <= windowEnd)
            .Select(r => r.ReadAt).ToListAsync(ct);

        var outcomes = ConsumptionCalculator.Calculate(
            chain.Select(f => new ConsumptionChainEntry(f.Id, f.FueledAt, f.OdometerKm, f.Quantity, f.TotalAmount, f.IsFullTank,
                f.FuelType.Unit, f.Anomalies.Any(a => a.Type == FuelAnomalyType.MileageJump && a.ReviewedAt == null))).ToList(),
            corrections);

        var affectedFulls = 0;
        for (var i = 0; i < chain.Count && affectedFulls < 2; i++)
        {
            var fueling = chain[i];
            if (fueling.FueledAt < from || (anchorAt is not null && i == 0)) continue;
            if (!fueling.IsFullTank)
            {
                Apply(fueling, outcomes[i], baseline: null);
                Merge(fueling, FuelAnomalyRules.ConsumptionTypes, []);
                fueling.RefreshStatus();
                continue;
            }
            affectedFulls++;
            await ApplySegmentAsync(fueling, outcomes[i], vehicle, settings, ct);
        }
    }

    private async Task ApplySegmentAsync(Fueling fueling, ConsumptionOutcome outcome, Vehicle vehicle, FuelSettings settings, CancellationToken ct)
    {
        if (outcome.Result != ConsumptionResult.Calculated)
        {
            Apply(fueling, outcome, baseline: null);
            Merge(fueling, FuelAnomalyRules.ConsumptionTypes, []);
            fueling.RefreshStatus();
            return;
        }

        var baseline = ConsumptionBaseline.Resolve(
            vehicle.ExpectedConsumption,
            await VehicleHistoryAsync(fueling, ct),
            await TypeAverageAsync(fueling, vehicle, ct));
        Apply(fueling, outcome, baseline);

        var detected = baseline is null
            ? null
            : FuelAnomalyRules.EvaluateConsumption(outcome.Consumption!.Value, baseline, fueling.FuelType.Unit, settings.ConsumptionDeviationPercent);
        var added = Merge(fueling, FuelAnomalyRules.ConsumptionTypes, detected is null ? [] : [detected]);
        fueling.RefreshStatus();
        foreach (var anomaly in added)
        {
            events.Record(OperationalEventType.FuelConsumptionAnomalyDetected, Subject(fueling, vehicle),
                $"{Plate(vehicle)}: {anomaly.Message}",
                new { fuelingId = fueling.Id, anomaly.Type, expected = anomaly.ExpectedValue, actual = anomaly.ActualValue });
        }
    }

    private static void Apply(Fueling fueling, ConsumptionOutcome outcome, ResolvedBaseline? baseline)
    {
        fueling.ConsumptionResult = outcome.Result;
        fueling.SegmentDistanceKm = outcome.DistanceKm;
        fueling.SegmentQuantity = outcome.Quantity;
        fueling.SegmentCost = outcome.Cost;
        fueling.Consumption = outcome.Consumption;
        fueling.ExpectedConsumption = baseline?.Value;
        fueling.BaselineSource = baseline?.Source;
        fueling.SegmentExpectedQuantity = baseline is not null && outcome.DistanceKm is { } distance
            ? Math.Round(distance / baseline.Value, 3, MidpointRounding.AwayFromZero)
            : null;
        fueling.ConsumptionDeviationPercent = baseline is not null && outcome.Consumption is { } actual
            ? FuelAnomalyRules.DeviationPercent(actual, baseline.Value)
            : null;
    }

    /// <summary>Own history: the last measured segments before this one (weighted average, ADR-032).</summary>
    private async Task<ConsumptionSample?> VehicleHistoryAsync(Fueling fueling, CancellationToken ct)
    {
        var rows = await db.Fuelings
            .Where(f => f.VehicleId == fueling.VehicleId && f.Id != fueling.Id && f.Status != FuelingStatus.Cancelled &&
                        f.ConsumptionResult == ConsumptionResult.Calculated && f.FueledAt < fueling.FueledAt &&
                        f.FuelType.Unit == fueling.FuelType.Unit)
            .OrderByDescending(f => f.FueledAt)
            .Take(ConsumptionBaseline.VehicleHistorySegments)
            .Select(f => new { Distance = f.SegmentDistanceKm!.Value, Quantity = f.SegmentQuantity!.Value })
            .ToListAsync(ct);
        return rows.Count == 0 ? null : new ConsumptionSample(rows.Sum(r => (long)r.Distance), rows.Sum(r => r.Quantity), rows.Count);
    }

    /// <summary>Same vehicle type and fuel unit across the company, over the last <see cref="ConsumptionBaseline.TypeAverageDays"/> days.</summary>
    private async Task<ConsumptionSample?> TypeAverageAsync(Fueling fueling, Vehicle vehicle, CancellationToken ct)
    {
        var since = fueling.FueledOn.AddDays(-ConsumptionBaseline.TypeAverageDays);
        var unit = fueling.FuelType.Unit;
        var sample = await db.Fuelings
            .Where(f => f.Id != fueling.Id && f.Status != FuelingStatus.Cancelled && f.ConsumptionResult == ConsumptionResult.Calculated &&
                        f.Vehicle.Type == vehicle.Type && f.FuelType.Unit == unit && f.FueledOn >= since && f.FueledOn <= fueling.FueledOn)
            .GroupBy(_ => 1)
            .Select(g => new { Distance = g.Sum(f => (long)f.SegmentDistanceKm!.Value), Quantity = g.Sum(f => f.SegmentQuantity!.Value), Count = g.Count() })
            .FirstOrDefaultAsync(ct);
        return sample is null ? null : new ConsumptionSample(sample.Distance, sample.Quantity, sample.Count);
    }

    /// <summary>The station's reference price in force on the date; otherwise the company's recent weighted average for the product.</summary>
    private async Task<decimal?> ReferencePriceAsync(Fueling fueling, CancellationToken ct)
    {
        if (fueling.FuelStationId is { } stationId)
        {
            var reference = await db.FuelPrices
                .Where(p => p.FuelStationId == stationId && p.FuelTypeId == fueling.FuelTypeId && p.EffectiveFrom <= fueling.FueledOn)
                .OrderByDescending(p => p.EffectiveFrom).ThenByDescending(p => p.CreatedAt)
                .Select(p => (decimal?)p.Price).FirstOrDefaultAsync(ct);
            if (reference is not null) return reference;
        }

        var since = fueling.FueledOn.AddDays(-ReferencePriceDays);
        var recent = await db.Fuelings
            .Where(f => f.Id != fueling.Id && f.FuelTypeId == fueling.FuelTypeId && f.Status != FuelingStatus.Cancelled &&
                        f.FueledOn >= since && f.FueledOn <= fueling.FueledOn)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(f => f.TotalAmount), Quantity = g.Sum(f => f.Quantity) })
            .FirstOrDefaultAsync(ct);
        return recent is { Count: >= MinReferencePriceSamples, Quantity: > 0 }
            ? Math.Round(recent.Total / recent.Quantity, 4, MidpointRounding.AwayFromZero)
            : null;
    }

    private async Task<double?> HoursFromNearestFuelingAsync(Fueling fueling, CancellationToken ct)
    {
        var others = db.Fuelings.Where(f => f.VehicleId == fueling.VehicleId && f.Id != fueling.Id && f.Status != FuelingStatus.Cancelled);
        var before = await others.Where(f => f.FueledAt <= fueling.FueledAt).OrderByDescending(f => f.FueledAt)
            .Select(f => (DateTime?)f.FueledAt).FirstOrDefaultAsync(ct);
        var after = await others.Where(f => f.FueledAt > fueling.FueledAt).OrderBy(f => f.FueledAt)
            .Select(f => (DateTime?)f.FueledAt).FirstOrDefaultAsync(ct);
        double? Hours(DateTime? other) => other is { } at ? Math.Abs((fueling.FueledAt - at).TotalHours) : null;
        var candidates = new[] { Hours(before), Hours(after) }.Where(h => h is not null).ToList();
        return candidates.Count == 0 ? null : candidates.Min();
    }

    /// <summary>Replaces the anomalies of the given types with the detected ones, keeping the review of types that persist.</summary>
    private List<FuelingAnomaly> Merge(Fueling fueling, IReadOnlyCollection<FuelAnomalyType> types, IReadOnlyList<DetectedAnomaly> detected)
    {
        foreach (var stale in fueling.Anomalies.Where(a => types.Contains(a.Type) && detected.All(d => d.Type != a.Type)).ToList())
            fueling.Anomalies.Remove(stale);

        var added = new List<FuelingAnomaly>();
        foreach (var found in detected)
        {
            var existing = fueling.Anomalies.FirstOrDefault(a => a.Type == found.Type);
            if (existing is not null)
            {
                existing.Message = Truncate(found.Message);
                existing.ExpectedValue = found.Expected;
                existing.ActualValue = found.Actual;
                continue;
            }
            var anomaly = new FuelingAnomaly
            {
                FuelingId = fueling.Id, Type = found.Type, Message = Truncate(found.Message),
                ExpectedValue = found.Expected, ActualValue = found.Actual, DetectedAt = clock.UtcNow,
            };
            fueling.Anomalies.Add(anomaly);
            added.Add(anomaly);
        }
        return added;
    }

    public static EventSubject Subject(Fueling f, Vehicle v) => new(nameof(Fueling), f.Id, v.Id, f.DriverId);

    public static string Plate(Vehicle v) => LicensePlate.Format(v.LicensePlate);

    private static string Truncate(string message) =>
        message.Length <= FuelingAnomaly.MessageMaxLength ? message : message[..(FuelingAnomaly.MessageMaxLength - 1)] + "…";
}
