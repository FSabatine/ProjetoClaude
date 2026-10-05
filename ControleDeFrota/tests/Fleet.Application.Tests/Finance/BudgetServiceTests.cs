using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Finance;
using FluentAssertions;

namespace Fleet.Application.Tests.Finance;

public class BudgetServiceTests : IDisposable
{
    private readonly TestDb T = new();
    private Guid _tollsCategoryId;

    private async Task ArrangeAsync()
    {
        await Scenario.SignedInAsync(T, SystemRoles.FleetManager);
        var categories = await Services.ExpenseCategories(T).ListAsync(true, default);
        _tollsCategoryId = categories.Single(c => c.Code == "TOLLS").Id;
    }

    [Fact]
    public async Task Create_DuplicateScope_Throws()
    {
        await ArrangeAsync();
        var request = new BudgetRequest { Year = 2026, Month = 10, ExpenseCategoryId = _tollsCategoryId, Amount = 1000m };
        await Services.Budgets(T).CreateAsync(request, default);
        var act = () => Services.Budgets(T).CreateAsync(request, default);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task VsActual_UnderBudget_ComputesRemainingAndUtilization()
    {
        await ArrangeAsync();
        await Services.Budgets(T).CreateAsync(
            new BudgetRequest { Year = 2026, Month = 10, ExpenseCategoryId = _tollsCategoryId, Amount = 1000m }, default);
        await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = _tollsCategoryId, Description = "Pedágio", ExpenseDate = new DateOnly(2026, 10, 5), Amount = 400m,
        }, default);

        var rows = await Services.Budgets(T).ListVsActualAsync(2026, 10, default);

        var row = rows.Single();
        row.Actual.Should().Be(400m);
        row.Remaining.Should().Be(600m);
        row.UtilizationPercent.Should().Be(40m);
        row.Status.Should().Be(BudgetStatus.UnderBudget);
    }

    [Fact]
    public async Task VsActual_OverBudget_FlagsOverBudget()
    {
        await ArrangeAsync();
        await Services.Budgets(T).CreateAsync(
            new BudgetRequest { Year = 2026, Month = 10, ExpenseCategoryId = _tollsCategoryId, Amount = 100m }, default);
        await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = _tollsCategoryId, Description = "Pedágio", ExpenseDate = new DateOnly(2026, 10, 5), Amount = 150m,
        }, default);

        var rows = await Services.Budgets(T).ListVsActualAsync(2026, 10, default);

        rows.Single().Status.Should().Be(BudgetStatus.OverBudget);
    }

    [Fact]
    public async Task VsActual_CancelledExpense_DoesNotCountAsActual()
    {
        await ArrangeAsync();
        await Services.Budgets(T).CreateAsync(
            new BudgetRequest { Year = 2026, Month = 10, ExpenseCategoryId = _tollsCategoryId, Amount = 1000m }, default);
        var expense = await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = _tollsCategoryId, Description = "Pedágio", ExpenseDate = new DateOnly(2026, 10, 5), Amount = 400m,
        }, default);
        await Services.Expenses(T).CancelAsync(expense.Id, new ExpenseCancelRequest { Reason = "Duplicado" }, default);

        var rows = await Services.Budgets(T).ListVsActualAsync(2026, 10, default);

        rows.Single().Actual.Should().Be(0m);
    }

    [Fact]
    public async Task VsActual_WithoutFinanceViewCosts_ReturnsEmpty()
    {
        await ArrangeAsync();
        await Services.Budgets(T).CreateAsync(
            new BudgetRequest { Year = 2026, Month = 10, ExpenseCategoryId = _tollsCategoryId, Amount = 1000m }, default);
        T.CurrentUser.PermissionSet = [Permissions.Finance.View];

        var rows = await Services.Budgets(T).ListVsActualAsync(2026, 10, default);

        rows.Should().BeEmpty();
    }

    public void Dispose() => T.Dispose();
}
