namespace Fleet.Domain.Intelligence;

public enum TrendDirection
{
    Stable,
    Up,
    Down,
}

public sealed record TrendResult(TrendDirection Direction, decimal? ChangePercent, bool IsSignificant);

/// <summary>
/// Period-over-period comparison (spec §15): plain rule-based statistics, no machine learning. A change only counts
/// when the base is large enough to mean something and the variation passes the minimum — small noise is "stable".
/// </summary>
public static class TrendAnalysis
{
    public static TrendResult Compare(decimal current, decimal previous, decimal minBase, decimal minChangePercent)
    {
        if (previous <= 0 || previous < minBase) return new TrendResult(TrendDirection.Stable, null, false);
        var change = Math.Round((current - previous) / previous * 100m, 1, MidpointRounding.AwayFromZero);
        if (Math.Abs(change) < minChangePercent) return new TrendResult(TrendDirection.Stable, change, false);
        return new TrendResult(change > 0 ? TrendDirection.Up : TrendDirection.Down, change, true);
    }
}

public enum HealthFactorStatus
{
    Good,
    Attention,
    Critical,
    /// <summary>The reader cannot see this area — it neither adds nor subtracts.</summary>
    NotVisible,
}

public enum HealthFactorArea
{
    Maintenance,
    Fuel,
    Tires,
    Costs,
    Documents,
    Occurrences,
    Mileage,
}

public sealed record HealthFactor(HealthFactorArea Area, HealthFactorStatus Status, string Explanation, string? Tab);

public enum HealthLevel
{
    Good,
    Attention,
    Critical,
}

/// <summary>
/// Operational health of a vehicle (spec §16): a transparent checklist turned into a 0–100 number, NOT a scientific
/// measure. Each area in attention subtracts 10 and each critical area 25; areas the reader cannot see are ignored.
/// </summary>
public static class VehicleHealthPolicy
{
    public const int AttentionPenalty = 10;
    public const int CriticalPenalty = 25;

    public static int Score(IEnumerable<HealthFactor> factors) =>
        Math.Max(0, 100 - factors.Sum(f => f.Status switch
        {
            HealthFactorStatus.Attention => AttentionPenalty,
            HealthFactorStatus.Critical => CriticalPenalty,
            _ => 0,
        }));

    public static HealthLevel Level(int score) => score switch
    {
        >= 80 => HealthLevel.Good,
        >= 50 => HealthLevel.Attention,
        _ => HealthLevel.Critical,
    };

    public static HealthFactorStatus Worst(params HealthFactorStatus[] statuses) =>
        statuses.Contains(HealthFactorStatus.Critical) ? HealthFactorStatus.Critical
        : statuses.Contains(HealthFactorStatus.Attention) ? HealthFactorStatus.Attention
        : HealthFactorStatus.Good;
}
