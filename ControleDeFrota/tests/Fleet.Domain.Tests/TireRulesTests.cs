using FluentAssertions;
using Fleet.Domain.Tires;

namespace Fleet.Domain.Tests;

public class TireRulesTests
{
    // ---------- workflow (seção 6) ----------

    [Theory]
    [InlineData(TireStatus.InStock, true)]
    [InlineData(TireStatus.Installed, false)]
    [InlineData(TireStatus.UnderInspection, false)]
    [InlineData(TireStatus.UnderRepair, false)]
    [InlineData(TireStatus.UnderRetread, false)]
    [InlineData(TireStatus.Disposed, false)]
    public void CanInstall_OnlyFromStock(TireStatus status, bool expected) => TireWorkflow.CanInstall(status).Should().Be(expected);

    [Theory]
    [InlineData(TireStatus.InStock, true)]
    [InlineData(TireStatus.UnderInspection, true)]
    [InlineData(TireStatus.Installed, false)]
    [InlineData(TireStatus.UnderRepair, false)]
    [InlineData(TireStatus.Disposed, false)]
    public void CanDispose_NeverAnInstalledTire(TireStatus status, bool expected) => TireWorkflow.CanDispose(status).Should().Be(expected);

    [Theory]
    [InlineData(TireRemovalDestination.Stock, TireStatus.InStock)]
    [InlineData(TireRemovalDestination.Evaluation, TireStatus.UnderInspection)]
    [InlineData(TireRemovalDestination.Repair, TireStatus.UnderRepair)]
    [InlineData(TireRemovalDestination.Retread, TireStatus.UnderRetread)]
    [InlineData(TireRemovalDestination.Disposal, TireStatus.Disposed)]
    [InlineData(TireRemovalDestination.Installed, TireStatus.Installed)]
    public void AfterRemoval_FollowsTheDestination(TireRemovalDestination destination, TireStatus expected) =>
        TireWorkflow.AfterRemoval(destination).Should().Be(expected);

    [Fact]
    public void AfterService_OnlyAnApprovedServiceReturnsToStock()
    {
        TireWorkflow.AfterService(TireServiceStatus.Completed, TireServiceResult.Approved).Should().Be(TireStatus.InStock);
        TireWorkflow.AfterService(TireServiceStatus.Completed, TireServiceResult.Rejected).Should().Be(TireStatus.UnderInspection);
        TireWorkflow.AfterService(TireServiceStatus.Cancelled, null).Should().Be(TireStatus.UnderInspection);
    }

    [Fact]
    public void Disposed_IsFinal()
    {
        TireWorkflow.IsFinal(TireStatus.Disposed).Should().BeTrue();
        TireWorkflow.CanInspect(TireStatus.Disposed).Should().BeFalse();
        TireWorkflow.CanEdit(TireStatus.Disposed).Should().BeFalse();
        TireWorkflow.CanSendToService(TireStatus.Disposed).Should().BeFalse();
    }

    // ---------- identification ----------

    [Theory]
    [InlineData("DOT 4B7X 3524", 2024, 8, 26)]
    [InlineData("dot  xyz12 0123", 2023, 1, 2)]
    [InlineData("1220", 2020, 3, 16)]
    public void Dot_LastFourDigitsAreWeekAndYear(string dot, int year, int month, int day) =>
        TireDot.ManufacturedOn(dot).Should().Be(new DateOnly(year, month, day));

    [Theory]
    [InlineData(null)]
    [InlineData("DOT ABC")]
    [InlineData("DOT 5424")] // week 54 does not exist
    [InlineData("DOT 0024")]
    [InlineData("DOT 123")] // pre-2000 three-digit code
    public void Dot_WithoutAValidDateCode_HasNoDate(string? dot) => TireDot.ManufacturedOn(dot).Should().BeNull();

    [Theory]
    [InlineData("295/80 r 22,5", "295/80R22.5")]
    [InlineData(" 11.00 R22 ", "11.00R22")]
    public void Size_IsNormalizedForComparison(string input, string expected) => TireSizes.Normalize(input).Should().Be(expected);

    [Fact]
    public void Code_IsGeneratedWithSixDigits() => Tire.FormatCode(123).Should().Be("PN-000123");

    // ---------- positions (seções 8–10) ----------

    private static TireLayout Truck(int spares = 1) => new()
    {
        Name = "Toco", SpareCount = spares,
        Axles =
        [
            new TireLayoutAxle { Number = 1, Type = AxleType.Steer, AllowedSize = "295/80R22.5", RecommendedPressurePsi = 120 },
            new TireLayoutAxle { Number = 2, Type = AxleType.Drive, IsDual = true },
        ],
    };

    [Fact]
    public void Positions_AreGeneratedFromTheAxles()
    {
        var positions = TirePositions.For(Truck());

        positions.Select(p => p.Code).Should().Equal("1E", "1D", "2EE", "2EI", "2DI", "2DE", "EST1");
        positions[2].Label.Should().Be("Eixo 2 — Esquerdo externo");
        positions[0].AllowedSize.Should().Be("295/80R22.5");
        positions[^1].IsSpare.Should().BeTrue();
        positions[^1].IsRequired.Should().BeFalse();
    }

    [Fact]
    public void Positions_TwoSparesAreNumbered() =>
        TirePositions.For(Truck(spares: 2)).Where(p => p.IsSpare).Select(p => p.Label).Should().Equal("Estepe 1", "Estepe 2");

    [Fact]
    public void DualPartner_IsTheOtherTireOfTheSameSide()
    {
        var positions = TirePositions.For(Truck());
        TirePositions.DualPartner(positions.Single(p => p.Code == "2EE")).Should().Be("2EI");
        TirePositions.DualPartner(positions.Single(p => p.Code == "2DI")).Should().Be("2DE");
        TirePositions.DualPartner(positions.Single(p => p.Code == "1E")).Should().BeNull();
    }

    // ---------- compatibility (seção 33) ----------

    [Fact]
    public void Compatibility_WrongSizeOnAConfiguredAxle_IsIncompatible()
    {
        var steer = TirePositions.For(Truck())[0];

        var result = TireCompatibility.Check(steer, "275/80 R22.5", TireApplication.AllPosition, null);

        result.Status.Should().Be(TireCompatibilityStatus.Incompatible);
    }

    [Fact]
    public void Compatibility_WithoutConfiguredSize_IsNeverClaimed()
    {
        var drive = TirePositions.For(Truck())[2];

        var result = TireCompatibility.Check(drive, "295/80R22.5", TireApplication.AllPosition, null);

        result.Status.Should().Be(TireCompatibilityStatus.NotVerified);
        result.Messages.Single().Should().Be(TireCompatibility.NotVerifiedMessage);
    }

    [Fact]
    public void Compatibility_DriveTireOnTheSteerAxle_AndMismatchedDualPair_AreWarnings()
    {
        var positions = TirePositions.For(Truck());

        TireCompatibility.Check(positions[0], "295/80R22.5", TireApplication.Drive, null).Status.Should().Be(TireCompatibilityStatus.Warning);
        TireCompatibility.Check(positions[2], "295/80R22.5", TireApplication.Drive, "275/80R22.5").Messages.Should().ContainSingle(m => m.Contains("geminados"));
    }

    // ---------- pressure (seção 19) ----------

    [Theory]
    [InlineData(110, TirePressureUnit.Psi, TirePressureCheck.WithinRange)]
    [InlineData(95, TirePressureUnit.Psi, TirePressureCheck.Low)]
    [InlineData(135, TirePressureUnit.Psi, TirePressureCheck.High)]
    [InlineData(7.6, TirePressureUnit.Bar, TirePressureCheck.WithinRange)] // ≈ 110 psi
    [InlineData(620, TirePressureUnit.Kpa, TirePressureCheck.Low)] // ≈ 90 psi
    public void Pressure_IsComparedWithTheReferenceInAnyUnit(decimal value, TirePressureUnit unit, TirePressureCheck expected) =>
        TirePressure.Check(value, unit, recommendedPsi: 110, tolerancePercent: 10).Should().Be(expected);

    [Fact]
    public void Pressure_WithoutReference_IsNotEvaluated() =>
        TirePressure.Check(80, TirePressureUnit.Psi, null, 10).Should().Be(TirePressureCheck.NotEvaluated);

    // ---------- mileage and cost (seções 25, 38) ----------

    [Fact]
    public void StintDistance_FromTheOdometer_ZeroForSpare_NullWhenUnknown_NeverNegative()
    {
        TireMileage.StintDistance(false, 100_000, 112_500).Should().Be(12_500);
        TireMileage.StintDistance(true, 100_000, 112_500).Should().Be(0);
        TireMileage.StintDistance(false, null, null).Should().BeNull();
        TireMileage.StintDistance(false, 100_000, 99_000).Should().Be(0, "an odometer correction below the installation km never yields negative km");
    }

    [Fact]
    public void CostPerKm_OnlyWithEnoughMeasuredKm()
    {
        TireCostPolicy.CostPerKm(2_000m, 100_000, false).Should().Be(0.02m);
        TireCostPolicy.CostPerKm(2_000m, 4_999, false).Should().BeNull();
        TireCostPolicy.CostPerKm(2_000m, 100_000, hasUnmeasuredDistance: true).Should().BeNull();
        TireCostPolicy.CostPerKm(0m, 100_000, false).Should().BeNull();
    }

    // ---------- alerts and anomalies (seções 34, 35) ----------

    [Fact]
    public void Alerts_TreadThresholdsAreCompanyPolicy()
    {
        var settings = TireSettings.Defaults();
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(now);

        TireAlertPolicy.Evaluate(new Tire { CurrentTreadDepthMm = 3.5m }, settings, now, today).Single().Type.Should().Be(TireAlertType.TreadNearMinimum);
        var critical = TireAlertPolicy.Evaluate(new Tire { CurrentTreadDepthMm = 3m }, settings, now, today).Single();
        critical.Severity.Should().Be(TireAlertSeverity.Critical);
        critical.Message.Should().Contain("configurado pela empresa");
        TireAlertPolicy.Evaluate(new Tire { CurrentTreadDepthMm = 3m, Status = TireStatus.Disposed }, settings, now, today).Should().BeEmpty();
    }

    [Fact]
    public void Alerts_InspectionOverdueAndAge()
    {
        var settings = TireSettings.Defaults();
        var now = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(now);
        var tire = new Tire
        {
            Status = TireStatus.Installed, InspectionReferenceAt = now.AddDays(-31), ManufacturedOn = today.AddYears(-6),
            LastWearPattern = TireWearPattern.Cupping,
        };

        TireAlertPolicy.Evaluate(tire, settings, now, today).Select(a => a.Type).Should()
            .BeEquivalentTo([TireAlertType.InspectionOverdue, TireAlertType.AgeExceeded, TireAlertType.UnevenWear]);
        tire.Status = TireStatus.InStock;
        TireAlertPolicy.IsInspectionOverdue(tire, settings, now).Should().BeFalse("only installed tires are due for inspection");
    }

    [Fact]
    public void WearRate_NeedsComparableMeasurements()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        TireAnomalyRules.WearRate(new(start, 16m, 0), new(start.AddDays(30), 14m, 2_000)).Should().Be(1.0m);
        TireAnomalyRules.WearRate(new(start, 16m, 0), new(start.AddDays(1), 15m, 500)).Should().BeNull("too little distance to be meaningful");
        TireAnomalyRules.WearRate(new(start, 4m, 80_000), new(start.AddDays(30), 14m, 80_000)).Should().BeNull("a retread in between");
        TireAnomalyRules.WearRate(new(start, 16m, null), new(start.AddDays(30), 14m, 2_000)).Should().BeNull();
    }

    [Fact]
    public void Anomalies_SayRequiresReviewAndNeverACause()
    {
        var settings = TireSettings.Defaults();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var messages = new[]
        {
            TireAnomalyRules.RapidTreadLoss(new(start, 16m, 0), new(start, 13m, 2_000), settings),
            TireAnomalyRules.RepeatedRepairs(3),
            TireAnomalyRules.RepeatedPunctures(2),
            TireAnomalyRules.ShortLifecycle(10_000, false, new TireSettings { MinExpectedLifeKm = 50_000 }),
            TireAnomalyRules.RecurringPositionDamage("Eixo 1 — Esquerdo", "ABC-1D23", 2),
        };

        messages.Should().OnlyContain(m => m != null && m.Contains("Requer revisão"));
        TireAnomalyRules.RepeatedRepairs(2).Should().BeNull();
        TireAnomalyRules.ShortLifecycle(10_000, false, settings).Should().BeNull("the rule is off by default");
    }
}
