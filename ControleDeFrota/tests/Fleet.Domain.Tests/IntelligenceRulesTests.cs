using Fleet.Domain.Authorization;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Operations;
using FluentAssertions;

namespace Fleet.Domain.Tests;

public class AlertPriorityTests
{
    [Fact]
    public void Compute_SameFactors_CriticalAboveWarningAboveInfo()
    {
        var critical = AlertPriority.Compute(FleetAlertSeverity.Critical, 10, 10, 0);
        var warning = AlertPriority.Compute(FleetAlertSeverity.Warning, 10, 10, 0);
        var info = AlertPriority.Compute(FleetAlertSeverity.Info, 10, 10, 0);
        critical.Should().BeGreaterThan(warning);
        warning.Should().BeGreaterThan(info);
    }

    [Fact]
    public void Compute_FactorsAreCapped()
    {
        AlertPriority.Compute(FleetAlertSeverity.Info, 999, 999, 99).Should().Be(10 + 30 + 30 + 30);
        AlertPriority.Compute(FleetAlertSeverity.Info, -5, -5, 0).Should().Be(10);
    }

    [Fact]
    public void Compute_RecurrenceRaisesPriority()
    {
        AlertPriority.Compute(FleetAlertSeverity.Warning, 10, 10, 1)
            .Should().BeGreaterThan(AlertPriority.Compute(FleetAlertSeverity.Warning, 10, 10, 0));
    }
}

public class FleetAlertWorkflowTests
{
    [Theory]
    [InlineData(FleetAlertStatus.New, FleetAlertStatus.InProgress, true)]
    [InlineData(FleetAlertStatus.Read, FleetAlertStatus.Dismissed, true)]
    [InlineData(FleetAlertStatus.InProgress, FleetAlertStatus.Read, false)]
    [InlineData(FleetAlertStatus.Resolved, FleetAlertStatus.InProgress, false)]
    [InlineData(FleetAlertStatus.Dismissed, FleetAlertStatus.New, false)]
    public void CanTransition_FollowsTheStateMachine(FleetAlertStatus from, FleetAlertStatus to, bool expected) =>
        FleetAlertWorkflow.CanTransition(from, to).Should().Be(expected);

    [Fact]
    public void RequiresNotes_OnlyToDismiss()
    {
        FleetAlertWorkflow.RequiresNotes(FleetAlertStatus.Dismissed).Should().BeTrue();
        FleetAlertWorkflow.RequiresNotes(FleetAlertStatus.Resolved).Should().BeFalse();
    }
}

public class AutomationCatalogTests
{
    [Fact]
    public void DefaultRules_ScheduledOnesAlwaysCreateAlerts()
    {
        AutomationTriggerCatalog.DefaultRules().Where(r => r.Trigger != AutomationTrigger.OperationalEvent)
            .Should().OnlyContain(r => r.CreateAlert && r.IsSystemDefault);
    }

    [Fact]
    public void Catalog_CoversEveryTrigger()
    {
        AutomationTriggerCatalog.All.Select(d => d.Trigger).Should().BeEquivalentTo(Enum.GetValues<AutomationTrigger>());
    }

    [Theory]
    [InlineData(OperationalEventType.ChecklistFailed, AlertAudience.Checklists)]
    [InlineData(OperationalEventType.FuelingMarkedForReview, AlertAudience.Fuel)]
    [InlineData(OperationalEventType.TireAnomalyDetected, AlertAudience.Tires)]
    [InlineData(OperationalEventType.WorkOrderCompleted, AlertAudience.Maintenance)]
    [InlineData(OperationalEventType.BudgetExceeded, AlertAudience.FinanceView)]
    [InlineData(OperationalEventType.DocumentExpired, AlertAudience.DriverDocuments)]
    public void EventAudience_FollowsTheModuleOfTheEvent(OperationalEventType type, AlertAudience expected) =>
        AutomationTriggerCatalog.EventAudience(type).Should().Be(expected);

    [Fact]
    public void VisibleTo_WithoutAlertsView_SeesNothing()
    {
        var all = PermissionCatalog.All.Select(p => p.Key).Where(k => k != Permissions.Alerts.View).ToHashSet();
        AlertAudiences.VisibleTo(all).Should().BeEmpty();
    }
}

public class TrendAnalysisTests
{
    [Fact]
    public void Compare_SmallBase_IsStable() =>
        TrendAnalysis.Compare(300m, 100m, minBase: 500m, minChangePercent: 10m).IsSignificant.Should().BeFalse();

    [Fact]
    public void Compare_SmallVariation_IsStableButReportsTheChange()
    {
        var result = TrendAnalysis.Compare(1_050m, 1_000m, 500m, 10m);
        result.Direction.Should().Be(TrendDirection.Stable);
        result.ChangePercent.Should().Be(5m);
    }

    [Fact]
    public void Compare_RelevantIncreaseAndDecrease()
    {
        TrendAnalysis.Compare(1_180m, 1_000m, 500m, 10m).Should().Be(new TrendResult(TrendDirection.Up, 18m, true));
        TrendAnalysis.Compare(800m, 1_000m, 500m, 10m).Should().Be(new TrendResult(TrendDirection.Down, -20m, true));
    }
}

public class VehicleHealthPolicyTests
{
    [Fact]
    public void Score_SubtractsAttentionAndCritical_IgnoringHiddenAreas()
    {
        var factors = new[]
        {
            new HealthFactor(HealthFactorArea.Maintenance, HealthFactorStatus.Critical, "", null),
            new HealthFactor(HealthFactorArea.Fuel, HealthFactorStatus.Attention, "", null),
            new HealthFactor(HealthFactorArea.Costs, HealthFactorStatus.NotVisible, "", null),
            new HealthFactor(HealthFactorArea.Tires, HealthFactorStatus.Good, "", null),
        };
        VehicleHealthPolicy.Score(factors).Should().Be(65);
    }

    [Theory]
    [InlineData(100, HealthLevel.Good)]
    [InlineData(80, HealthLevel.Good)]
    [InlineData(79, HealthLevel.Attention)]
    [InlineData(49, HealthLevel.Critical)]
    public void Level_Bands(int score, HealthLevel expected) => VehicleHealthPolicy.Level(score).Should().Be(expected);

    [Fact]
    public void Score_NeverBelowZero() =>
        VehicleHealthPolicy.Score(Enumerable.Repeat(new HealthFactor(HealthFactorArea.Tires, HealthFactorStatus.Critical, "", null), 7)).Should().Be(0);
}
