using Fleet.Application.Analytics;
using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Application.Occurrences;
using Fleet.Application.Operations;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Operations;
using FluentAssertions;

namespace Fleet.Application.Tests.Intelligence;

public abstract class AnalyticsTestBase : IDisposable
{
    protected readonly TestDb T = new();
    protected Company Company = null!;
    protected Guid InsuranceCategoryId;

    protected async Task ArrangeAsync(string role = SystemRoles.Administrator)
    {
        Company = await Scenario.SignedInAsync(T, SystemRoles.Administrator);
        var categories = await Services.ExpenseCategories(T).ListAsync(includeInactive: true, default);
        InsuranceCategoryId = categories.Single(c => c.Code == "INSURANCE").Id;
        T.SignInAs(Company, role);
    }

    protected void As(string role) => T.SignInAs(Company, role);

    protected async Task ExpenseAsync(Guid? vehicleId, decimal amount, DateOnly date)
    {
        var role = T.CurrentUser.PermissionSet;
        As(SystemRoles.Administrator);
        await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = InsuranceCategoryId, Description = "Seguro", ExpenseDate = date, Amount = amount, VehicleId = vehicleId,
        }, default);
        T.CurrentUser.PermissionSet = role;
    }

    protected async Task ReadingAsync(Guid vehicleId, int km, DateTime readAt)
    {
        T.Db.OdometerReadings.Add(new OdometerReading
        {
            VehicleId = vehicleId, OdometerKm = km, ReadAt = readAt, Source = OdometerReadingSource.Manual, Status = OdometerReadingStatus.Valid,
        });
        await T.Db.SaveChangesAsync();
    }

    protected async Task CompletedCorrectiveAsync(Guid vehicleId, int sequence, string problem, DateTime completedAt, int downtimeMinutes = 120)
    {
        T.Db.WorkOrders.Add(new WorkOrder
        {
            VehicleId = vehicleId, Sequence = sequence, Type = MaintenanceType.Corrective, Status = WorkOrderStatus.Completed,
            OpenedAt = completedAt.AddDays(-1), CompletedAt = completedAt, Description = problem, DowntimeMinutes = downtimeMinutes,
            Items = [new WorkOrderItem { Description = problem }],
        });
        await T.Db.SaveChangesAsync();
    }

    public void Dispose() => T.Dispose();
}

public class VehicleMetricsTests : AnalyticsTestBase
{
    [Fact]
    public async Task FleetPerformance_CombinesKmCostsAndMaintenance_PerVehicle()
    {
        await ArrangeAsync();
        var start = T.Clock.UtcNow;
        var vehicle = await Scenario.VehicleAsync(T); // registration reading: 1.000 km now
        T.Clock.UtcNow = start.AddDays(10);
        await ReadingAsync(vehicle.Id, 1_800, start.AddDays(5));
        await ExpenseAsync(vehicle.Id, 400m, T.Clock.Today.AddDays(-2));
        await CompletedCorrectiveAsync(vehicle.Id, 1, "Troca de embreagem", start.AddDays(6), downtimeMinutes: 180);

        var page = await Services.FleetReports(T).FleetPerformanceAsync(
            new FleetPerformanceRequest { From = DateOnly.FromDateTime(start.AddDays(1)), To = T.Clock.Today }, default);

        var row = page.Items.Single();
        row.KmDriven.Should().Be(800);
        row.KmReliable.Should().BeTrue();
        row.TotalCost.Should().Be(400m);
        row.CostPerKm.Should().Be(0.5m);
        row.WorkOrdersCompleted.Should().Be(1);
        row.DowntimeHours.Should().Be(3m);
    }

    [Fact]
    public async Task FleetPerformance_WithoutCostPermission_HidesMoneyInsteadOfZero()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        await ExpenseAsync(vehicle.Id, 400m, T.Clock.Today);

        As(SystemRoles.Viewer);
        var row = (await Services.FleetReports(T).FleetPerformanceAsync(new FleetPerformanceRequest(), default)).Items.Single();

        row.TotalCost.Should().BeNull();
        row.FuelCost.Should().BeNull();
        row.CostPerKm.Should().BeNull();
    }

    [Fact]
    public async Task FleetPerformance_NoReadingBeforeThePeriod_KmIsNotReliableAndCostPerKmWithheld()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        await ReadingAsync(vehicle.Id, 3_000, T.Clock.UtcNow.AddHours(1));
        await ExpenseAsync(vehicle.Id, 400m, T.Clock.Today);

        var row = (await Services.FleetReports(T).FleetPerformanceAsync(
            new FleetPerformanceRequest { From = T.Clock.Today, To = T.Clock.Today }, default)).Items.Single();

        row.KmReliable.Should().BeFalse();
        row.CostPerKm.Should().BeNull();
    }

    [Fact]
    public async Task Compare_AddsFleetAndTypeAverages_AndRejectsTooManyOrForeignVehicles()
    {
        await ArrangeAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++) ids.Add((await Scenario.VehicleAsync(T, i)).Id);
        await ExpenseAsync(ids[0], 900m, T.Clock.Today);
        await ExpenseAsync(ids[1], 300m, T.Clock.Today);

        var result = await Services.FleetReports(T).CompareAsync([ids[0], ids[1]], null, null, default);
        var tooMany = () => Services.FleetReports(T).CompareAsync(Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToList(), null, null, default);
        var foreign = () => Services.FleetReports(T).CompareAsync([ids[0], Guid.NewGuid()], null, null, default);

        result.Vehicles.Select(v => v.VehicleId).Should().Equal(ids[0], ids[1]);
        var fleet = result.Benchmarks.Single(b => b.Label == "Média da frota");
        fleet.VehicleCount.Should().Be(3);
        fleet.Average.TotalCost.Should().Be(400m);
        await tooMany.Should().ThrowAsync<BusinessRuleException>();
        await foreign.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RecurringProblems_SameProblemTwiceOnTheSameVehicle_IsReportedOnce()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        var other = await Scenario.VehicleAsync(T, 1);
        await CompletedCorrectiveAsync(vehicle.Id, 1, "Troca de pastilhas", T.Clock.UtcNow.AddDays(-20));
        await CompletedCorrectiveAsync(vehicle.Id, 2, "troca  de PASTILHAS.", T.Clock.UtcNow.AddDays(-2));
        await CompletedCorrectiveAsync(other.Id, 3, "Troca de pastilhas", T.Clock.UtcNow.AddDays(-2));

        var rows = await Services.FleetReports(T).RecurringProblemsAsync(new PeriodRequest(null, null), default);

        var row = rows.Single();
        row.VehicleId.Should().Be(vehicle.Id);
        row.Occurrences.Should().Be(2);
        row.WorkOrderNumbers.Should().Equal(1, 2);
    }
}

public class VehicleHealthTests : AnalyticsTestBase
{
    [Fact]
    public async Task Health_CriticalOpenOccurrence_SubtractsAndExplains()
    {
        await ArrangeAsync(SystemRoles.FleetManager);
        var vehicle = await Scenario.VehicleAsync(T);
        await Services.Occurrences(T).CreateAsync(new OccurrenceRequest
        {
            VehicleId = vehicle.Id, Type = OccurrenceType.MechanicalIssue, Severity = OccurrenceSeverity.Critical,
            OccurredAt = T.Clock.UtcNow.AddHours(-1), Description = "Vazamento de óleo",
        }, default);

        var health = await Services.VehicleHealth(T).GetAsync(vehicle.Id, default);

        var factor = health.Factors.Single(f => f.Area == HealthFactorArea.Occurrences);
        factor.Status.Should().Be(HealthFactorStatus.Critical);
        factor.Explanation.Should().Contain("crítica");
        health.Score.Should().Be(100 - VehicleHealthPolicy.CriticalPenalty);
        health.IsPartial.Should().BeFalse();
    }

    [Fact]
    public async Task Health_AreasWithoutPermission_AreNotVisibleAndDoNotCount()
    {
        await ArrangeAsync(SystemRoles.Maintenance);
        var vehicle = await Scenario.VehicleAsync(T);

        var health = await Services.VehicleHealth(T).GetAsync(vehicle.Id, default);

        health.Factors.Single(f => f.Area == HealthFactorArea.Documents).Status.Should().Be(HealthFactorStatus.NotVisible);
        health.Factors.Single(f => f.Area == HealthFactorArea.Costs).Status.Should().Be(HealthFactorStatus.NotVisible);
        health.IsPartial.Should().BeTrue();
    }

    [Fact]
    public async Task Health_VehicleOfAnotherCompany_IsNotFound()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        var other = await T.AddCompanyAsync("11444777000161", "Outra");
        T.SignInAs(other, SystemRoles.Administrator);

        var act = () => Services.VehicleHealth(T).GetAsync(vehicle.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class InsightAndSearchTests : AnalyticsTestBase
{
    [Fact]
    public async Task Insights_CostIncrease_ExplainsWithBothPeriodsAndTheVehicle()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        await ExpenseAsync(vehicle.Id, 1_000m, T.Clock.Today.AddDays(-45));
        await ExpenseAsync(vehicle.Id, 2_000m, T.Clock.Today.AddDays(-5));

        var insight = (await Services.Insights(T).GetAsync(default)).Single(i => i.Key == "cost-other");

        insight.Title.Should().Be("Custos de outras despesas aumentaram 100%");
        insight.Explanation.Should().Contain("R$ 2.000,00").And.Contain("R$ 1.000,00").And.Contain("ABC1D23");
        insight.Sentiment.Should().Be(InsightSentiment.Negative);
    }

    [Fact]
    public async Task Insights_WithoutAllCostPermissions_NeverQuoteMoney()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        await ExpenseAsync(vehicle.Id, 1_000m, T.Clock.Today.AddDays(-45));
        await ExpenseAsync(vehicle.Id, 2_000m, T.Clock.Today.AddDays(-5));

        As(SystemRoles.Operations);
        var insights = await Services.Insights(T).GetAsync(default);

        insights.Should().NotContain(i => (i.Title + i.Explanation + i.Evidence).Contains("R$"));
    }

    [Fact]
    public async Task Search_FindsByPlateAndRespectsModulePermissions()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        await Scenario.DriverAsync(T);
        await ExpenseAsync(vehicle.Id, 100m, T.Clock.Today);

        var admin = await Services.Search(T).SearchAsync("ABC-1D23", default);
        As(SystemRoles.Maintenance);
        var maintenance = await Services.Search(T).SearchAsync("Motorista", default);
        var maintenanceExpense = await Services.Search(T).SearchAsync("Seguro", default);

        admin.Results.Should().Contain(r => r.Type == SearchResultType.Vehicle && r.Link == $"/veiculos/{vehicle.Id}");
        admin.Results.Should().Contain(r => r.Type == SearchResultType.Expense);
        maintenance.Results.Should().NotContain(r => r.Type == SearchResultType.Driver);
        maintenanceExpense.Results.Should().NotContain(r => r.Type == SearchResultType.Expense);
    }

    [Fact]
    public async Task Search_OtherCompanyRecords_AreNeverReturned()
    {
        await ArrangeAsync();
        await Scenario.VehicleAsync(T);
        var other = await T.AddCompanyAsync("11444777000161", "Outra");
        T.SignInAs(other, SystemRoles.Administrator);

        (await Services.Search(T).SearchAsync("ABC1D23", default)).Results.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_TooShort_ReturnsNothing()
    {
        await ArrangeAsync();
        (await Services.Search(T).SearchAsync("a", default)).Results.Should().BeEmpty();
    }
}

public class TimelineVisibilityTests : AnalyticsTestBase
{
    [Fact]
    public async Task VehicleHistory_HidesEventsOfModulesTheReaderCannotSee()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        await ExpenseAsync(vehicle.Id, 100m, T.Clock.Today);

        var admin = await Services.History(T).ForVehicleAsync(vehicle.Id, new HistoryRequest(), default);
        As(SystemRoles.Operations);
        var operations = await Services.History(T).ForVehicleAsync(vehicle.Id, new HistoryRequest(), default);

        admin.Items.Should().Contain(e => e.Type == OperationalEventType.ExpenseCreated);
        operations.Items.Should().NotContain(e => e.Type == OperationalEventType.ExpenseCreated);
    }

    [Fact]
    public async Task VehicleHistory_VehicleDocumentEvents_VisibleWithDocumentsViewEvenWithoutDriversView()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        Services.Events(T).Record(OperationalEventType.DocumentCreated, new Fleet.Application.Operations.EventSubject("Document", Guid.NewGuid(), VehicleId: vehicle.Id), "CRLV cadastrado.");
        await T.Db.SaveChangesAsync();

        T.CurrentUser.PermissionSet = [Permissions.Vehicles.View, Permissions.Documents.View];
        var withDocuments = await Services.History(T).ForVehicleAsync(vehicle.Id, new HistoryRequest(), default);
        T.CurrentUser.PermissionSet = [Permissions.Vehicles.View];
        var withoutDocuments = await Services.History(T).ForVehicleAsync(vehicle.Id, new HistoryRequest(), default);

        withDocuments.Items.Should().Contain(e => e.Type == OperationalEventType.DocumentCreated);
        withoutDocuments.Items.Should().NotContain(e => e.Type == OperationalEventType.DocumentCreated);
    }

    [Fact]
    public async Task VehicleHistory_CategoryFilter_KeepsOnlyThatArea()
    {
        await ArrangeAsync();
        var vehicle = await Scenario.VehicleAsync(T);
        await ExpenseAsync(vehicle.Id, 100m, T.Clock.Today);

        var finance = await Services.History(T).ForVehicleAsync(vehicle.Id, new HistoryRequest { Category = FleetAlertCategory.Finance }, default);

        finance.Items.Should().NotBeEmpty().And.OnlyContain(e => e.Category == FleetAlertCategory.Finance);
    }
}
