using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Maintenance;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Operations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Maintenance;

public class HourMeterServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private HourMeterService Service => Services.HourMeter(_t);

    private static HourMeterReadingRequest Reading(decimal hours, DateTime? readAt = null, bool correction = false, string? notes = null) =>
        new() { Hours = hours, ReadAt = readAt, IsCorrection = correction, Notes = notes };

    /// <summary>Vehicle registered with 1,000 h on the meter; the clock then moves forward.</summary>
    private async Task<Guid> VehicleAt1000hAsync()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t, change: r => r with { HourMeter = 1_000m });
        _t.Clock.UtcNow = _t.Clock.UtcNow.AddDays(4);
        return vehicle.Id;
    }

    [Fact]
    public async Task RecordAsync_ValidIncrease_UpdatesCurrentHourMeter()
    {
        var vehicleId = await VehicleAt1000hAsync();

        var reading = await Service.RecordAsync(vehicleId, Reading(1_040), default);

        reading.Status.Should().Be(HourMeterReadingStatus.Valid);
        var vehicle = await Services.Vehicles(_t).GetAsync(vehicleId, default);
        vehicle.HourMeter.Should().Be(1_040);
        (await _t.NewContext().OperationalEvents.SingleAsync(e => e.Type == OperationalEventType.HourMeterRecorded)).Summary.Should().Contain("1.040");
    }

    [Fact]
    public async Task RecordAsync_Decrease_IsRejectedWithGuidance()
    {
        var vehicleId = await VehicleAt1000hAsync();

        var act = () => Service.RecordAsync(vehicleId, Reading(999), default);

        var error = (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single();
        error.PropertyName.Should().Be("hours");
        error.ErrorMessage.Should().Contain("menor que a última leitura válida");
    }

    [Fact]
    public async Task RecordAsync_SuspiciousJump_IsKeptPendingAndNotApplied()
    {
        var vehicleId = await VehicleAt1000hAsync();

        var reading = await Service.RecordAsync(vehicleId, Reading(10_000), default);

        reading.Status.Should().Be(HourMeterReadingStatus.PendingReview);
        (await Services.Vehicles(_t).GetAsync(vehicleId, default)).HourMeter.Should().Be(1_000, "suspicious values are never applied automatically");
        (await _t.NewContext().OperationalEvents.CountAsync(e => e.Type == OperationalEventType.HourMeterAnomalyDetected)).Should().Be(1);
    }

    [Fact]
    public async Task ApproveAsync_AppliesPendingReading()
    {
        var vehicleId = await VehicleAt1000hAsync();
        var reading = await Service.RecordAsync(vehicleId, Reading(10_000), default);

        await Service.ApproveAsync(reading.Id, new HourMeterReviewRequest { Notes = "Confirmado com o motorista" }, default);

        (await Services.Vehicles(_t).GetAsync(vehicleId, default)).HourMeter.Should().Be(10_000);
    }

    [Fact]
    public async Task RejectAsync_RequiresReasonAndKeepsVehicleUnchanged()
    {
        var vehicleId = await VehicleAt1000hAsync();
        var reading = await Service.RecordAsync(vehicleId, Reading(10_000), default);

        var act = () => Service.RejectAsync(reading.Id, new HourMeterReviewRequest(), default);
        await act.Should().ThrowAsync<ValidationException>();

        await Service.RejectAsync(reading.Id, new HourMeterReviewRequest { Notes = "Erro de digitação" }, default);
        (await Services.Vehicles(_t).GetAsync(vehicleId, default)).HourMeter.Should().Be(1_000);
    }

    [Fact]
    public async Task RecordAsync_Correction_CanGoBackwardsWithReason()
    {
        var vehicleId = await VehicleAt1000hAsync();

        var act = () => Service.RecordAsync(vehicleId, Reading(500, correction: true), default);
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*motivo da correção*");

        var corrected = await Service.RecordAsync(vehicleId, Reading(500, correction: true, notes: "Troca do painel"), default);
        corrected.Status.Should().Be(HourMeterReadingStatus.Valid);
        (await Services.Vehicles(_t).GetAsync(vehicleId, default)).HourMeter.Should().Be(500);
        (await _t.NewContext().OperationalEvents.AnyAsync(e => e.Type == OperationalEventType.HourMeterCorrected)).Should().BeTrue();
    }
}
