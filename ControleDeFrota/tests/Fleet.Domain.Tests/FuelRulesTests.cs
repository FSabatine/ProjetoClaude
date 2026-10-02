using FluentAssertions;
using Fleet.Domain.Fuel;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Tests;

public class FuelingAmountsTests
{
    [Theory]
    [InlineData(250, 6.20, 1550.00)]
    [InlineData(37.512, 6.199, 232.54)]  // 232.536888 rounds half away from zero
    [InlineData(0.001, 0.005, 0.00)]
    public void Total_IsQuantityTimesPriceRoundedToCents(decimal quantity, decimal price, decimal expected) =>
        FuelingAmounts.Total(quantity, price).Should().Be(expected);

    [Theory]
    [InlineData(232.54, true)]
    [InlineData(232.59, true)]   // pump receipts may differ by a few cents
    [InlineData(233.00, false)]
    public void TotalMatches_AcceptsOnlyTheTolerance(decimal informed, bool expected) =>
        FuelingAmounts.TotalMatches(informed, 37.512m, 6.199m).Should().Be(expected);
}

public class FuelCompatibilityTests
{
    [Theory]
    [InlineData(VehicleFuelType.DieselS10, FuelCategory.Diesel, true)]
    [InlineData(VehicleFuelType.DieselS10, FuelCategory.Gasoline, false)]
    [InlineData(VehicleFuelType.Flex, FuelCategory.Ethanol, true)]
    [InlineData(VehicleFuelType.Flex, FuelCategory.Diesel, false)]
    [InlineData(VehicleFuelType.Hybrid, FuelCategory.Electric, true)]
    [InlineData(VehicleFuelType.Electric, FuelCategory.Diesel, false)]
    [InlineData(VehicleFuelType.Gasoline, FuelCategory.Other, true)]
    [InlineData(VehicleFuelType.Other, FuelCategory.Diesel, true)]
    public void IsCompatible(VehicleFuelType vehicle, FuelCategory product, bool expected) =>
        FuelCompatibility.IsCompatible(vehicle, product).Should().Be(expected);
}

public class FuelingStatusTests
{
    [Fact]
    public void RefreshStatus_UnreviewedAnomaly_IsPendingReview()
    {
        var fueling = new Fueling { Anomalies = [new FuelingAnomaly { Type = FuelAnomalyType.ExcessiveQuantity }] };
        fueling.RefreshStatus();
        fueling.Status.Should().Be(FuelingStatus.PendingReview);
    }

    [Fact]
    public void RefreshStatus_AllReviewed_IsValid()
    {
        var fueling = new Fueling { Status = FuelingStatus.PendingReview, Anomalies = [new FuelingAnomaly { ReviewedAt = DateTime.UtcNow }] };
        fueling.RefreshStatus();
        fueling.Status.Should().Be(FuelingStatus.Valid);
    }

    [Fact]
    public void RefreshStatus_Cancelled_StaysCancelled()
    {
        var fueling = new Fueling { Status = FuelingStatus.Cancelled, Anomalies = [new FuelingAnomaly()] };
        fueling.RefreshStatus();
        fueling.Status.Should().Be(FuelingStatus.Cancelled);
    }

    [Theory]
    [InlineData(FuelingStatus.Valid, true, true, false)]
    [InlineData(FuelingStatus.PendingReview, true, true, true)]
    [InlineData(FuelingStatus.Cancelled, false, false, false)]
    public void Workflow_AllowedActions(FuelingStatus status, bool correct, bool cancel, bool review)
    {
        FuelingWorkflow.CanCorrect(status).Should().Be(correct);
        FuelingWorkflow.CanCancel(status).Should().Be(cancel);
        FuelingWorkflow.CanReview(status).Should().Be(review);
    }
}

public class ConsumptionCalculatorTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ConsumptionChainEntry Entry(int day, int km, decimal liters, bool full = true, FuelUnit unit = FuelUnit.Liter,
        bool mileageUnderReview = false) =>
        new(Guid.NewGuid(), Start.AddDays(day), km, liters, liters * 6m, full, unit, mileageUnderReview);

    [Fact]
    public void Calculate_TwoFullTanks_IsDistanceOverFuel()
    {
        // seção 56: 100,000 → 101,000 km with 200 L = 5.0 km/L.
        var chain = new[] { Entry(0, 100_000, 300), Entry(3, 101_000, 200) };

        var outcomes = ConsumptionCalculator.Calculate(chain, []);

        outcomes[0].Result.Should().Be(ConsumptionResult.FirstFullTank);
        outcomes[1].Should().BeEquivalentTo(new ConsumptionOutcome(chain[1].FuelingId, ConsumptionResult.Calculated, 1_000, 200m, 1_200m, 5.0m));
    }

    [Fact]
    public void Calculate_PartialFills_AddToTheNextFullTank()
    {
        var chain = new[] { Entry(0, 100_000, 300), Entry(1, 100_400, 80, full: false), Entry(3, 101_000, 120) };

        var outcomes = ConsumptionCalculator.Calculate(chain, []);

        outcomes[1].Result.Should().Be(ConsumptionResult.PartialFill);
        outcomes[2].Quantity.Should().Be(200m);
        outcomes[2].Consumption.Should().Be(5.0m);
    }

    [Fact]
    public void Calculate_PartialFillsBeforeTheFirstFullTank_HaveNoFigure()
    {
        var chain = new[] { Entry(0, 100_000, 50, full: false), Entry(1, 100_300, 200) };

        var outcomes = ConsumptionCalculator.Calculate(chain, []);

        outcomes.Select(o => o.Result).Should().Equal(ConsumptionResult.PartialFill, ConsumptionResult.FirstFullTank);
    }

    [Fact]
    public void Calculate_OdometerCorrectionInsideTheSegment_IsNotReliable()
    {
        var chain = new[] { Entry(0, 100_000, 300), Entry(4, 101_000, 200) };

        var outcomes = ConsumptionCalculator.Calculate(chain, [Start.AddDays(2)]);

        outcomes[1].Result.Should().Be(ConsumptionResult.NotReliable);
        outcomes[1].Consumption.Should().BeNull();
    }

    [Fact]
    public void Calculate_CorrectionBeforeTheSegment_DoesNotAffectIt()
    {
        var chain = new[] { Entry(2, 100_000, 300), Entry(4, 101_000, 200) };

        ConsumptionCalculator.Calculate(chain, [Start.AddDays(1)])[1].Result.Should().Be(ConsumptionResult.Calculated);
    }

    [Fact]
    public void Calculate_MileageUnderReview_IsNotReliable()
    {
        var chain = new[] { Entry(0, 100_000, 300), Entry(2, 190_000, 200, mileageUnderReview: true) };

        ConsumptionCalculator.Calculate(chain, [])[1].Result.Should().Be(ConsumptionResult.NotReliable);
    }

    [Fact]
    public void Calculate_MixedUnits_IsNotReliable()
    {
        var chain = new[] { Entry(0, 100_000, 30), Entry(1, 100_200, 10, full: false, unit: FuelUnit.CubicMeter), Entry(2, 100_400, 20) };

        ConsumptionCalculator.Calculate(chain, [])[2].Result.Should().Be(ConsumptionResult.NotReliable);
    }

    [Fact]
    public void Calculate_NoDistance_IsNotReliable()
    {
        var chain = new[] { Entry(0, 100_000, 300), Entry(0, 100_000, 50) };

        ConsumptionCalculator.Calculate(chain, [])[1].Result.Should().Be(ConsumptionResult.NotReliable);
    }
}

public class ConsumptionBaselineTests
{
    [Fact]
    public void Resolve_ConfiguredWins()
    {
        var baseline = ConsumptionBaseline.Resolve(5.0m, new ConsumptionSample(10_000, 2_500, 8), null);
        baseline.Should().Be(new ResolvedBaseline(5.0m, ConsumptionBaselineSource.Configured));
    }

    [Fact]
    public void Resolve_VehicleHistory_IsWeightedAverage()
    {
        var baseline = ConsumptionBaseline.Resolve(null, new ConsumptionSample(9_000, 2_000, 3), null);
        baseline.Should().Be(new ResolvedBaseline(4.5m, ConsumptionBaselineSource.VehicleHistory));
    }

    [Fact]
    public void Resolve_NotEnoughHistory_FallsBackToTheVehicleType()
    {
        var baseline = ConsumptionBaseline.Resolve(null, new ConsumptionSample(2_000, 400, 2), new ConsumptionSample(50_000, 12_500, 20));
        baseline.Should().Be(new ResolvedBaseline(4.0m, ConsumptionBaselineSource.VehicleTypeAverage));
    }

    [Fact]
    public void Resolve_InsufficientData_HasNoBaseline() =>
        ConsumptionBaseline.Resolve(null, new ConsumptionSample(2_000, 400, 2), new ConsumptionSample(5_000, 1_000, 4)).Should().BeNull();
}

public class FuelAnomalyRulesTests
{
    private static readonly FuelSettings Settings = FuelSettings.Defaults();

    private static FuelingCheck Check(decimal quantity = 200, decimal price = 6.00m, decimal? tank = 400, decimal? reference = 6.00m,
        double? hours = 48, VehicleFuelType vehicleFuel = VehicleFuelType.DieselS10, FuelCategory product = FuelCategory.Diesel,
        string? mileage = null) =>
        new(quantity, FuelUnit.Liter, price, tank, reference, hours, vehicleFuel, product, "Diesel S10", mileage);

    private static IEnumerable<FuelAnomalyType> Types(FuelingCheck check, FuelSettings? settings = null) =>
        FuelAnomalyRules.EvaluateRecord(check, settings ?? Settings).Select(a => a.Type);

    [Fact]
    public void EvaluateRecord_NormalFueling_HasNoAnomaly() => Types(Check()).Should().BeEmpty();

    [Fact]
    public void EvaluateRecord_AboveTankCapacity_IsExcessiveQuantity()
    {
        // seção 55: 500 L in a 400 L tank.
        var anomaly = FuelAnomalyRules.EvaluateRecord(Check(quantity: 500), Settings).Single();
        anomaly.Type.Should().Be(FuelAnomalyType.ExcessiveQuantity);
        anomaly.Expected.Should().Be(400);
        anomaly.Actual.Should().Be(500);
    }

    [Theory]
    [InlineData(420, 5, false)]   // inside the 5% tolerance
    [InlineData(421, 5, true)]
    [InlineData(421, 10, false)]  // threshold is configurable
    public void EvaluateRecord_TankTolerance(decimal quantity, int tolerance, bool flagged)
    {
        var settings = new FuelSettings { TankTolerancePercent = tolerance };
        Types(Check(quantity: quantity), settings).Contains(FuelAnomalyType.ExcessiveQuantity).Should().Be(flagged);
    }

    [Fact]
    public void EvaluateRecord_UnknownTankCapacity_DoesNotFlagQuantity() =>
        Types(Check(quantity: 5_000, tank: null)).Should().BeEmpty();

    [Theory]
    [InlineData(9.50, true)]   // seção 12: average R$ 6.00, paid R$ 9.50
    [InlineData(0.62, true)]   // typo in the other direction
    [InlineData(7.10, false)]  // +18%: within 20%
    public void EvaluateRecord_AbnormalPrice(decimal price, bool flagged) =>
        Types(Check(price: price)).Contains(FuelAnomalyType.AbnormalPrice).Should().Be(flagged);

    [Fact]
    public void EvaluateRecord_PriceMessage_HasNoMoneyValues()
    {
        var anomaly = FuelAnomalyRules.EvaluateRecord(Check(price: 9.50m), Settings).Single();
        anomaly.Message.Should().NotContain("R$").And.Contain("58,3%");
    }

    [Fact]
    public void EvaluateRecord_MileageUnderReview_IsMileageJump() =>
        Types(Check(mileage: "Aumento de 90.000 km em 1 dia.")).Should().Equal(FuelAnomalyType.MileageJump);

    [Theory]
    [InlineData(0.5, 2, true)]
    [InlineData(3, 2, false)]
    [InlineData(0.5, 0, false)]  // 0 disables the rule
    public void EvaluateRecord_HighFrequency(double hours, int minHours, bool flagged)
    {
        var settings = new FuelSettings { MinHoursBetweenFuelings = minHours };
        Types(Check(hours: hours), settings).Contains(FuelAnomalyType.HighFrequency).Should().Be(flagged);
    }

    [Fact]
    public void EvaluateRecord_WrongProductForTheEngine_IsMismatch() =>
        Types(Check(product: FuelCategory.Gasoline)).Should().Equal(FuelAnomalyType.FuelTypeMismatch);

    [Fact]
    public void EvaluateConsumption_BelowThreshold_IsLowConsumption()
    {
        // seção 21: expected 5.0, actual 3.7 = −26%.
        var anomaly = FuelAnomalyRules.EvaluateConsumption(3.7m, new ResolvedBaseline(5.0m, ConsumptionBaselineSource.Configured), FuelUnit.Liter, 20);
        anomaly!.Type.Should().Be(FuelAnomalyType.LowConsumption);
        anomaly.Message.Should().Contain("-26%").And.Contain("Revisão recomendada").And.NotContainAny("fraude", "defeito");
    }

    [Fact]
    public void EvaluateConsumption_FarAboveExpected_IsHighConsumption() =>
        FuelAnomalyRules.EvaluateConsumption(7.0m, new ResolvedBaseline(5.0m, ConsumptionBaselineSource.VehicleHistory), FuelUnit.Liter, 20)!
            .Type.Should().Be(FuelAnomalyType.HighConsumption);

    [Fact]
    public void EvaluateConsumption_WithinThreshold_IsNull() =>
        FuelAnomalyRules.EvaluateConsumption(4.6m, new ResolvedBaseline(5.0m, ConsumptionBaselineSource.Configured), FuelUnit.Liter, 20)
            .Should().BeNull();
}
