namespace Fleet.Domain.Finance;

/// <summary>
/// PaymentStatus is never stored (same idea as DocumentExpiryPolicy). Pure function: easy to unit test, and the
/// one place both the list screen and any SQL-side filter must agree with.
/// </summary>
public static class ExpensePaymentPolicy
{
    public static PaymentStatus Evaluate(bool isCancelled, decimal amount, decimal paidAmount, DateOnly? dueDate, DateOnly today)
    {
        if (isCancelled) return PaymentStatus.Cancelled;
        if (paidAmount >= amount && amount > 0) return PaymentStatus.Paid;
        if (paidAmount > 0) return PaymentStatus.PartiallyPaid;
        if (dueDate is { } due && due < today) return PaymentStatus.Overdue;
        if (dueDate is not null) return PaymentStatus.Scheduled;
        return PaymentStatus.Pending;
    }
}

/// <summary>Pure date math for the recurring-expense generator (ADR-040) — no persistence, no side effects.</summary>
public static class RecurringExpensePolicy
{
    /// <summary>The day-of-month, clamped to the days the target month actually has (e.g. 31 in February → 28/29).</summary>
    public static DateOnly DueDateIn(int year, int month, int dayOfMonth)
    {
        var day = Math.Clamp(dayOfMonth, 1, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, day);
    }

    /// <summary>First due date on/after startDate, honoring the configured day-of-month.</summary>
    public static DateOnly FirstDueDate(DateOnly startDate, int dayOfMonth)
    {
        var candidate = DueDateIn(startDate.Year, startDate.Month, dayOfMonth);
        return candidate >= startDate ? candidate : NextDueDate(candidate, ExpenseFrequency.Monthly, dayOfMonth);
    }

    public static DateOnly NextDueDate(DateOnly lastDueDate, ExpenseFrequency frequency, int dayOfMonth)
    {
        var monthsToAdd = frequency switch
        {
            ExpenseFrequency.Monthly => 1,
            ExpenseFrequency.Quarterly => 3,
            ExpenseFrequency.Semiannual => 6,
            ExpenseFrequency.Annual => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency)),
        };
        var next = lastDueDate.AddMonths(monthsToAdd);
        return DueDateIn(next.Year, next.Month, dayOfMonth);
    }

    /// <summary>
    /// Every due date from the template's cursor up to the generation horizon (inclusive), so upcoming obligations
    /// show ahead of time (spec §8) without generating the whole future at once.
    /// </summary>
    public static IReadOnlyList<DateOnly> DueDatesToGenerate(
        DateOnly startDate, DateOnly? endDate, int dayOfMonth, ExpenseFrequency frequency, DateOnly? lastGeneratedDueDate, DateOnly horizon)
    {
        var dates = new List<DateOnly>();
        var next = lastGeneratedDueDate is { } last ? NextDueDate(last, frequency, dayOfMonth) : FirstDueDate(startDate, dayOfMonth);
        while (next <= horizon && (endDate is null || next <= endDate))
        {
            dates.Add(next);
            next = NextDueDate(next, frequency, dayOfMonth);
        }
        return dates;
    }
}

/// <summary>Insufficient-mileage-data guard for period cost/km, mirroring TireCostPolicy but scaled to a period rather than a lifecycle.</summary>
public static class VehicleCostPolicy
{
    /// <summary>Below this a cost/km figure is dominated by measurement noise rather than real usage.</summary>
    public const int MinKmForCostPerKm = 50;

    public static decimal? CostPerKm(decimal totalCost, int? distanceKm) =>
        totalCost <= 0 || distanceKm is null or < MinKmForCostPerKm ? null : Math.Round(totalCost / distanceKm.Value, 4);
}

/// <summary>Budget vs actual math (spec §14) — pure, so the dashboard tile and the report row compute it identically.</summary>
public static class BudgetAnalysis
{
    public static decimal Remaining(decimal budget, decimal actual) => budget - actual;

    /// <summary>Null when there is no budget to compare against (avoids a meaningless "infinite" percentage).</summary>
    public static decimal? UtilizationPercent(decimal budget, decimal actual) =>
        budget <= 0 ? null : Math.Round(actual / budget * 100m, 1);

    public static BudgetStatus Status(decimal budget, decimal actual)
    {
        if (budget <= 0) return BudgetStatus.NoBudget;
        var ratio = actual / budget;
        return ratio switch
        {
            > 1m => BudgetStatus.OverBudget,
            >= 0.9m => BudgetStatus.NearBudget,
            _ => BudgetStatus.UnderBudget,
        };
    }
}

public enum BudgetStatus
{
    UnderBudget,
    NearBudget,
    OverBudget,
    NoBudget,
}
