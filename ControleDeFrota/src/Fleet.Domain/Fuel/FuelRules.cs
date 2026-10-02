using Fleet.Domain.Common;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Fuel;

/// <summary>
/// Per-company thresholds of the anomaly rules (seção 21: configurable, not hard-coded). A company without a row
/// uses <see cref="Defaults"/>. Changing a threshold affects new evaluations only; stored results are snapshots.
/// </summary>
public class FuelSettings : AuditableEntity, ITenantScoped, IAuditable
{
    public const int MaxPercent = 200;
    public const int MaxHours = 48;

    public Guid CompanyId { get; set; }
    /// <summary>Quantity above (tank capacity × (1 + tolerance)) is flagged. Pump calibration and filler neck hold a little extra.</summary>
    public int TankTolerancePercent { get; set; } = 5;
    /// <summary>Unit price this far from the company's recent average for the same product is flagged.</summary>
    public int PriceDeviationPercent { get; set; } = 20;
    /// <summary>Consumption this far below or above the baseline is flagged.</summary>
    public int ConsumptionDeviationPercent { get; set; } = 20;
    /// <summary>Two fuelings of the same vehicle closer than this are flagged. 0 disables the rule.</summary>
    public int MinHoursBetweenFuelings { get; set; } = 2;
    /// <summary>When true, a fueling must name the driver.</summary>
    public bool RequireDriver { get; set; }

    public static FuelSettings Defaults() => new();
}

public static class FuelingAmounts
{
    /// <summary>Accepted difference between a total typed by the user (pump receipt) and quantity × price.</summary>
    public const decimal TotalTolerance = 0.05m;
    public const decimal MaxQuantity = 100_000m;
    public const decimal MaxUnitPrice = 1_000m;

    /// <summary>The only way a total is produced: the client's total is checked against this, never stored as is.</summary>
    public static decimal Total(decimal quantity, decimal unitPrice) =>
        Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);

    public static bool TotalMatches(decimal informed, decimal quantity, decimal unitPrice) =>
        Math.Abs(informed - Total(quantity, unitPrice)) <= TotalTolerance;
}

/// <summary>Allowed transitions (seção 13). Corrections keep the status but re-run the checks.</summary>
public static class FuelingWorkflow
{
    public static bool CanCorrect(FuelingStatus status) => status != FuelingStatus.Cancelled;
    public static bool CanCancel(FuelingStatus status) => status != FuelingStatus.Cancelled;
    public static bool CanReview(FuelingStatus status) => status == FuelingStatus.PendingReview;
}

/// <summary>One fueling of the vehicle chain, chronological, cancelled ones excluded.</summary>
public sealed record ConsumptionChainEntry(
    Guid FuelingId, DateTime FueledAt, int OdometerKm, decimal Quantity, decimal TotalAmount, bool IsFullTank, FuelUnit Unit,
    bool MileageUnderReview);

public sealed record ConsumptionOutcome(
    Guid FuelingId, ConsumptionResult Result, int? DistanceKm = null, decimal? Quantity = null, decimal? Cost = null, decimal? Consumption = null);

/// <summary>
/// Tank-to-tank consumption (ADR-032). A full-tank fueling closes the segment that started at the previous full tank:
/// distance = km(full) − km(previous full); fuel = every quantity after the previous full up to and including this one.
/// Partial fills never have a figure of their own. The first full tank only opens the first segment.
/// </summary>
public static class ConsumptionCalculator
{
    public static IReadOnlyList<ConsumptionOutcome> Calculate(
        IReadOnlyList<ConsumptionChainEntry> chain, IReadOnlyCollection<DateTime> odometerCorrections)
    {
        var outcomes = new List<ConsumptionOutcome>(chain.Count);
        var previousFull = -1;
        for (var i = 0; i < chain.Count; i++)
        {
            var entry = chain[i];
            if (!entry.IsFullTank)
            {
                outcomes.Add(new(entry.FuelingId, ConsumptionResult.PartialFill));
                continue;
            }
            outcomes.Add(previousFull < 0
                ? new(entry.FuelingId, ConsumptionResult.FirstFullTank)
                : Segment(chain, previousFull, i, odometerCorrections));
            previousFull = i;
        }
        return outcomes;
    }

    private static ConsumptionOutcome Segment(IReadOnlyList<ConsumptionChainEntry> chain, int start, int end, IReadOnlyCollection<DateTime> corrections)
    {
        var open = chain[start];
        var close = chain[end];
        var fills = chain.Skip(start + 1).Take(end - start).ToList();
        var distance = close.OdometerKm - open.OdometerKm;
        var quantity = fills.Sum(f => f.Quantity);
        var cost = fills.Sum(f => f.TotalAmount);

        // Each of these makes km or fuel of the segment meaningless; better no figure than a misleading one.
        var unreliable = distance <= 0 || quantity <= 0 ||
                         fills.Any(f => f.Unit != close.Unit) || open.Unit != close.Unit ||
                         open.MileageUnderReview || fills.Any(f => f.MileageUnderReview) ||
                         corrections.Any(c => c > open.FueledAt && c <= close.FueledAt);
        if (unreliable) return new(close.FuelingId, ConsumptionResult.NotReliable);

        return new(close.FuelingId, ConsumptionResult.Calculated, distance, quantity, cost,
            Math.Round(distance / quantity, 2, MidpointRounding.AwayFromZero));
    }
}

/// <summary>Aggregate of measured segments: weighted average = total km / total fuel (never the average of ratios).</summary>
public sealed record ConsumptionSample(long DistanceKm, decimal Quantity, int Segments)
{
    public decimal? Average => Quantity > 0 ? Math.Round(DistanceKm / Quantity, 2, MidpointRounding.AwayFromZero) : null;
}

public sealed record ResolvedBaseline(decimal Value, ConsumptionBaselineSource Source);

/// <summary>Normal consumption of a vehicle (seção 20): configured &gt; own history &gt; same vehicle type.</summary>
public static class ConsumptionBaseline
{
    public const int VehicleHistorySegments = 10;
    public const int MinVehicleSegments = 3;
    public const int MinTypeSegments = 5;
    public const int TypeAverageDays = 180;

    public static ResolvedBaseline? Resolve(decimal? configured, ConsumptionSample? vehicleHistory, ConsumptionSample? typeAverage)
    {
        if (configured is > 0) return new(configured.Value, ConsumptionBaselineSource.Configured);
        if (vehicleHistory is { Segments: >= MinVehicleSegments, Average: { } own })
            return new(own, ConsumptionBaselineSource.VehicleHistory);
        if (typeAverage is { Segments: >= MinTypeSegments, Average: { } type })
            return new(type, ConsumptionBaselineSource.VehicleTypeAverage);
        return null;
    }
}

public sealed record DetectedAnomaly(FuelAnomalyType Type, string Message, decimal? Expected = null, decimal? Actual = null);

/// <summary>Facts about one fueling the record-level rules look at (gathered by the service, evaluated here).</summary>
public sealed record FuelingCheck(
    decimal Quantity,
    FuelUnit Unit,
    decimal UnitPrice,
    decimal? TankCapacity,
    decimal? ReferencePrice,
    double? HoursFromNearestFueling,
    VehicleFuelType VehicleFuel,
    FuelCategory ProductCategory,
    string ProductName,
    string? MileageAnomaly);

/// <summary>
/// Anomaly rules (seções 12 e 21) — the single place for them. Messages are factual and neutral: they say what is
/// unusual and ask for a review, never conclude fraud or a mechanical problem. Price messages carry no money values,
/// because they are shown to people who may not see costs.
/// </summary>
public static class FuelAnomalyRules
{
    public static readonly FuelAnomalyType[] RecordTypes =
    [
        FuelAnomalyType.ExcessiveQuantity, FuelAnomalyType.AbnormalPrice, FuelAnomalyType.MileageJump,
        FuelAnomalyType.HighFrequency, FuelAnomalyType.FuelTypeMismatch,
    ];

    public static readonly FuelAnomalyType[] ConsumptionTypes = [FuelAnomalyType.LowConsumption, FuelAnomalyType.HighConsumption];

    public static IReadOnlyList<DetectedAnomaly> EvaluateRecord(FuelingCheck f, FuelSettings settings)
    {
        var found = new List<DetectedAnomaly>();
        var unit = FuelUnits.Symbol(f.Unit);

        if (f.TankCapacity is > 0 and var capacity)
        {
            var limit = capacity * (1 + settings.TankTolerancePercent / 100m);
            if (f.Quantity > limit)
                found.Add(new(FuelAnomalyType.ExcessiveQuantity,
                    $"Quantidade abastecida ({Qty(f.Quantity)} {unit}) acima da capacidade cadastrada do tanque " +
                    $"({Qty(capacity)} {unit}, com tolerância de {settings.TankTolerancePercent}%). Revisão recomendada.",
                    capacity, f.Quantity));
        }

        if (f.ReferencePrice is > 0 and var reference)
        {
            var deviation = DeviationPercent(f.UnitPrice, reference);
            if (Math.Abs(deviation) > settings.PriceDeviationPercent)
                found.Add(new(FuelAnomalyType.AbnormalPrice,
                    $"Preço por {unit} {BrazilianFormat.Compact(Math.Abs(deviation))}% {(deviation > 0 ? "acima" : "abaixo")} da média recente da empresa " +
                    $"para {f.ProductName}. Confira o valor digitado.",
                    reference, f.UnitPrice));
        }

        if (f.MileageAnomaly is { } mileage)
            found.Add(new(FuelAnomalyType.MileageJump, $"Hodômetro em revisão: {mileage}"));

        if (settings.MinHoursBetweenFuelings > 0 && f.HoursFromNearestFueling is { } hours && hours < settings.MinHoursBetweenFuelings)
            found.Add(new(FuelAnomalyType.HighFrequency,
                $"Outro abastecimento deste veículo foi registrado a {Elapsed(hours)} deste " +
                $"(o intervalo mínimo configurado é de {settings.MinHoursBetweenFuelings} h). Confira se não é um lançamento duplicado."));

        if (!FuelCompatibility.IsCompatible(f.VehicleFuel, f.ProductCategory))
            found.Add(new(FuelAnomalyType.FuelTypeMismatch,
                $"{f.ProductName} não corresponde ao combustível cadastrado para o veículo. Confira o tipo informado."));

        return found;
    }

    public static DetectedAnomaly? EvaluateConsumption(decimal actual, ResolvedBaseline baseline, FuelUnit unit, int thresholdPercent)
    {
        var deviation = DeviationPercent(actual, baseline.Value);
        if (Math.Abs(deviation) <= thresholdPercent) return null;
        var unitText = $"km/{FuelUnits.Symbol(unit)}";
        var facts = $"Esperado: {Qty(baseline.Value)} {unitText} ({BaselineText(baseline.Source)}). " +
                    $"Real: {Qty(actual)} {unitText} ({(deviation > 0 ? "+" : "")}{BrazilianFormat.Compact(deviation)}%).";
        return deviation < 0
            ? new(FuelAnomalyType.LowConsumption, $"Consumo abaixo do esperado. {facts} Revisão recomendada.", baseline.Value, actual)
            // Better than expected usually means fuel missing from the segment (a fueling not registered) or a wrong odometer.
            : new(FuelAnomalyType.HighConsumption,
                $"Consumo acima do esperado. {facts} Pode haver abastecimento não registrado ou hodômetro incorreto no período.",
                baseline.Value, actual);
    }

    public static decimal DeviationPercent(decimal actual, decimal expected) =>
        expected == 0 ? 0 : Math.Round((actual - expected) / expected * 100, 1, MidpointRounding.AwayFromZero);

    private static string BaselineText(ConsumptionBaselineSource source) => source switch
    {
        ConsumptionBaselineSource.Configured => "configurado no veículo",
        ConsumptionBaselineSource.VehicleHistory => "média do próprio veículo",
        _ => "média dos veículos do mesmo tipo",
    };

    private static string Qty(decimal value) => BrazilianFormat.Number(value, 2);

    private static string Elapsed(double hours) =>
        hours < 1 ? $"{Math.Max(1, (int)Math.Round(hours * 60))} min" : $"{BrazilianFormat.Compact((decimal)hours)} h";
}
