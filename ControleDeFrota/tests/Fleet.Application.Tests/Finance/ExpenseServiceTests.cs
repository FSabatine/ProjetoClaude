using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;
using Fleet.Domain.Finance;
using FluentAssertions;

namespace Fleet.Application.Tests.Finance;

public abstract class ExpenseTestBase : IDisposable
{
    protected readonly TestDb T = new();
    protected Company Company = null!;
    protected Guid InsuranceCategoryId;
    protected Guid FuelCategoryId;
    protected Guid VehicleId;

    protected async Task ArrangeAsync(string role = SystemRoles.FleetManager)
    {
        Company = await Scenario.SignedInAsync(T, role);
        var categories = await Services.ExpenseCategories(T).ListAsync(includeInactive: true, default);
        InsuranceCategoryId = categories.Single(c => c.Code == "INSURANCE").Id;
        FuelCategoryId = categories.Single(c => c.Code == "FUEL").Id;
        VehicleId = (await Scenario.VehicleAsync(T)).Id;
        T.SignInAs(Company, role);
    }

    protected ExpenseRequest ValidRequest(decimal amount = 500m, DateOnly? expenseDate = null, DateOnly? dueDate = null) => new()
    {
        ExpenseCategoryId = InsuranceCategoryId, Description = "Seguro anual", ExpenseDate = expenseDate ?? T.Clock.Today,
        DueDate = dueDate, Amount = amount,
    };

    public void Dispose() => T.Dispose();
}

public class ExpenseService_CreateTests : ExpenseTestBase
{
    [Fact]
    public async Task Create_Valid_PersistsAsPendingWithoutDueDate()
    {
        await ArrangeAsync();
        var response = await Services.Expenses(T).CreateAsync(ValidRequest(), default);
        response.Status.Should().Be(PaymentStatus.Pending);
        response.Amount.Should().Be(500m);
    }

    [Fact]
    public async Task Create_WithFutureDueDate_IsScheduled()
    {
        await ArrangeAsync();
        var response = await Services.Expenses(T).CreateAsync(ValidRequest(dueDate: T.Clock.Today.AddDays(10)), default);
        response.Status.Should().Be(PaymentStatus.Scheduled);
    }

    [Fact]
    public async Task Create_SystemCategory_Throws()
    {
        await ArrangeAsync();
        var request = ValidRequest() with { ExpenseCategoryId = FuelCategoryId };
        var act = () => Services.Expenses(T).CreateAsync(request, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Create_UnknownVehicle_ThrowsNotFound()
    {
        await ArrangeAsync();
        var request = ValidRequest() with { VehicleId = Guid.NewGuid() };
        var act = () => Services.Expenses(T).CreateAsync(request, default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_SecondMatchingExpense_IsFlaggedAsDuplicateSuspect()
    {
        await ArrangeAsync();
        var request = ValidRequest() with { VehicleId = VehicleId };
        await Services.Expenses(T).CreateAsync(request, default);
        var second = await Services.Expenses(T).CreateAsync(request, default);
        second.IsDuplicateSuspect.Should().BeTrue();
    }
}

public class ExpenseService_PaymentAndCancelTests : ExpenseTestBase
{
    [Fact]
    public async Task RegisterPayment_FullAmount_BecomesPaid()
    {
        await ArrangeAsync();
        var expense = await Services.Expenses(T).CreateAsync(ValidRequest(100m), default);
        var paid = await Services.Expenses(T).RegisterPaymentAsync(
            expense.Id, new ExpensePaymentRequest { PaidAmount = 100m, PaymentDate = T.Clock.Today }, default);
        paid.Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public async Task RegisterPayment_PartialAmount_BecomesPartiallyPaid()
    {
        await ArrangeAsync();
        var expense = await Services.Expenses(T).CreateAsync(ValidRequest(100m), default);
        var paid = await Services.Expenses(T).RegisterPaymentAsync(
            expense.Id, new ExpensePaymentRequest { PaidAmount = 40m, PaymentDate = T.Clock.Today }, default);
        paid.Status.Should().Be(PaymentStatus.PartiallyPaid);
    }

    [Fact]
    public async Task RegisterPayment_AboveAmount_Throws()
    {
        await ArrangeAsync();
        var expense = await Services.Expenses(T).CreateAsync(ValidRequest(100m), default);
        var act = () => Services.Expenses(T).RegisterPaymentAsync(
            expense.Id, new ExpensePaymentRequest { PaidAmount = 150m, PaymentDate = T.Clock.Today }, default);
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Cancel_SetsStatusCancelled()
    {
        await ArrangeAsync();
        var expense = await Services.Expenses(T).CreateAsync(ValidRequest(), default);
        var cancelled = await Services.Expenses(T).CancelAsync(expense.Id, new ExpenseCancelRequest { Reason = "Lançamento em duplicidade" }, default);
        cancelled.Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_Twice_Throws()
    {
        await ArrangeAsync();
        var expense = await Services.Expenses(T).CreateAsync(ValidRequest(), default);
        await Services.Expenses(T).CancelAsync(expense.Id, new ExpenseCancelRequest { Reason = "Motivo" }, default);
        var act = () => Services.Expenses(T).CancelAsync(expense.Id, new ExpenseCancelRequest { Reason = "Motivo" }, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Update_CancelledExpense_Throws()
    {
        await ArrangeAsync();
        var expense = await Services.Expenses(T).CreateAsync(ValidRequest(), default);
        await Services.Expenses(T).CancelAsync(expense.Id, new ExpenseCancelRequest { Reason = "Motivo" }, default);
        var act = () => Services.Expenses(T).UpdateAsync(expense.Id, ValidRequest(200m), default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Update_AmountBelowPaidAmount_Throws()
    {
        await ArrangeAsync();
        var expense = await Services.Expenses(T).CreateAsync(ValidRequest(100m), default);
        await Services.Expenses(T).RegisterPaymentAsync(expense.Id, new ExpensePaymentRequest { PaidAmount = 80m, PaymentDate = T.Clock.Today }, default);
        var act = () => Services.Expenses(T).UpdateAsync(expense.Id, ValidRequest(50m), default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }
}

public class ExpenseService_PermissionAndTenantTests : ExpenseTestBase
{
    [Fact]
    public async Task List_WithoutViewCosts_HidesAmountForOthersRecords()
    {
        await ArrangeAsync();
        await Services.Expenses(T).CreateAsync(ValidRequest(), default);

        // A different user, same company, role without finance.viewcosts (Viewer).
        T.SignInAs(Company, SystemRoles.Viewer, Guid.NewGuid());
        var page = await Services.Expenses(T).ListAsync(new ExpenseListRequest(), default);
        page.Items.Single().Amount.Should().BeNull();
    }

    [Fact]
    public async Task Get_RecordFromAnotherCompany_ThrowsNotFound()
    {
        await ArrangeAsync();
        var expense = await Services.Expenses(T).CreateAsync(ValidRequest(), default);

        var otherCompany = await T.AddCompanyAsync("22333444000199", "Outra Empresa");
        T.SignInAs(otherCompany, SystemRoles.FleetManager);
        var act = () => Services.Expenses(T).GetAsync(expense.Id, default);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
