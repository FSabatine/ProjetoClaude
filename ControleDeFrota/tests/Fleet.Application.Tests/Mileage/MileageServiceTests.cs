using System.Text.Json;
using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Mileage;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Mileage;
using Fleet.Domain.Operations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Mileage;

public class MileageServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private MileageService Service => Services.Mileage(_t);

    private static OdometerReadingRequest Reading(int km, DateTime? readAt = null, bool correction = false, string? notes = null) =>
        new() { OdometerKm = km, ReadAt = readAt, IsCorrection = correction, Notes = notes };

    /// <summary>Vehicle registered with 120,000 km; the clock then moves forward.</summary>
    private async Task<Guid> VehicleAt120kAsync(string role = SystemRoles.FleetManager)
    {
        await Scenario.SignedInAsync(_t, role);
        var vehicle = await Scenario.VehicleAsync(_t, change: r => r with { CurrentOdometerKm = 120_000 });
        _t.Clock.UtcNow = _t.Clock.UtcNow.AddDays(4);
        return vehicle.Id;
    }

    [Fact]
    public async Task VehicleCreation_WritesTheInitialReading()
    {
        var vehicleId = await VehicleAt120kAsync();

        var history = await Service.ListAsync(vehicleId, new(), default);

        history.Items.Should().ContainSingle().Which.Should().Match<OdometerReadingResponse>(r =>
            r.OdometerKm == 120_000 && r.Source == OdometerReadingSource.Registration && r.Status == OdometerReadingStatus.Valid);
    }

    [Fact]
    public async Task RecordAsync_ValidIncrease_UpdatesCurrentMileageAndKeepsHistory()
    {
        var vehicleId = await VehicleAt120kAsync();

        var reading = await Service.RecordAsync(vehicleId, Reading(120_480), default);

        reading.Status.Should().Be(OdometerReadingStatus.Valid);
        reading.RecordedByName.Should().BeNull("the test user is not persisted — the id is still stamped");
        var vehicle = await Services.Vehicles(_t).GetAsync(vehicleId, default);
        vehicle.CurrentOdometerKm.Should().Be(120_480);
        vehicle.OdometerUpdatedAt.Should().Be(_t.Clock.UtcNow);
        (await Service.ListAsync(vehicleId, new(), default)).Items.Select(r => r.OdometerKm).Should().Equal(120_480, 120_000);

        var db = _t.NewContext();
        (await db.OdometerReadings.SingleAsync(r => r.Id == reading.Id)).CreatedBy.Should().Be(_t.CurrentUser.UserId);
        (await db.OperationalEvents.SingleAsync(e => e.Type == OperationalEventType.MileageRecorded)).Summary.Should().Contain("120.480 km");
    }

    [Fact]
    public async Task RecordAsync_Decrease_IsRejectedWithGuidance()
    {
        var vehicleId = await VehicleAt120kAsync();

        var act = () => Service.RecordAsync(vehicleId, Reading(119_900), default);

        var error = (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single();
        error.PropertyName.Should().Be("odometerKm");
        error.ErrorMessage.Should().Contain("menor que a última leitura válida").And.Contain("120.000 km");
        (await _t.NewContext().OdometerReadings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RecordAsync_SuspiciousJump_IsKeptPendingAndNotApplied()
    {
        var vehicleId = await VehicleAt120kAsync();

        var reading = await Service.RecordAsync(vehicleId, Reading(1_204_800), default);

        reading.Status.Should().Be(OdometerReadingStatus.PendingReview);
        reading.Anomaly.Should().Contain("km/dia");
        (await Services.Vehicles(_t).GetAsync(vehicleId, default)).CurrentOdometerKm.Should().Be(120_000, "suspicious values are never applied automatically");
        (await _t.NewContext().OperationalEvents.CountAsync(e => e.Type == OperationalEventType.MileageAnomalyDetected)).Should().Be(1);

        // The next honest reading is still compared with the last VALID one.
        var next = await Service.RecordAsync(vehicleId, Reading(120_900), default);
        next.Status.Should().Be(OdometerReadingStatus.Valid);
    }

    [Fact]
    public async Task ApproveAsync_PendingStillNewest_AppliesIt()
    {
        var vehicleId = await VehicleAt120kAsync();
        var pending = await Service.RecordAsync(vehicleId, Reading(130_000), default);

        var approved = await Service.ApproveAsync(pending.Id, new() { Notes = "Viagem longa confirmada" }, default);

        approved.Status.Should().Be(OdometerReadingStatus.Valid);
        approved.ReviewNotes.Should().Be("Viagem longa confirmada");
        (await Services.Vehicles(_t).GetAsync(vehicleId, default)).CurrentOdometerKm.Should().Be(130_000);
    }

    [Fact]
    public async Task ApproveAsync_LaterValidReadingExists_IsRefused()
    {
        var vehicleId = await VehicleAt120kAsync();
        var pending = await Service.RecordAsync(vehicleId, Reading(1_204_800), default);
        _t.Clock.UtcNow = _t.Clock.UtcNow.AddHours(1);
        await Service.RecordAsync(vehicleId, Reading(120_900), default);

        var act = () => Service.ApproveAsync(pending.Id, new(), default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*posteriores*");
    }

    [Fact]
    public async Task RejectAsync_RequiresReason_AndKeepsTheReadingInHistory()
    {
        var vehicleId = await VehicleAt120kAsync();
        var pending = await Service.RecordAsync(vehicleId, Reading(1_204_800), default);

        var withoutReason = () => Service.RejectAsync(pending.Id, new(), default);
        await withoutReason.Should().ThrowAsync<ValidationException>();

        var rejected = await Service.RejectAsync(pending.Id, new() { Notes = "Dígito a mais" }, default);

        rejected.Status.Should().Be(OdometerReadingStatus.Rejected);
        (await Service.ListAsync(vehicleId, new() { Status = OdometerReadingStatus.Rejected }, default)).Items.Should().ContainSingle();
        var again = () => Service.ApproveAsync(pending.Id, new(), default);
        await again.Should().ThrowAsync<BusinessRuleException>().WithMessage("*já foi revisada*");
    }

    [Fact]
    public async Task RecordAsync_CorrectionByManager_MayDecrease_IsAuditedAndAnnounced()
    {
        var vehicleId = await VehicleAt120kAsync();

        var correction = await Service.RecordAsync(vehicleId, Reading(98_000, correction: true, notes: "Painel substituído"), default);

        correction.Source.Should().Be(OdometerReadingSource.Correction);
        correction.Status.Should().Be(OdometerReadingStatus.Valid);
        (await Services.Vehicles(_t).GetAsync(vehicleId, default)).CurrentOdometerKm.Should().Be(98_000);

        var db = _t.NewContext();
        var vehicleAudit = await db.AuditLogs.Where(a => a.EntityName == "Vehicle" && a.EntityId == vehicleId.ToString())
            .OrderByDescending(a => a.Id).FirstAsync();
        var changes = JsonDocument.Parse(vehicleAudit.Changes).RootElement.GetProperty("CurrentOdometerKm");
        changes.GetProperty("old").GetInt32().Should().Be(120_000);
        changes.GetProperty("new").GetInt32().Should().Be(98_000);
        (await db.AuditLogs.AnyAsync(a => a.EntityName == "OdometerReading" && a.EntityId == correction.Id.ToString())).Should().BeTrue();
        (await db.OperationalEvents.SingleAsync(e => e.Type == OperationalEventType.MileageCorrected)).Summary
            .Should().Contain("120.000 km").And.Contain("98.000 km").And.Contain("Painel substituído");
    }

    [Fact]
    public async Task RecordAsync_CorrectionWithoutReason_ThrowsValidation()
    {
        var vehicleId = await VehicleAt120kAsync();

        var act = () => Service.RecordAsync(vehicleId, Reading(98_000, correction: true), default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "Notes");
    }

    [Fact]
    public async Task RecordAsync_CorrectionByOperations_IsForbidden()
    {
        var vehicleId = await VehicleAt120kAsync(SystemRoles.Operations);

        var act = () => Service.RecordAsync(vehicleId, Reading(98_000, correction: true, notes: "x"), default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task RecordAsync_ReadingDatedBeforeTheLastOne_ThrowsValidation()
    {
        var vehicleId = await VehicleAt120kAsync();
        await Service.RecordAsync(vehicleId, Reading(120_480), default);

        var act = () => Service.RecordAsync(vehicleId, Reading(120_500, readAt: _t.Clock.UtcNow.AddDays(-1)), default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("readAt");
    }

    [Fact]
    public async Task VehicleUpdate_ChangingOdometerDirectly_IsRefused()
    {
        var vehicleId = await VehicleAt120kAsync();
        var vehicle = await Services.Vehicles(_t).GetAsync(vehicleId, default);

        var act = () => Services.Vehicles(_t).UpdateAsync(vehicleId,
            Requests.Vehicle(vehicle.LicensePlate, vehicle.Renavam, vehicle.Chassis) with { CurrentOdometerKm = 150_000 }, default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("currentOdometerKm");
    }

    [Fact]
    public async Task StaleMileage_ListsActiveVehiclesWithoutRecentReading()
    {
        var vehicleId = await VehicleAt120kAsync();
        _t.Clock.UtcNow = _t.Clock.UtcNow.AddDays(OdometerPolicy.StaleAfterDays);

        var stale = await Services.Vehicles(_t).ListAsync(new() { StaleMileage = true }, default);
        stale.Items.Should().ContainSingle(v => v.Id == vehicleId);

        await Service.RecordAsync(vehicleId, Reading(121_000), default);
        (await Services.Vehicles(_t).ListAsync(new() { StaleMileage = true }, default)).Items.Should().BeEmpty();
    }
}
