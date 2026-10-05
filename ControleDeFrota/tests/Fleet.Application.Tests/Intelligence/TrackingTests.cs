using Fleet.Application.Common;
using Fleet.Application.Tests.TestSupport;
using Fleet.Application.Tracking;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;
using Fleet.Domain.Tracking;
using Fleet.Domain.Vehicles;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Intelligence;

public class TrackingTests : IDisposable
{
    private readonly TestDb T = new();
    private Company _company = null!;

    private async Task<(Guid VehicleId, TrackingDeviceKeyResponse Device)> ArrangeAsync(bool link = true)
    {
        _company = await Scenario.SignedInAsync(T, SystemRoles.FleetManager);
        var vehicle = await Scenario.VehicleAsync(T);
        var provider = await Services.TrackingProviders(T).SaveAsync(null, new TrackingProviderRequest { Name = "Rastreadora X" }, default);
        var device = await Services.TrackingDevices(T).CreateAsync(new TrackingDeviceRequest { TrackingProviderId = provider.Id, Identifier = "IMEI-1" }, default);
        if (link) await Services.TrackingDevices(T).LinkAsync(device.Device.Id, new LinkVehicleRequest(vehicle.Id), default);
        return (vehicle.Id, device);
    }

    private IngestRequest Positions(params (int MinutesAgo, decimal Lat, decimal Lon)[] fixes) => new()
    {
        Positions = fixes.Select(f => new IngestPosition
        {
            RecordedAt = T.Clock.UtcNow.AddMinutes(-f.MinutesAgo), Latitude = f.Lat, Longitude = f.Lon, SpeedKmh = 60, Ignition = true,
        }).ToList(),
    };

    [Fact]
    public async Task CreateDevice_ReturnsTheKeyOnce_AndStoresOnlyItsHash()
    {
        var (_, device) = await ArrangeAsync();

        var stored = await T.NewContext().TrackingDevices.SingleAsync();

        device.ApiKey.Should().StartWith("fk_").And.HaveLength(43);
        stored.ApiKeyHash.Should().Be(TrackingRules.HashApiKey(device.ApiKey)).And.NotContain(device.ApiKey);
        device.Device.ApiKeyPrefix.Should().Be(device.ApiKey[..8]);
    }

    [Fact]
    public async Task CreateDevice_DuplicateIdentifier_Conflicts()
    {
        var (_, device) = await ArrangeAsync();
        var act = () => Services.TrackingDevices(T).CreateAsync(
            new TrackingDeviceRequest { TrackingProviderId = device.Device.TrackingProviderId, Identifier = "IMEI-1" }, default);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Ingest_ValidKeyWithoutSignedInUser_StoresPositionsInTheDevicesCompanyAndVehicle()
    {
        var (vehicleId, device) = await ArrangeAsync();
        T.CurrentUser.Anonymous();

        var result = await Services.TrackingIngestion(T).IngestAsync(device.ApiKey, Positions((10, -25.43m, -49.27m), (5, -25.44m, -49.20m)), default);

        result.Accepted.Should().Be(2);
        var positions = await T.NewContext().VehiclePositions.IgnoreQueryFilters().ToListAsync();
        positions.Should().HaveCount(2).And.OnlyContain(p => p.CompanyId == _company.Id && p.VehicleId == vehicleId);
    }

    [Fact]
    public async Task Ingest_SameFixTwice_IsCountedAsDuplicate()
    {
        var (_, device) = await ArrangeAsync();
        var request = Positions((5, -25.43m, -49.27m));
        await Services.TrackingIngestion(T).IngestAsync(device.ApiKey, request, default);

        var second = await Services.TrackingIngestion(T).IngestAsync(device.ApiKey, request, default);

        second.Accepted.Should().Be(0);
        second.Duplicates.Should().Be(1);
    }

    [Fact]
    public async Task Ingest_InvalidFixes_AreRejectedWithReason()
    {
        var (_, device) = await ArrangeAsync();

        var result = await Services.TrackingIngestion(T).IngestAsync(device.ApiKey, Positions((5, 95m, 10m), (5, 0m, 0m), (-60, -25m, -49m), (1, -25.4m, -49.2m)), default);

        result.Accepted.Should().Be(1);
        result.Rejected.Select(r => r.Reason).Should().BeEquivalentTo("coordenadas fora do intervalo", "coordenada 0,0 (sem sinal de GPS)", "data no futuro");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fk_wrong")]
    [InlineData("not-a-key")]
    public async Task Ingest_MissingOrWrongKey_IsUnauthorized(string? key)
    {
        await ArrangeAsync();
        var act = () => Services.TrackingIngestion(T).IngestAsync(key, Positions((5, -25.4m, -49.2m)), default);
        await act.Should().ThrowAsync<AuthenticationFailedException>();
    }

    [Fact]
    public async Task Ingest_AfterKeyRotationOrDeletion_OldKeyStopsWorking()
    {
        var (_, device) = await ArrangeAsync();
        var rotated = await Services.TrackingDevices(T).RotateKeyAsync(device.Device.Id, default);

        var old = () => Services.TrackingIngestion(T).IngestAsync(device.ApiKey, Positions((5, -25.4m, -49.2m)), default);
        await old.Should().ThrowAsync<AuthenticationFailedException>();
        (await Services.TrackingIngestion(T).IngestAsync(rotated.ApiKey, Positions((5, -25.4m, -49.2m)), default)).Accepted.Should().Be(1);

        await Services.TrackingDevices(T).DeleteAsync(device.Device.Id, default);
        var deleted = () => Services.TrackingIngestion(T).IngestAsync(rotated.ApiKey, Positions((4, -25.4m, -49.2m)), default);
        await deleted.Should().ThrowAsync<AuthenticationFailedException>();
    }

    [Fact]
    public async Task Link_SecondDeviceOnTheSameVehicle_EndsThePreviousLink()
    {
        var (vehicleId, first) = await ArrangeAsync();
        var second = await Services.TrackingDevices(T).CreateAsync(
            new TrackingDeviceRequest { TrackingProviderId = first.Device.TrackingProviderId, Identifier = "IMEI-2" }, default);

        await Services.TrackingDevices(T).LinkAsync(second.Device.Id, new LinkVehicleRequest(vehicleId), default);

        var devices = await Services.TrackingDevices(T).ListAsync(default);
        devices.Single(d => d.Identifier == "IMEI-1").VehicleId.Should().BeNull();
        devices.Single(d => d.Identifier == "IMEI-2").VehicleId.Should().Be(vehicleId);
        (await T.NewContext().VehicleDevices.CountAsync()).Should().Be(2, "history is kept, the first link is only ended");
    }

    [Fact]
    public async Task Link_InactiveVehicle_IsRefused()
    {
        var (_, device) = await ArrangeAsync(link: false);
        var inactive = await Scenario.VehicleAsync(T, 1, r => r with { Status = VehicleStatus.Inactive });
        var act = () => Services.TrackingDevices(T).LinkAsync(device.Device.Id, new LinkVehicleRequest(inactive.Id), default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Fleet_ShowsTheLastFixAndConnectionStatus_OnlyForTheCurrentCompany()
    {
        var (vehicleId, device) = await ArrangeAsync();
        await Services.TrackingIngestion(T).IngestAsync(device.ApiKey, Positions((30, -25.40m, -49.20m), (3, -25.45m, -49.10m)), default);

        var fleet = await Services.TrackingQueries(T).FleetAsync(default);
        var other = await T.AddCompanyAsync("11444777000161", "Outra");
        T.SignInAs(other, SystemRoles.Administrator);
        var otherFleet = await Services.TrackingQueries(T).FleetAsync(default);

        var location = fleet.Single();
        location.VehicleId.Should().Be(vehicleId);
        location.Latitude.Should().Be(-25.45m);
        location.Status.Should().Be(TrackingConnectionStatus.Online);
        otherFleet.Should().BeEmpty();
    }

    [Fact]
    public async Task Track_PeriodLongerThanAWeek_IsRefused()
    {
        var (vehicleId, _) = await ArrangeAsync();
        var act = () => Services.TrackingQueries(T).TrackAsync(vehicleId, T.Clock.UtcNow.AddDays(-8), T.Clock.UtcNow, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Track_ReturnsTheRouteInOrder()
    {
        var (vehicleId, device) = await ArrangeAsync();
        await Services.TrackingIngestion(T).IngestAsync(device.ApiKey, Positions((20, -25.40m, -49.20m), (10, -25.41m, -49.19m), (1, -25.42m, -49.18m)), default);

        var track = await Services.TrackingQueries(T).TrackAsync(vehicleId, null, null, default);

        track.Count.Should().Be(3);
        track.Points.Select(p => p.Latitude).Should().Equal(-25.40m, -25.41m, -25.42m);
        track.MaxSpeedKmh.Should().Be(60m);
    }

    public void Dispose() => T.Dispose();
}

public class TrackingRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, TrackingConnectionStatus.NeverConnected)]
    [InlineData(5, TrackingConnectionStatus.Online)]
    [InlineData(60, TrackingConnectionStatus.Recent)]
    [InlineData(60 * 25, TrackingConnectionStatus.Offline)]
    public void Status_ByAgeOfTheLastFix(int? minutesAgo, TrackingConnectionStatus expected) =>
        TrackingRules.Status(minutesAgo is null ? null : Now.AddMinutes(-minutesAgo.Value), Now).Should().Be(expected);

    [Fact]
    public void Reject_TooOldOrTooFast()
    {
        TrackingRules.Reject(new PositionInput(Now.AddDays(-31), -25m, -49m, 50m, null, null, null), Now).Should().Be("posição muito antiga");
        TrackingRules.Reject(new PositionInput(Now, -25m, -49m, 400m, null, null, null), Now).Should().Be("velocidade fora do intervalo");
        TrackingRules.Reject(new PositionInput(Now, -25m, -49m, 80m, 90, true, 1000), Now).Should().BeNull();
    }

    [Fact]
    public void NewApiKey_IsRandomAndHashIsStable()
    {
        var a = TrackingRules.NewApiKey();
        var b = TrackingRules.NewApiKey();
        a.Should().NotBe(b);
        TrackingRules.HashApiKey(a).Should().Be(TrackingRules.HashApiKey(a)).And.HaveLength(64);
    }
}
