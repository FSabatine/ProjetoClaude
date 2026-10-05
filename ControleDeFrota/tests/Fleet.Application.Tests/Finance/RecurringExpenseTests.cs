using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Finance;

public class RecurringExpenseGenerationTests : IDisposable
{
    private readonly TestDb T = new();
    private Guid _insuranceCategoryId;

    private async Task<RecurringExpenseResponse> ArrangeTemplateAsync(DateOnly startDate, DateOnly? endDate = null, int dueDay = 10)
    {
        await Scenario.SignedInAsync(T, SystemRoles.FleetManager);
        var categories = await Services.ExpenseCategories(T).ListAsync(true, default);
        _insuranceCategoryId = categories.Single(c => c.Code == "INSURANCE").Id;
        return await Services.RecurringExpenses(T).CreateAsync(new RecurringExpenseRequest
        {
            Description = "Seguro da frota", ExpenseCategoryId = _insuranceCategoryId, Amount = 1200m,
            Frequency = ExpenseFrequency.Monthly, StartDate = startDate, EndDate = endDate, DueDayOfMonth = dueDay,
        }, default);
    }

    [Fact]
    public async Task Scan_GeneratesExpensesUpToHorizon()
    {
        T.Clock.UtcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await ArrangeTemplateAsync(new DateOnly(2026, 1, 10));

        var generated = await Services.RecurringExpenseScanner(T).ScanAsync(default);

        generated.Should().Be(2); // Jan 10 (today-ish) and Feb 10 (within the 30-day horizon)
        var expenses = await T.Db.Expenses.Where(e => e.IsRecurring).OrderBy(e => e.DueDate).ToListAsync();
        expenses.Should().HaveCount(2);
        expenses[0].DueDate.Should().Be(new DateOnly(2026, 1, 10));
        expenses[0].Amount.Should().Be(1200m);
        expenses[0].ExpenseCategoryId.Should().Be(_insuranceCategoryId);
    }

    [Fact]
    public async Task Scan_RunTwice_NeverDuplicates()
    {
        T.Clock.UtcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await ArrangeTemplateAsync(new DateOnly(2026, 1, 10));

        await Services.RecurringExpenseScanner(T).ScanAsync(default);
        var secondRunGenerated = await Services.RecurringExpenseScanner(T).ScanAsync(default);

        secondRunGenerated.Should().Be(0);
        (await T.Db.Expenses.CountAsync(e => e.IsRecurring)).Should().Be(2);
    }

    [Fact]
    public async Task Scan_PastEndDate_StopsGenerating()
    {
        T.Clock.UtcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        await ArrangeTemplateAsync(new DateOnly(2026, 1, 10), endDate: new DateOnly(2026, 1, 31));

        var generated = await Services.RecurringExpenseScanner(T).ScanAsync(default);

        generated.Should().Be(1);
    }

    [Fact]
    public async Task Scan_InactiveTemplate_GeneratesNothing()
    {
        T.Clock.UtcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var template = await ArrangeTemplateAsync(new DateOnly(2026, 1, 10));
        await Services.RecurringExpenses(T).UpdateAsync(template.Id, new RecurringExpenseRequest
        {
            Description = template.Description, ExpenseCategoryId = _insuranceCategoryId, Amount = template.Amount,
            Frequency = ExpenseFrequency.Monthly, StartDate = template.StartDate, DueDayOfMonth = template.DueDayOfMonth, IsActive = false,
        }, default);

        var generated = await Services.RecurringExpenseScanner(T).ScanAsync(default);

        generated.Should().Be(0);
    }

    [Fact]
    public async Task Delete_AfterGeneratingExpenses_Throws()
    {
        T.Clock.UtcNow = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var template = await ArrangeTemplateAsync(new DateOnly(2026, 1, 10));
        await Services.RecurringExpenseScanner(T).ScanAsync(default);

        var act = () => Services.RecurringExpenses(T).DeleteAsync(template.Id, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Create_SystemCategory_Throws()
    {
        await Scenario.SignedInAsync(T, SystemRoles.FleetManager);
        var categories = await Services.ExpenseCategories(T).ListAsync(true, default);
        var fuelCategoryId = categories.Single(c => c.Code == "FUEL").Id;
        var act = () => Services.RecurringExpenses(T).CreateAsync(new RecurringExpenseRequest
        {
            Description = "Combustível mensal", ExpenseCategoryId = fuelCategoryId, Amount = 100m,
            Frequency = ExpenseFrequency.Monthly, StartDate = T.Clock.Today,
        }, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    public void Dispose() => T.Dispose();
}
