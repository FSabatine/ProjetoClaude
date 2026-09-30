using FluentAssertions;
using Fleet.Application.Assignments;
using Fleet.Application.Common;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Auditing;
using Fleet.Domain.Drivers;
using Fleet.Domain.Operations;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Assignments;

public class AssignmentServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private AssignmentService Service => Services.Assignments(_t);

    private static AssignmentCreateRequest Assign(Guid driverId, bool endCurrent = false, DateTime? startedAt = null) =>
        new() { DriverId = driverId, EndCurrent = endCurrent, StartedAt = startedAt };

    [Fact]
    public async Task AssignAsync_Valid_CreatesActiveAssignmentEventAuditAndAssignedStatus()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t);

        var assignment = await Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);

        assignment.IsActive.Should().BeTrue();
        assignment.StartedAt.Should().Be(_t.Clock.UtcNow);
        var reloaded = await Services.Vehicles(_t).GetAsync(vehicle.Id, default);
        reloaded.OperationalStatus.Should().Be(VehicleOperationalStatus.Assigned);
        reloaded.Status.Should().Be(VehicleStatus.Available, "the stored condition does not change (ADR-018)");
        reloaded.CurrentAssignment!.DriverName.Should().Be(driver.FullName);

        var db = _t.NewContext();
        (await db.OperationalEvents.SingleAsync(e => e.Type == OperationalEventType.VehicleAssigned))
            .Should().Match<OperationalEvent>(e => e.VehicleId == vehicle.Id && e.DriverId == driver.Id && e.UserId == _t.CurrentUser.UserId);
        (await db.AuditLogs.AnyAsync(a => a.EntityName == "VehicleAssignment" && a.Action == AuditAction.Created)).Should().BeTrue();
    }

    [Fact]
    public async Task AssignAsync_VehicleAlreadyHasDriver_WithoutConfirmation_ThrowsConflict()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var first = await Scenario.DriverAsync(_t, 0);
        var second = await Scenario.DriverAsync(_t, 1);
        await Service.AssignAsync(vehicle.Id, Assign(first.Id), default);

        var act = () => Service.AssignAsync(vehicle.Id, Assign(second.Id), default);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain(first.FullName);
    }

    [Fact]
    public async Task AssignAsync_ConfirmedHandOver_EndsPreviousAndKeepsHistory()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var first = await Scenario.DriverAsync(_t, 0);
        var second = await Scenario.DriverAsync(_t, 1);
        await Service.AssignAsync(vehicle.Id, Assign(first.Id), default);
        _t.Clock.UtcNow = _t.Clock.UtcNow.AddDays(15);

        await Service.AssignAsync(vehicle.Id, Assign(second.Id, endCurrent: true), default);

        var history = await Service.ListForVehicleAsync(vehicle.Id, new ListRequest(), default);
        history.Items.Should().HaveCount(2);
        history.Items[0].Should().Match<AssignmentResponse>(a => a.DriverId == second.Id && a.IsActive);
        history.Items[1].Should().Match<AssignmentResponse>(a => a.DriverId == first.Id && !a.IsActive && a.EndedAt == _t.Clock.UtcNow);
    }

    [Fact]
    public async Task AssignAsync_DriverAlreadyOnAnotherVehicle_ConfirmedMovesTheDriver()
    {
        await Scenario.SignedInAsync(_t);
        var abc = await Scenario.VehicleAsync(_t, 0);
        var def = await Scenario.VehicleAsync(_t, 1);
        var john = await Scenario.DriverAsync(_t);
        await Service.AssignAsync(abc.Id, Assign(john.Id), default);

        var refused = () => Service.AssignAsync(def.Id, Assign(john.Id), default);
        (await refused.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("ABC1D23");

        await Service.AssignAsync(def.Id, Assign(john.Id, endCurrent: true), default);

        var johnHistory = await Service.ListForDriverAsync(john.Id, new ListRequest(), default);
        johnHistory.Items.Select(a => (a.VehicleId, a.IsActive)).Should().Equal((def.Id, true), (abc.Id, false));
        (await Services.Vehicles(_t).GetAsync(abc.Id, default)).OperationalStatus.Should().Be(VehicleOperationalStatus.Available);
    }

    [Fact]
    public async Task AssignAsync_SameDriverAgain_ThrowsBusinessRule()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t);
        await Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);

        var act = () => Service.AssignAsync(vehicle.Id, Assign(driver.Id, endCurrent: true), default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*já é o motorista atual*");
    }

    [Theory]
    [InlineData(DriverStatus.Inactive, "desligado")]
    [InlineData(DriverStatus.OnLeave, "afastado")]
    public async Task AssignAsync_DriverNotActive_ThrowsBusinessRule(DriverStatus status, string message)
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t, change: r => r with { Status = status });

        var act = () => Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage($"*{message}*");
    }

    [Fact]
    public async Task AssignAsync_ExpiredLicense_ThrowsBusinessRule()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t, change: r => r with { LicenseExpiresOn = _t.Clock.Today.AddDays(-1) });

        var act = () => Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*CNH*vencida*");
    }

    [Fact]
    public async Task AssignAsync_InactiveVehicle_ThrowsBusinessRule()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t, change: r => r with { Status = VehicleStatus.Inactive });
        var driver = await Scenario.DriverAsync(_t);

        var act = () => Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*inativo*");
    }

    [Fact]
    public async Task AssignAsync_FutureStart_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t);

        var act = () => Service.AssignAsync(vehicle.Id, Assign(driver.Id, startedAt: _t.Clock.UtcNow.AddDays(1)), default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "StartedAt");
    }

    [Fact]
    public async Task AssignAsync_HandOverBeforeCurrentStart_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var first = await Scenario.DriverAsync(_t, 0);
        var second = await Scenario.DriverAsync(_t, 1);
        await Service.AssignAsync(vehicle.Id, Assign(first.Id), default);

        var act = () => Service.AssignAsync(vehicle.Id, Assign(second.Id, endCurrent: true, startedAt: _t.Clock.UtcNow.AddDays(-2)), default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "startedAt");
    }

    [Fact]
    public async Task AssignAsync_LateRegistrationOverlappingEndedPeriod_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var first = await Scenario.DriverAsync(_t, 0);
        var second = await Scenario.DriverAsync(_t, 1);
        var start = _t.Clock.UtcNow;
        var assignment = await Service.AssignAsync(vehicle.Id, Assign(first.Id), default);
        _t.Clock.UtcNow = start.AddDays(10);
        await Service.EndAsync(assignment.Id, new AssignmentEndRequest(), default);

        var act = () => Service.AssignAsync(vehicle.Id, Assign(second.Id, startedAt: start.AddDays(5)), default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Contain("sobrepõe");
    }

    [Fact]
    public async Task EndAsync_ActiveAssignment_EndsItAndRecordsEvent()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t);
        var assignment = await Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);
        _t.Clock.UtcNow = _t.Clock.UtcNow.AddHours(8);

        var ended = await Service.EndAsync(assignment.Id, new AssignmentEndRequest { Reason = "Férias" }, default);

        ended.IsActive.Should().BeFalse();
        ended.EndReason.Should().Be("Férias");
        (await _t.NewContext().OperationalEvents.CountAsync(e => e.Type == OperationalEventType.VehicleAssignmentEnded)).Should().Be(1);
        (await Services.Vehicles(_t).GetAsync(vehicle.Id, default)).CurrentAssignment.Should().BeNull();
    }

    [Fact]
    public async Task EndAsync_BeforeStart_ThrowsValidation_AndAlreadyEnded_ThrowsBusinessRule()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t);
        var assignment = await Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);

        var beforeStart = () => Service.EndAsync(assignment.Id, new AssignmentEndRequest { EndedAt = _t.Clock.UtcNow.AddHours(-1) }, default);
        await beforeStart.Should().ThrowAsync<ValidationException>();

        await Service.EndAsync(assignment.Id, new AssignmentEndRequest(), default);
        var again = () => Service.EndAsync(assignment.Id, new AssignmentEndRequest(), default);
        await again.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task VehicleInactivation_WithActiveAssignment_IsBlocked()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t);
        await Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);

        var act = () => Services.Vehicles(_t).UpdateAsync(vehicle.Id,
            Requests.Vehicle(vehicle.LicensePlate, vehicle.Renavam, vehicle.Chassis) with { Status = VehicleStatus.Inactive }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*Encerre a alocação*");
    }

    [Fact]
    public async Task DriverDismissal_WithActiveAssignment_IsBlocked_ButLeaveIsAllowed()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t);
        await Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);
        var request = Requests.Driver(_t.Clock.Today, driver.Cpf, driver.LicenseNumber) with { FullName = driver.FullName };

        var dismiss = () => Services.Drivers(_t).UpdateAsync(driver.Id, request with { Status = DriverStatus.Inactive }, default);
        await dismiss.Should().ThrowAsync<BusinessRuleException>();

        var leave = () => Services.Drivers(_t).UpdateAsync(driver.Id, request with { Status = DriverStatus.OnLeave }, default);
        await leave.Should().NotThrowAsync("the vehicle stays reserved for a driver on leave");
    }

    [Fact]
    public async Task DeleteVehicle_WithAssignmentHistory_IsBlocked()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        var driver = await Scenario.DriverAsync(_t);
        var assignment = await Service.AssignAsync(vehicle.Id, Assign(driver.Id), default);
        await Service.EndAsync(assignment.Id, new AssignmentEndRequest(), default);

        var act = () => Services.Vehicles(_t).DeleteAsync(vehicle.Id, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*histórico operacional*");
    }

    [Fact]
    public async Task AssignAsync_VehicleOfAnotherCompany_ThrowsNotFound()
    {
        await Scenario.SignedInAsync(_t);
        var foreignVehicle = await Scenario.VehicleAsync(_t);
        await Scenario.SignedInAsync(_t, cnpj: "12ABC34501DE35");
        var driver = await Scenario.DriverAsync(_t);

        var act = () => Service.AssignAsync(foreignVehicle.Id, Assign(driver.Id), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task VehicleList_FiltersByOperationalStatusAndDriver()
    {
        await Scenario.SignedInAsync(_t);
        var assigned = await Scenario.VehicleAsync(_t, 0);
        var free = await Scenario.VehicleAsync(_t, 1);
        var driver = await Scenario.DriverAsync(_t);
        await Service.AssignAsync(assigned.Id, Assign(driver.Id), default);
        var vehicles = Services.Vehicles(_t);

        (await vehicles.ListAsync(new() { OperationalStatus = VehicleOperationalStatus.Assigned }, default)).Items
            .Should().ContainSingle(v => v.Id == assigned.Id && v.CurrentDriverName == driver.FullName);
        (await vehicles.ListAsync(new() { OperationalStatus = VehicleOperationalStatus.Available }, default)).Items
            .Should().ContainSingle(v => v.Id == free.Id);
        (await vehicles.ListAsync(new() { DriverId = driver.Id }, default)).Items.Should().ContainSingle(v => v.Id == assigned.Id);
        (await vehicles.ListAsync(new() { Search = "Motorista 1" }, default)).Items.Should().ContainSingle(v => v.Id == assigned.Id);
    }
}
