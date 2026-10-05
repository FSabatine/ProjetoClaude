using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Application.Intelligence;
using Fleet.Application.Operations;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Operations;
using Fleet.Domain.Users;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Intelligence;

/// <summary>
/// The engine runs as the system (every permission of the company); here that is the Administrator role.
/// Readers then sign in with their own role — what they see is filtered by the alert audience.
/// </summary>
public abstract class AutomationTestBase : IDisposable
{
    protected readonly TestDb T = new();
    protected Company Company = null!;
    protected Guid InsuranceCategoryId;

    protected async Task ArrangeAsync()
    {
        Company = await Scenario.SignedInAsync(T, SystemRoles.Administrator);
        var categories = await Services.ExpenseCategories(T).ListAsync(includeInactive: true, default);
        InsuranceCategoryId = categories.Single(c => c.Code == "INSURANCE").Id;
    }

    protected void AsSystem() => T.SignInAs(Company, SystemRoles.Administrator);

    protected Task<AutomationRunResult> RunAsync()
    {
        AsSystem();
        return Services.Automation(T).RunAsync(AutomationRunSource.Manual, default);
    }

    protected async Task<ExpenseResponse> OverdueExpenseAsync(Guid? vehicleId = null, decimal amount = 500m)
    {
        AsSystem();
        return await Services.Expenses(T).CreateAsync(new ExpenseRequest
        {
            ExpenseCategoryId = InsuranceCategoryId, Description = "Seguro", ExpenseDate = T.Clock.Today.AddDays(-20),
            DueDate = T.Clock.Today.AddDays(-5), Amount = amount, VehicleId = vehicleId,
        }, default);
    }

    protected async Task<AutomationRule> RuleAsync(AutomationTrigger trigger)
    {
        AsSystem();
        await Services.AutomationRules(T).EnsureDefaultsAsync(default);
        return await T.NewContext().AutomationRules.SingleAsync(r => r.Trigger == trigger && r.IsSystemDefault);
    }

    protected Task<List<FleetAlert>> AlertsAsync() => T.NewContext().FleetAlerts.OrderBy(a => a.DetectedAt).ToListAsync();

    public void Dispose() => T.Dispose();
}

public class AutomationEngine_ScheduledRulesTests : AutomationTestBase
{
    [Fact]
    public async Task Run_FirstTime_CreatesDefaultRulesOnce()
    {
        await ArrangeAsync();
        await RunAsync();
        await RunAsync();

        var rules = await T.NewContext().AutomationRules.ToListAsync();
        rules.Should().HaveCount(AutomationTriggerCatalog.DefaultRules().Count());
        rules.Should().OnlyContain(r => r.IsSystemDefault && r.IsActive);
    }

    [Fact]
    public async Task Run_OverdueExpense_CreatesOneAlertWithExplanationEvidenceAndAction()
    {
        await ArrangeAsync();
        var expense = await OverdueExpenseAsync();

        var result = await RunAsync();

        result.AlertsCreated.Should().Be(1);
        var alert = (await AlertsAsync()).Single();
        alert.Trigger.Should().Be(AutomationTrigger.ExpenseOverdue);
        alert.EntityId.Should().Be(expense.Id);
        alert.Status.Should().Be(FleetAlertStatus.New);
        alert.Explanation.Should().NotBeEmpty();
        alert.Evidence.Should().Contain("5 dias de atraso");
        alert.RecommendedAction.Should().NotBeEmpty();
        alert.Audience.Should().Be(AlertAudience.FinanceView);
        // finance.view audience: no money in any text.
        (alert.Title + alert.Explanation + alert.Evidence).Should().NotContain("R$");
    }

    [Fact]
    public async Task Run_ConditionStillTrue_RefreshesTheSameAlertInsteadOfDuplicating()
    {
        await ArrangeAsync();
        await OverdueExpenseAsync();
        await RunAsync();
        T.Clock.UtcNow = T.Clock.UtcNow.AddHours(1);

        var second = await RunAsync();

        second.AlertsCreated.Should().Be(0);
        var alert = (await AlertsAsync()).Single();
        alert.DetectionCount.Should().Be(2);
        alert.LastDetectedAt.Should().Be(T.Clock.UtcNow);
    }

    [Fact]
    public async Task Run_ConditionGone_ClosesTheAlertAutomatically()
    {
        await ArrangeAsync();
        var expense = await OverdueExpenseAsync();
        await RunAsync();
        await Services.Expenses(T).RegisterPaymentAsync(expense.Id, new ExpensePaymentRequest { PaidAmount = 500m, PaymentDate = T.Clock.Today }, default);

        var result = await RunAsync();

        result.AlertsResolved.Should().Be(1);
        var alert = (await AlertsAsync()).Single();
        alert.Status.Should().Be(FleetAlertStatus.Resolved);
        alert.AutoResolved.Should().BeTrue();
        alert.ClosingNotes.Should().Be(AutomationEngine.AutoResolvedNote);
    }

    [Fact]
    public async Task Run_AfterDismiss_DoesNotRaiseTheSameFindingWithinSuppressionWindow()
    {
        await ArrangeAsync();
        await OverdueExpenseAsync();
        await RunAsync();
        var alert = (await AlertsAsync()).Single();
        await Services.Alerts(T).ChangeStatusAsync(alert.Id, new FleetAlertStatusRequest { Status = FleetAlertStatus.Dismissed, Notes = "Negociado com o fornecedor" }, default);

        var result = await RunAsync();

        result.AlertsCreated.Should().Be(0);
        (await AlertsAsync()).Should().ContainSingle(a => a.Status == FleetAlertStatus.Dismissed);
    }

    [Fact]
    public async Task Run_AfterManualResolveWhileStillTrue_RaisesAgainCountingTheRecurrence()
    {
        await ArrangeAsync();
        await OverdueExpenseAsync();
        await RunAsync();
        var first = (await AlertsAsync()).Single();
        await Services.Alerts(T).ChangeStatusAsync(first.Id, new FleetAlertStatusRequest { Status = FleetAlertStatus.Resolved }, default);

        await RunAsync();

        var alerts = await AlertsAsync();
        alerts.Should().HaveCount(2);
        var again = alerts.Single(a => a.Id != first.Id);
        again.RecurrenceCount.Should().Be(1);
        again.Priority.Should().BeGreaterThan(first.Priority);
    }

    [Fact]
    public async Task Run_DeactivatedRule_DoesNotEvaluateAndClosesItsOpenAlerts()
    {
        await ArrangeAsync();
        await OverdueExpenseAsync();
        await RunAsync();
        var rule = await RuleAsync(AutomationTrigger.ExpenseOverdue);
        var response = await Services.AutomationRules(T).GetAsync(rule.Id, default);

        await Services.AutomationRules(T).UpdateAsync(rule.Id, ToRequest(response) with { IsActive = false }, default);
        var result = await RunAsync();

        result.AlertsCreated.Should().Be(0);
        (await AlertsAsync()).Single().Status.Should().Be(FleetAlertStatus.Dismissed);
    }

    [Fact]
    public async Task Run_VehicleCostAboveTypeAverage_AlertsOnlyThatVehicleAndExplainsTheMainFactor()
    {
        await ArrangeAsync();
        var vehicles = new List<Guid>();
        for (var i = 0; i < 4; i++) vehicles.Add((await Scenario.VehicleAsync(T, i)).Id);
        await OverdueExpenseAsync(vehicles[0], 3000m);
        foreach (var peer in vehicles.Skip(1)) await OverdueExpenseAsync(peer, 1000m);

        await RunAsync();

        var costAlert = (await AlertsAsync()).Single(a => a.Trigger == AutomationTrigger.VehicleCostAboveAverage);
        costAlert.VehicleId.Should().Be(vehicles[0]);
        costAlert.Audience.Should().Be(AlertAudience.FleetCosts);
        costAlert.Title.Should().Contain("200% acima da média do tipo");
        costAlert.Explanation.Should().Contain("outras despesas");
        costAlert.Evidence.Should().Contain("R$ 3.000,00");
    }

    [Fact]
    public async Task Run_RuleWithNotification_NotifiesOnlyUsersWhoCanSeeTheAlert()
    {
        await ArrangeAsync();
        var finance = await T.AddUserAsync(Company, "fin@frota.local", SystemRoles.Finance);
        var operations = await T.AddUserAsync(Company, "ops@frota.local", SystemRoles.Operations);
        var rule = await RuleAsync(AutomationTrigger.ExpenseOverdue);
        var response = await Services.AutomationRules(T).GetAsync(rule.Id, default);
        await Services.AutomationRules(T).UpdateAsync(rule.Id, ToRequest(response) with { NotifyMode = AutomationNotifyMode.EveryoneWhoCanSee }, default);
        await OverdueExpenseAsync();

        await RunAsync();

        var notifications = await T.NewContext().UserNotifications.ToListAsync();
        notifications.Should().Contain(n => n.UserId == finance.Id);
        notifications.Should().NotContain(n => n.UserId == operations.Id);
    }

    [Fact]
    public async Task Run_CostAlertNotification_NeverQuotesMoney()
    {
        await ArrangeAsync();
        var manager = await T.AddUserAsync(Company, "gestor@frota.local", SystemRoles.FleetManager);
        var rule = await RuleAsync(AutomationTrigger.VehicleCostAboveAverage);
        var response = await Services.AutomationRules(T).GetAsync(rule.Id, default);
        await Services.AutomationRules(T).UpdateAsync(rule.Id, ToRequest(response) with { NotifyMode = AutomationNotifyMode.EveryoneWhoCanSee }, default);
        var vehicles = new List<Guid>();
        for (var i = 0; i < 4; i++) vehicles.Add((await Scenario.VehicleAsync(T, i)).Id);
        await OverdueExpenseAsync(vehicles[0], 5000m);
        foreach (var peer in vehicles.Skip(1)) await OverdueExpenseAsync(peer, 1000m);

        await RunAsync();

        var notification = await T.NewContext().UserNotifications.SingleAsync(n => n.UserId == manager.Id && n.Title.Contains("custo"));
        (notification.Title + notification.Message).Should().NotContain("R$");
    }

    [Fact]
    public async Task Run_ManyNewAlertsOfOneRule_SendsOneSummaryNotificationPerUser()
    {
        await ArrangeAsync();
        var finance = await T.AddUserAsync(Company, "fin@frota.local", SystemRoles.Finance);
        var rule = await RuleAsync(AutomationTrigger.ExpenseOverdue);
        var response = await Services.AutomationRules(T).GetAsync(rule.Id, default);
        await Services.AutomationRules(T).UpdateAsync(rule.Id, ToRequest(response) with { NotifyMode = AutomationNotifyMode.EveryoneWhoCanSee }, default);
        for (var i = 0; i < AutomationEngine.MaxIndividualNotifications + 2; i++) await OverdueExpenseAsync(amount: 100m + i);

        await RunAsync();

        var notifications = await T.NewContext().UserNotifications.Where(n => n.UserId == finance.Id).ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].Title.Should().StartWith($"{AutomationEngine.MaxIndividualNotifications + 2} novos alertas");
    }

    internal static AutomationRuleRequest ToRequest(AutomationRuleResponse r) => new()
    {
        Name = r.Name, Description = r.Description, Trigger = r.Trigger, EventType = r.EventType, Threshold = r.Threshold,
        LookbackDays = r.LookbackDays, Severity = r.Severity, CreateAlert = r.CreateAlert, NotifyMode = r.NotifyMode,
        NotifyUserId = r.NotifyUserId, IsActive = r.IsActive,
    };
}

public class AutomationEngine_EventRulesTests : AutomationTestBase
{
    private async Task<AutomationRuleResponse> OccurrenceRuleAsync(bool createAlert)
    {
        AsSystem();
        return await Services.AutomationRules(T).CreateAsync(new AutomationRuleRequest
        {
            Name = "Ocorrência registrada", Trigger = AutomationTrigger.OperationalEvent, EventType = OperationalEventType.OccurrenceCreated,
            CreateAlert = createAlert, NotifyMode = AutomationNotifyMode.EveryoneWhoCanSee, Severity = FleetAlertSeverity.Critical,
        }, default);
    }

    private async Task RecordOccurrenceEventAsync(Guid vehicleId)
    {
        AsSystem();
        Services.Events(T).Record(OperationalEventType.OccurrenceCreated, new EventSubject("Occurrence", Guid.NewGuid(), VehicleId: vehicleId),
            "Ocorrência registrada: pneu furado na BR-277.");
        await T.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Run_EventAfterRuleCreated_NotifiesAndCreatesAlertAndMarksEventPublished()
    {
        await ArrangeAsync();
        var manager = await T.AddUserAsync(Company, "gestor@frota.local", SystemRoles.FleetManager);
        var vehicle = await Scenario.VehicleAsync(T);
        await OccurrenceRuleAsync(createAlert: true);
        T.Clock.UtcNow = T.Clock.UtcNow.AddMinutes(5);
        await RecordOccurrenceEventAsync(vehicle.Id);

        await RunAsync();

        var alert = (await AlertsAsync()).Single(a => a.Trigger == AutomationTrigger.OperationalEvent);
        alert.Explanation.Should().Contain("pneu furado");
        alert.Audience.Should().Be(AlertAudience.Occurrences);
        var notification = await T.NewContext().UserNotifications.SingleAsync(n => n.UserId == manager.Id);
        notification.Link.Should().Be($"/alertas/{alert.Id}");
        (await T.NewContext().OperationalEvents.AnyAsync(e => e.PublishedAt == null)).Should().BeFalse();
    }

    [Fact]
    public async Task Run_EventBeforeRuleExisted_IsOnlyMarkedPublished()
    {
        await ArrangeAsync();
        await T.AddUserAsync(Company, "gestor@frota.local", SystemRoles.FleetManager);
        var vehicle = await Scenario.VehicleAsync(T);
        await RecordOccurrenceEventAsync(vehicle.Id);
        T.Clock.UtcNow = T.Clock.UtcNow.AddMinutes(5);
        await OccurrenceRuleAsync(createAlert: true);

        await RunAsync();

        (await T.NewContext().UserNotifications.CountAsync()).Should().Be(0);
        (await AlertsAsync()).Should().NotContain(a => a.Trigger == AutomationTrigger.OperationalEvent);
        (await T.NewContext().OperationalEvents.AnyAsync(e => e.PublishedAt == null)).Should().BeFalse();
    }

    [Fact]
    public async Task Run_EventRuleWithoutAlert_NotificationLinksToTheRecord()
    {
        await ArrangeAsync();
        var manager = await T.AddUserAsync(Company, "gestor@frota.local", SystemRoles.FleetManager);
        var vehicle = await Scenario.VehicleAsync(T);
        await OccurrenceRuleAsync(createAlert: false);
        T.Clock.UtcNow = T.Clock.UtcNow.AddMinutes(5);
        await RecordOccurrenceEventAsync(vehicle.Id);

        await RunAsync();

        var notification = await T.NewContext().UserNotifications.SingleAsync(n => n.UserId == manager.Id);
        notification.Link.Should().StartWith("/ocorrencias/");
        notification.FleetAlertId.Should().BeNull();
    }
}

public class FleetAlertService_VisibilityTests : AutomationTestBase
{
    [Fact]
    public async Task List_UserWithoutTheAudiencePermission_DoesNotSeeTheAlert()
    {
        await ArrangeAsync();
        await OverdueExpenseAsync();
        await RunAsync();
        var alertId = (await AlertsAsync()).Single().Id;

        T.SignInAs(Company, SystemRoles.Operations);
        var list = await Services.Alerts(T).ListAsync(new FleetAlertListRequest(), default);
        var act = () => Services.Alerts(T).GetAsync(alertId, default);

        list.TotalCount.Should().Be(0);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task List_FleetCostsAlert_RequiresEveryCostPermission()
    {
        AlertAudiences.VisibleTo(SystemRoles.All.Single(r => r.Key == SystemRoles.Finance).Permissions.ToHashSet())
            .Should().Contain(AlertAudience.FleetCosts);
        var withoutTires = SystemRoles.All.Single(r => r.Key == SystemRoles.Finance).Permissions
            .Where(p => p != Permissions.Tires.ViewCosts).ToHashSet();
        AlertAudiences.VisibleTo(withoutTires).Should().NotContain(AlertAudience.FleetCosts);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Get_AlertOfAnotherCompany_IsNotFound()
    {
        await ArrangeAsync();
        await OverdueExpenseAsync();
        await RunAsync();
        var alertId = (await AlertsAsync()).Single().Id;

        var other = await T.AddCompanyAsync("11444777000161", "Outra");
        T.SignInAs(other, SystemRoles.Administrator);
        var act = () => Services.Alerts(T).GetAsync(alertId, default);

        await act.Should().ThrowAsync<NotFoundException>();
        (await Services.Alerts(T).ListAsync(new FleetAlertListRequest(), default)).TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ChangeStatus_Dismiss_RequiresReason()
    {
        await ArrangeAsync();
        await OverdueExpenseAsync();
        await RunAsync();
        var alertId = (await AlertsAsync()).Single().Id;

        var act = () => Services.Alerts(T).ChangeStatusAsync(alertId, new FleetAlertStatusRequest { Status = FleetAlertStatus.Dismissed }, default);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ChangeStatus_InProgress_AssignsToCurrentUserAndClosedCannotReopen()
    {
        await ArrangeAsync();
        await OverdueExpenseAsync();
        await RunAsync();
        var alertId = (await AlertsAsync()).Single().Id;

        var taken = await Services.Alerts(T).ChangeStatusAsync(alertId, new FleetAlertStatusRequest { Status = FleetAlertStatus.InProgress }, default);
        await Services.Alerts(T).ChangeStatusAsync(alertId, new FleetAlertStatusRequest { Status = FleetAlertStatus.Resolved, Notes = "Pago" }, default);
        var reopen = () => Services.Alerts(T).ChangeStatusAsync(alertId, new FleetAlertStatusRequest { Status = FleetAlertStatus.InProgress }, default);

        taken.AssignedToUserId.Should().Be(T.CurrentUser.UserId);
        await reopen.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Notifications_AreOnlyTheCurrentUsersOwn()
    {
        await ArrangeAsync();
        var finance = await T.AddUserAsync(Company, "fin@frota.local", SystemRoles.Finance);
        var other = await T.AddUserAsync(Company, "fin2@frota.local", SystemRoles.Finance);
        var rule = await RuleAsync(AutomationTrigger.ExpenseOverdue);
        var response = await Services.AutomationRules(T).GetAsync(rule.Id, default);
        await Services.AutomationRules(T).UpdateAsync(rule.Id,
            AutomationEngine_ScheduledRulesTests.ToRequest(response) with { NotifyMode = AutomationNotifyMode.EveryoneWhoCanSee }, default);
        await OverdueExpenseAsync();
        await RunAsync();

        T.SignInAs(Company, SystemRoles.Finance, finance.Id);
        var mine = await Services.Notifications(T).ListAsync(unreadOnly: false, page: 1, default);
        var othersId = (await T.NewContext().UserNotifications.SingleAsync(n => n.UserId == other.Id)).Id;
        var act = () => Services.Notifications(T).MarkReadAsync(othersId, default);

        mine.Items.Should().ContainSingle();
        mine.UnreadCount.Should().Be(1);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

public class AutomationRuleServiceTests : AutomationTestBase
{
    [Fact]
    public async Task Create_ThresholdOutOfRange_Throws()
    {
        await ArrangeAsync();
        var act = () => Services.AutomationRules(T).CreateAsync(new AutomationRuleRequest
        {
            Name = "Consumo", Trigger = AutomationTrigger.FuelConsumptionAbnormal, Threshold = 1m,
        }, default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Create_SpecificUserOfAnotherCompany_Throws()
    {
        await ArrangeAsync();
        var other = await T.AddCompanyAsync("11444777000161", "Outra");
        var stranger = await T.AddUserAsync(other, "x@outra.local", SystemRoles.Administrator);
        AsSystem();
        var act = () => Services.AutomationRules(T).CreateAsync(new AutomationRuleRequest
        {
            Name = "Atrasos", Trigger = AutomationTrigger.ExpenseOverdue, Threshold = 3m,
            NotifyMode = AutomationNotifyMode.SpecificUser, NotifyUserId = stranger.Id,
        }, default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Delete_DefaultRule_IsRefused()
    {
        await ArrangeAsync();
        await Services.AutomationRules(T).ListAsync(default);
        var rule = await RuleAsync(AutomationTrigger.TireTreadLow);
        var act = () => Services.AutomationRules(T).DeleteAsync(rule.Id, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task List_OtherCompanyRules_AreNotVisible()
    {
        await ArrangeAsync();
        await Services.AutomationRules(T).CreateAsync(new AutomationRuleRequest
        {
            Name = "Minha regra", Trigger = AutomationTrigger.ExpenseOverdue, Threshold = 10m,
        }, default);
        var other = await T.AddCompanyAsync("11444777000161", "Outra");
        T.SignInAs(other, SystemRoles.Administrator);

        var rules = await Services.AutomationRules(T).ListAsync(default);

        rules.Should().NotContain(r => r.Name == "Minha regra");
        rules.Should().OnlyContain(r => r.IsSystemDefault);
    }
}

public class AttentionServiceTests : AutomationTestBase
{
    [Fact]
    public async Task Get_GroupsOpenAlertsByTriggerAndRespectsPermissions()
    {
        await ArrangeAsync();
        await OverdueExpenseAsync(amount: 100m);
        await OverdueExpenseAsync(amount: 200m);
        await RunAsync();

        T.SignInAs(Company, SystemRoles.Finance);
        var finance = await Services.Attention(T).GetAsync(default);
        T.SignInAs(Company, SystemRoles.Operations);
        var operations = await Services.Attention(T).GetAsync(default);

        var item = finance.Items.Single(i => i.Key.StartsWith("alert:ExpenseOverdue"));
        item.Count.Should().Be(2);
        item.Label.Should().Be("despesas em atraso");
        item.Link.Should().Be("/alertas?trigger=ExpenseOverdue");
        operations.Items.Should().NotContain(i => i.Key.StartsWith("alert:ExpenseOverdue"));
    }

    [Fact]
    public async Task Get_EmptyQueues_AreLeftOut()
    {
        await ArrangeAsync();
        var result = await Services.Attention(T).GetAsync(default);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}
