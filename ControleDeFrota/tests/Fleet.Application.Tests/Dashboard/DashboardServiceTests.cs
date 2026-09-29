using FluentAssertions;
using Fleet.Application.Dashboard;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Drivers;
using Fleet.Domain.Vehicles;

namespace Fleet.Application.Tests.Dashboard;

public class DashboardServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private DateOnly Today => _t.Clock.Today;

    private async Task ArrangeFleetAsync()
    {
        var company = await _t.AddCompanyAsync();
        _t.SignInAs(company, SystemRoles.FleetManager);
        var vehicles = Services.Vehicles(_t);
        await vehicles.CreateAsync(Requests.Vehicle("AAA1A11", "12345678900", "9BWZZZ377VT004251"), default);
        await vehicles.CreateAsync(Requests.Vehicle("BBB2B22", "00123456789", "9BSR6X400J1234567") with { Status = VehicleStatus.UnderMaintenance }, default);
        await vehicles.CreateAsync(Requests.Vehicle("CCC3C33", "23456789013", "9BM958074AB123456") with { Status = VehicleStatus.OnTrip }, default);
        await vehicles.CreateAsync(Requests.Vehicle("DDD4D44", "34567890125", "9BVAG40D5KE765432") with { Status = VehicleStatus.Inactive }, default);

        var drivers = Services.Drivers(_t);
        await drivers.CreateAsync(Requests.Driver(Today, "529.982.247-25", "04512345678") with { LicenseExpiresOn = Today.AddDays(-3) }, default);
        await drivers.CreateAsync(Requests.Driver(Today, "123.456.789-09", "05623456789") with { LicenseExpiresOn = Today.AddDays(5) }, default);
        await drivers.CreateAsync(Requests.Driver(Today, "987.654.321-00", "06734567890") with { Status = DriverStatus.OnLeave }, default);
        // Inactive drivers don't generate alerts.
        await drivers.CreateAsync(Requests.Driver(Today, "111.444.777-35", "07845678901") with { Status = DriverStatus.Inactive, LicenseExpiresOn = Today.AddDays(-100) }, default);
    }

    [Fact]
    public async Task GetAsync_ComputesIndicators()
    {
        await ArrangeFleetAsync();

        var result = await Services.Dashboard(_t).GetAsync(default);

        result.Indicators.Should().Be(new DashboardIndicators(
            ActiveVehicles: 3, AvailableVehicles: 1, VehiclesUnderMaintenance: 1, ActiveDrivers: 2));
    }

    [Fact]
    public async Task GetAsync_ListsLicenseAlertsMostUrgentFirst()
    {
        await ArrangeFleetAsync();

        var result = await Services.Dashboard(_t).GetAsync(default);

        result.TotalAlerts.Should().Be(2);
        result.Alerts.Select(a => a.Type).Should().Equal(AlertType.LicenseExpired, AlertType.LicenseExpiringSoon);
        result.Alerts[0].Severity.Should().Be(AlertSeverity.Critical);
        result.Alerts[1].Description.Should().Contain("vence em 5 dias");
        // Always dd/MM/yyyy with slashes, regardless of the server's regional settings.
        result.Alerts[0].Description.Should().EndWith($"venceu em {Today.AddDays(-3):dd'/'MM'/'yyyy}.");
    }

    [Fact]
    public async Task GetAsync_WithoutDriversViewPermission_HidesAlerts()
    {
        await ArrangeFleetAsync();
        _t.CurrentUser.PermissionSet = [Permissions.Dashboard.View];

        var result = await Services.Dashboard(_t).GetAsync(default);

        result.Alerts.Should().BeEmpty();
        result.TotalAlerts.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_OnlyCountsOwnCompany()
    {
        await ArrangeFleetAsync();
        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        _t.SignInAs(other, SystemRoles.FleetManager);

        var result = await Services.Dashboard(_t).GetAsync(default);

        result.Indicators.Should().Be(new DashboardIndicators(0, 0, 0, 0));
        result.Alerts.Should().BeEmpty();
    }
}
