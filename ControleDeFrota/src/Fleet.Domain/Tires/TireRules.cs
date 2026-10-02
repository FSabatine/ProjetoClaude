using System.Globalization;
using System.Text.RegularExpressions;
using Fleet.Domain.Common;

namespace Fleet.Domain.Tires;

/// <summary>
/// The only place that says which lifecycle operation each status allows (seção 6 — no arbitrary transitions).
/// Services ask here; the API returns the resulting actions so the UI never decides a rule.
/// </summary>
public static class TireWorkflow
{
    public static bool CanInstall(TireStatus s) => s == TireStatus.InStock;
    public static bool CanRemove(TireStatus s) => s == TireStatus.Installed;
    public static bool CanRotate(TireStatus s) => s == TireStatus.Installed;
    /// <summary>Repair/retread: from stock or from evaluation (an installed tire is removed with destination repair/retread).</summary>
    public static bool CanSendToService(TireStatus s) => s is TireStatus.InStock or TireStatus.UnderInspection;
    /// <summary>A repair done on the vehicle, without removing the tire.</summary>
    public static bool CanRepairInPlace(TireStatus s) => s == TireStatus.Installed;
    /// <summary>Evaluation done, the tire can be used again.</summary>
    public static bool CanReturnToStock(TireStatus s) => s == TireStatus.UnderInspection;
    public static bool CanSendToEvaluation(TireStatus s) => s == TireStatus.InStock;
    /// <summary>An installed tire is disposed through its removal (destination Disposal) — never left on a vehicle.</summary>
    public static bool CanDispose(TireStatus s) => s is TireStatus.InStock or TireStatus.UnderInspection;
    public static bool CanInspect(TireStatus s) => s != TireStatus.Disposed;
    public static bool CanEdit(TireStatus s) => s != TireStatus.Disposed;
    public static bool IsFinal(TireStatus s) => s == TireStatus.Disposed;

    /// <summary>Status after a removal, by destination. Disposal is final; "Installed" is a rotation/transfer.</summary>
    public static TireStatus AfterRemoval(TireRemovalDestination destination) => destination switch
    {
        TireRemovalDestination.Stock => TireStatus.InStock,
        TireRemovalDestination.Evaluation => TireStatus.UnderInspection,
        TireRemovalDestination.Repair => TireStatus.UnderRepair,
        TireRemovalDestination.Retread => TireStatus.UnderRetread,
        TireRemovalDestination.Disposal => TireStatus.Disposed,
        _ => TireStatus.Installed,
    };

    /// <summary>Status after a service order closes: approved → stock; rejected or cancelled → evaluation (someone decides).</summary>
    public static TireStatus AfterService(TireServiceStatus status, TireServiceResult? result) =>
        status == TireServiceStatus.Completed && result == TireServiceResult.Approved ? TireStatus.InStock : TireStatus.UnderInspection;

    public static TireStatus WhileInService(TireServiceKind kind) =>
        kind == TireServiceKind.Retread ? TireStatus.UnderRetread : TireStatus.UnderRepair;
}

public static partial class TireSizes
{
    /// <summary>"295/80 r 22,5" → "295/80R22.5": uppercase, no spaces, decimal point. Comparison happens on this form.</summary>
    public static string Normalize(string? size) =>
        Whitespace().Replace((size ?? string.Empty).Trim().ToUpperInvariant(), string.Empty).Replace(',', '.');

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

public static partial class TireDot
{
    public static string? Normalize(string? dot)
    {
        if (string.IsNullOrWhiteSpace(dot)) return null;
        var text = Spaces().Replace(dot.Trim().ToUpperInvariant(), " ");
        return text.StartsWith("DOT ", StringComparison.Ordinal) ? text[4..] : text == "DOT" ? null : text;
    }

    /// <summary>
    /// Manufacturing date from the DOT date code: the last four digits are week (01–53) and year ("2423" = week 24 of 2023).
    /// Returns the Monday of that ISO week, or null when the DOT has no valid code (pre-2000 three-digit codes included).
    /// </summary>
    public static DateOnly? ManufacturedOn(string? dot)
    {
        var normalized = Normalize(dot);
        if (normalized is null) return null;
        var digits = new string(normalized.Where(char.IsAsciiLetterOrDigit).ToArray());
        if (digits.Length < 4) return null;
        var code = digits[^4..];
        if (!code.All(char.IsAsciiDigit)) return null;
        var week = int.Parse(code[..2], CultureInfo.InvariantCulture);
        var year = 2000 + int.Parse(code[2..], CultureInfo.InvariantCulture);
        if (week < 1 || week > ISOWeek.GetWeeksInYear(year)) return null;
        return DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

public static class TirePressure
{
    public const decimal MaxPsi = 250m;

    public static decimal ToPsi(decimal value, TirePressureUnit unit) => unit switch
    {
        TirePressureUnit.Bar => value * 14.5038m,
        TirePressureUnit.Kpa => value * 0.145038m,
        _ => value,
    };

    public static decimal FromPsi(decimal psi, TirePressureUnit unit) => unit switch
    {
        TirePressureUnit.Bar => Math.Round(psi / 14.5038m, 2),
        TirePressureUnit.Kpa => Math.Round(psi / 0.145038m, 0),
        _ => Math.Round(psi, 0),
    };

    /// <summary>Reading vs. the position's reference with the company tolerance. No reference = not evaluated (never guessed).</summary>
    public static TirePressureCheck Check(decimal value, TirePressureUnit unit, decimal? recommendedPsi, int tolerancePercent)
    {
        if (recommendedPsi is not > 0) return TirePressureCheck.NotEvaluated;
        var psi = ToPsi(value, unit);
        var margin = recommendedPsi.Value * tolerancePercent / 100m;
        return psi < recommendedPsi - margin ? TirePressureCheck.Low
            : psi > recommendedPsi + margin ? TirePressureCheck.High
            : TirePressureCheck.WithinRange;
    }
}

/// <summary>Tire km comes from the vehicle odometer history (seção 38) — there is no tire odometer.</summary>
public static class TireMileage
{
    /// <summary>
    /// km of a stint: odometer at removal − at installation; 0 for a spare (it does not roll); null when either end is unknown
    /// (implements have no odometer). A later odometer correction below the installation value never yields negative km.
    /// </summary>
    public static int? StintDistance(bool isSpare, int? installedKm, int? removedKm) =>
        isSpare ? 0 : installedKm is { } from && removedKm is { } to ? Math.Max(0, to - from) : null;

    /// <summary>Accumulated km plus the running km of the open stint on a vehicle.</summary>
    public static int Current(int accumulatedKm, bool hasOpenStint, bool isSpare, int? installedKm, int? vehicleCurrentKm) =>
        accumulatedKm + (hasOpenStint ? StintDistance(isSpare, installedKm, vehicleCurrentKm) ?? 0 : 0);
}

/// <summary>Cost/km only when it means something (seção 25): known km, enough of it, no unmeasured stretch.</summary>
public static class TireCostPolicy
{
    /// <summary>Below this a cost/km figure is dominated by the purchase price and misleads more than it informs.</summary>
    public const int MinKmForCostPerKm = 5_000;

    public static decimal? CostPerKm(decimal totalCost, int km, bool hasUnmeasuredDistance) =>
        totalCost <= 0 || km < MinKmForCostPerKm || hasUnmeasuredDistance ? null : Math.Round(totalCost / km, 4);
}

public enum TireCompatibilityStatus
{
    Compatible,
    /// <summary>Installable, with points to check (application, mismatched dual pair).</summary>
    Warning,
    /// <summary>Blocked: the position requires another size.</summary>
    Incompatible,
    /// <summary>The position has no size configured — nothing could be checked (seção 33).</summary>
    NotVerified,
}

public sealed record TireCompatibility(TireCompatibilityStatus Status, IReadOnlyList<string> Messages)
{
    public const string NotVerifiedMessage = "A compatibilidade não pôde ser verificada automaticamente: a posição não tem medida configurada.";

    /// <summary>
    /// Only what the registered data supports (seção 33): the axle's allowed size (blocking), the tire application vs. the axle
    /// type and the size of the dual partner (warnings). Load and speed ratings are informative — no position requirement exists.
    /// </summary>
    public static TireCompatibility Check(TirePosition position, string tireSize, TireApplication application, string? dualPartnerSize)
    {
        var size = TireSizes.Normalize(tireSize);
        if (position.AllowedSize is { Length: > 0 } allowed && TireSizes.Normalize(allowed) != size)
            return new(TireCompatibilityStatus.Incompatible,
                [$"A posição {position.Label} exige a medida {allowed}; o pneu é {tireSize}. Escolha um pneu da medida configurada."]);

        var warnings = new List<string>();
        if (position.AxleType is { } axle && !ApplicationFits(application, axle))
            warnings.Add($"Pneu de aplicação {ApplicationText(application)} em eixo {AxleText(axle)}. Confira a recomendação do fabricante.");
        if (dualPartnerSize is not null && TireSizes.Normalize(dualPartnerSize) != size)
            warnings.Add($"O pneu ao lado nesta roda dupla é {dualPartnerSize}; pneus geminados normalmente têm a mesma medida.");

        if (warnings.Count > 0) return new(TireCompatibilityStatus.Warning, warnings);
        return position.AllowedSize is { Length: > 0 } || position.IsSpare
            ? new(TireCompatibilityStatus.Compatible, [])
            : new(TireCompatibilityStatus.NotVerified, [NotVerifiedMessage]);
    }

    private static bool ApplicationFits(TireApplication application, AxleType axle) => application switch
    {
        TireApplication.AllPosition => true,
        TireApplication.Steer => true, // a steer tire is accepted anywhere; the reverse is what manufacturers advise against
        TireApplication.Drive => axle is AxleType.Drive or AxleType.Free,
        TireApplication.Trailer => axle is AxleType.Trailer or AxleType.Free,
        _ => true,
    };

    private static string ApplicationText(TireApplication a) => a switch
    {
        TireApplication.Steer => "direcional",
        TireApplication.Drive => "tração",
        TireApplication.Trailer => "reboque",
        _ => "uso misto",
    };

    private static string AxleText(AxleType a) => a switch
    {
        AxleType.Steer => "direcional",
        AxleType.Drive => "de tração",
        AxleType.Free => "livre",
        _ => "de reboque",
    };
}

public enum TireAlertType
{
    TreadBelowMinimum,
    TreadNearMinimum,
    InspectionOverdue,
    UnevenWear,
    DamageReported,
    AgeExceeded,
    PressureOutOfRange,
}

public enum TireAlertSeverity
{
    Warning,
    Critical,
}

public sealed record TireAlert(TireAlertType Type, TireAlertSeverity Severity, string Message);

/// <summary>
/// Calculated alerts (seção 34), never stored — the wording is neutral and always says "configurado pela empresa":
/// the thresholds are company policy, not a legal determination.
/// </summary>
public static class TireAlertPolicy
{
    public static IReadOnlyList<TireAlert> Evaluate(Tire tire, TireSettings settings, DateTime now, DateOnly today)
    {
        var alerts = new List<TireAlert>();
        if (TireWorkflow.IsFinal(tire.Status)) return alerts;

        if (tire.CurrentTreadDepthMm is { } tread)
        {
            if (tread <= settings.MinTreadDepthMm)
                alerts.Add(new(TireAlertType.TreadBelowMinimum, TireAlertSeverity.Critical,
                    $"Sulco de {Mm(tread)} no limite mínimo configurado pela empresa ({Mm(settings.MinTreadDepthMm)}). Avalie a substituição."));
            else if (tread <= settings.TreadWarningDepthMm)
                alerts.Add(new(TireAlertType.TreadNearMinimum, TireAlertSeverity.Warning,
                    $"Sulco de {Mm(tread)} se aproximando do mínimo configurado ({Mm(settings.MinTreadDepthMm)})."));
        }
        if (IsInspectionOverdue(tire, settings, now))
            alerts.Add(new(TireAlertType.InspectionOverdue, TireAlertSeverity.Warning,
                $"Inspeção atrasada: o intervalo configurado é de {settings.InspectionIntervalDays} dias."));
        if (tire.LastWearPattern is { } wear && wear is not (TireWearPattern.Normal or TireWearPattern.Unknown))
            alerts.Add(new(TireAlertType.UnevenWear, TireAlertSeverity.Warning,
                "Desgaste irregular registrado na última inspeção. Requer revisão."));
        if (tire.LastInspectionHasDamage)
            alerts.Add(new(TireAlertType.DamageReported, TireAlertSeverity.Critical,
                "Dano registrado na última inspeção. O pneu requer inspeção antes de seguir em uso."));
        if (IsAgeExceeded(tire, settings, today))
            alerts.Add(new(TireAlertType.AgeExceeded, TireAlertSeverity.Warning,
                $"Pneu fabricado há mais de {settings.MaxAgeYears} anos (limite configurado pela empresa). Requer revisão."));
        if (tire.LastPressureCheck is TirePressureCheck.Low or TirePressureCheck.High)
            alerts.Add(new(TireAlertType.PressureOutOfRange, TireAlertSeverity.Warning,
                tire.LastPressureCheck == TirePressureCheck.Low
                    ? "Pressão abaixo da referência na última medição."
                    : "Pressão acima da referência na última medição."));
        return alerts;
    }

    public static bool IsInspectionOverdue(Tire tire, TireSettings settings, DateTime now) =>
        tire.Status == TireStatus.Installed && settings.InspectionIntervalDays > 0 &&
        tire.InspectionReferenceAt is { } reference && reference < InspectionDueBefore(settings, now);

    /// <summary>SQL form: an installed tire whose reference is older than this is overdue.</summary>
    public static DateTime InspectionDueBefore(TireSettings settings, DateTime now) => now.AddDays(-settings.InspectionIntervalDays);

    public static bool IsAgeExceeded(Tire tire, TireSettings settings, DateOnly today) =>
        settings.MaxAgeYears > 0 && tire.ManufacturedOn is { } made && made < AgeLimit(settings, today);

    /// <summary>SQL form: manufactured before this date = older than the configured age.</summary>
    public static DateOnly AgeLimit(TireSettings settings, DateOnly today) => today.AddYears(-settings.MaxAgeYears);

    private static string Mm(decimal mm) => $"{BrazilianFormat.Compact(mm)} mm";
}

/// <summary>One tread measurement of the tire's history (chronological).</summary>
public sealed record TreadMeasurement(DateTime At, decimal TreadDepthMm, int? TireKm);

/// <summary>
/// Foundation of the anomaly rules (seção 35). Deterministic, explained, always "requires review" — none of them names a
/// mechanical cause. Thresholds that are not company settings are named constants documented in DOMAIN.md.
/// </summary>
public static class TireAnomalyRules
{
    public const int RepeatedRepairsCount = 3;
    public const int RepeatedRepairsWindowDays = 365;
    public const int RepeatedPuncturesCount = 2;
    public const int RepeatedPuncturesWindowDays = 180;
    public const int RecurringPositionCount = 2;
    public const int RecurringPositionWindowDays = 180;
    /// <summary>Wear over less than this distance is noise (measurement precision ~0.5 mm).</summary>
    public const int MinKmForWearRate = 1_000;

    /// <summary>mm lost per 1,000 km between two measurements; null when not comparable (retread in between, too little km).</summary>
    public static decimal? WearRate(TreadMeasurement previous, TreadMeasurement current)
    {
        if (previous.TireKm is not { } fromKm || current.TireKm is not { } toKm) return null;
        var km = toKm - fromKm;
        var lost = previous.TreadDepthMm - current.TreadDepthMm;
        if (km < MinKmForWearRate || lost <= 0) return null;
        return Math.Round(lost / km * 1000m, 2);
    }

    public static string? RapidTreadLoss(TreadMeasurement? previous, TreadMeasurement current, TireSettings settings)
    {
        if (previous is null || settings.RapidWearMmPer1000Km <= 0) return null;
        var rate = WearRate(previous, current);
        return rate > settings.RapidWearMmPer1000Km
            ? $"Perda de sulco de {BrazilianFormat.Number(rate.Value, 2)} mm a cada 1.000 km desde a medição anterior " +
              $"(referência configurada: {BrazilianFormat.Number(settings.RapidWearMmPer1000Km, 2)} mm). Requer revisão."
            : null;
    }

    public static string? RepeatedRepairs(int repairsInWindow) =>
        repairsInWindow >= RepeatedRepairsCount
            ? $"{repairsInWindow} consertos nos últimos {RepeatedRepairsWindowDays} dias. Requer revisão." : null;

    public static string? RepeatedPunctures(int puncturesInWindow) =>
        puncturesInWindow >= RepeatedPuncturesCount
            ? $"{puncturesInWindow} furos registrados nos últimos {RepeatedPuncturesWindowDays} dias. Requer revisão." : null;

    public static string? ShortLifecycle(int accumulatedKm, bool hasUnmeasuredDistance, TireSettings settings) =>
        settings.MinExpectedLifeKm > 0 && !hasUnmeasuredDistance && accumulatedKm < settings.MinExpectedLifeKm
            ? $"Pneu baixado com {BrazilianFormat.Number(accumulatedKm)} km, abaixo da vida mínima esperada configurada " +
              $"({BrazilianFormat.Number(settings.MinExpectedLifeKm)} km). Requer revisão."
            : null;

    public static string? RecurringPositionDamage(string positionLabel, string assetLabel, int damageRemovalsInWindow) =>
        damageRemovalsInWindow >= RecurringPositionCount
            ? $"{damageRemovalsInWindow} remoções por dano na posição {positionLabel} de {assetLabel} nos últimos " +
              $"{RecurringPositionWindowDays} dias. Requer revisão da posição."
            : null;
}
