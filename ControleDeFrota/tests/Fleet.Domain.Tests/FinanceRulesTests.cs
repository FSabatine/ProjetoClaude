using Fleet.Domain.Finance;
using FluentAssertions;

namespace Fleet.Domain.Tests;

public class ExpensePaymentPolicyTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Fact]
    public void Evaluate_Cancelled_IsAlwaysCancelled() =>
        ExpensePaymentPolicy.Evaluate(isCancelled: true, amount: 100, paidAmount: 100, dueDate: null, Today).Should().Be(PaymentStatus.Cancelled);

    [Fact]
    public void Evaluate_PaidAmountEqualsAmount_IsPaid() =>
        ExpensePaymentPolicy.Evaluate(false, 100, 100, Today.AddDays(-5), Today).Should().Be(PaymentStatus.Paid);

    [Fact]
    public void Evaluate_PaidAmountBelowTotal_IsPartiallyPaid() =>
        ExpensePaymentPolicy.Evaluate(false, 100, 40, Today.AddDays(10), Today).Should().Be(PaymentStatus.PartiallyPaid);

    [Fact]
    public void Evaluate_DueDatePassedAndUnpaid_IsOverdue() =>
        ExpensePaymentPolicy.Evaluate(false, 100, 0, Today.AddDays(-1), Today).Should().Be(PaymentStatus.Overdue);

    [Fact]
    public void Evaluate_DueDateInFutureAndUnpaid_IsScheduled() =>
        ExpensePaymentPolicy.Evaluate(false, 100, 0, Today.AddDays(1), Today).Should().Be(PaymentStatus.Scheduled);

    [Fact]
    public void Evaluate_NoDueDateAndUnpaid_IsPending() =>
        ExpensePaymentPolicy.Evaluate(false, 100, 0, null, Today).Should().Be(PaymentStatus.Pending);

    [Fact]
    public void Evaluate_DueDateTodayAndUnpaid_IsNotOverdue() =>
        ExpensePaymentPolicy.Evaluate(false, 100, 0, Today, Today).Should().Be(PaymentStatus.Scheduled);
}

public class RecurringExpensePolicyTests
{
    [Theory]
    [InlineData(2026, 1, 31, 2026, 1, 31)]   // January has 31 days
    [InlineData(2026, 2, 31, 2026, 2, 28)]   // clamped: 2026 is not a leap year
    [InlineData(2024, 2, 31, 2024, 2, 29)]   // leap year
    public void DueDateIn_ClampsToDaysInMonth(int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay) =>
        RecurringExpensePolicy.DueDateIn(year, month, day).Should().Be(new DateOnly(expectedYear, expectedMonth, expectedDay));

    [Fact]
    public void FirstDueDate_DayAlreadyPassedInStartMonth_RollsToNextMonth() =>
        RecurringExpensePolicy.FirstDueDate(new DateOnly(2026, 3, 20), dayOfMonth: 10).Should().Be(new DateOnly(2026, 4, 10));

    [Fact]
    public void FirstDueDate_DayStillAheadInStartMonth_UsesStartMonth() =>
        RecurringExpensePolicy.FirstDueDate(new DateOnly(2026, 3, 5), dayOfMonth: 10).Should().Be(new DateOnly(2026, 3, 10));

    [Theory]
    [InlineData(ExpenseFrequency.Monthly, 2026, 4)]
    [InlineData(ExpenseFrequency.Quarterly, 2026, 6)]
    [InlineData(ExpenseFrequency.Semiannual, 2026, 9)]
    [InlineData(ExpenseFrequency.Annual, 2027, 3)]
    public void NextDueDate_AdvancesByFrequency(ExpenseFrequency frequency, int expectedYear, int expectedMonth) =>
        RecurringExpensePolicy.NextDueDate(new DateOnly(2026, 3, 10), frequency, dayOfMonth: 10)
            .Should().Be(new DateOnly(expectedYear, expectedMonth, 10));

    [Fact]
    public void DueDatesToGenerate_StopsAtHorizon()
    {
        var dates = RecurringExpensePolicy.DueDatesToGenerate(
            new DateOnly(2026, 1, 10), null, dayOfMonth: 10, ExpenseFrequency.Monthly, lastGeneratedDueDate: null, horizon: new DateOnly(2026, 3, 15));
        dates.Should().Equal(new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 10), new DateOnly(2026, 3, 10));
    }

    [Fact]
    public void DueDatesToGenerate_ResumesFromLastGenerated_NeverRepeats()
    {
        var dates = RecurringExpensePolicy.DueDatesToGenerate(
            new DateOnly(2026, 1, 10), null, dayOfMonth: 10, ExpenseFrequency.Monthly,
            lastGeneratedDueDate: new DateOnly(2026, 2, 10), horizon: new DateOnly(2026, 4, 1));
        dates.Should().Equal(new DateOnly(2026, 3, 10));
    }

    [Fact]
    public void DueDatesToGenerate_StopsAtEndDate()
    {
        var dates = RecurringExpensePolicy.DueDatesToGenerate(
            new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 28), dayOfMonth: 10, ExpenseFrequency.Monthly,
            lastGeneratedDueDate: null, horizon: new DateOnly(2026, 6, 1));
        dates.Should().Equal(new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 10));
    }
}

public class VehicleCostPolicyTests
{
    [Fact]
    public void CostPerKm_BelowMinimumKm_ReturnsNull() =>
        VehicleCostPolicy.CostPerKm(1000m, VehicleCostPolicy.MinKmForCostPerKm - 1).Should().BeNull();

    [Fact]
    public void CostPerKm_NullDistance_ReturnsNull() =>
        VehicleCostPolicy.CostPerKm(1000m, null).Should().BeNull();

    [Fact]
    public void CostPerKm_ZeroCost_ReturnsNull() =>
        VehicleCostPolicy.CostPerKm(0m, 1000).Should().BeNull();

    [Fact]
    public void CostPerKm_SufficientData_Divides() =>
        VehicleCostPolicy.CostPerKm(1000m, 500).Should().Be(2.0000m);
}

public class BudgetAnalysisTests
{
    [Fact]
    public void Remaining_SubtractsActualFromBudget() => BudgetAnalysis.Remaining(1000m, 600m).Should().Be(400m);

    [Fact]
    public void UtilizationPercent_NoBudget_ReturnsNull() => BudgetAnalysis.UtilizationPercent(0m, 100m).Should().BeNull();

    [Fact]
    public void UtilizationPercent_Computes() => BudgetAnalysis.UtilizationPercent(1000m, 883m).Should().Be(88.3m);

    [Theory]
    [InlineData(1000, 500, BudgetStatus.UnderBudget)]
    [InlineData(1000, 950, BudgetStatus.NearBudget)]
    [InlineData(1000, 1200, BudgetStatus.OverBudget)]
    [InlineData(0, 100, BudgetStatus.NoBudget)]
    public void Status_ClassifiesByUtilization(decimal budget, decimal actual, BudgetStatus expected) =>
        BudgetAnalysis.Status(budget, actual).Should().Be(expected);
}
