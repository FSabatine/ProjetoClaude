using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Fleet.Api.Tests;

/// <summary>Phase 4 over the real HTTP pipeline: permissions per role, cost visibility, tenant isolation and the main flows.</summary>
public class FuelApiTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>The fixture database is shared by the tests of this class, so each test registers its own vehicle.</summary>
    private static async Task<Guid> CreateVehicleAsync(HttpClient admin, string plate, string renavam, string chassis, int km = 100_000)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/vehicles", new
        {
            licensePlate = plate, renavam, chassis, manufacturer = "Volvo", model = "FH 540", manufacturingYear = 2022, modelYear = 2022,
            type = "TruckTractor", fuelType = "DieselS10", currentOdometerKm = km, status = "Available",
            fuelTankCapacity = 400, expectedConsumption = 5.0,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> DieselAsync(HttpClient client)
    {
        var types = await JsonAsync(await client.GetAsync("/api/v1/fuel-types"));
        return types.EnumerateArray().Single(t => t.GetProperty("code").GetString() == "S10").GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> FuelAsync(HttpClient client, Guid vehicleId, Guid fuelTypeId, int km, decimal quantity,
        decimal price = 6m, decimal? total = null) =>
        client.PostAsJsonAsync("/api/v1/fuelings", new
        {
            vehicleId, fuelTypeId, odometerKm = km, quantity, unitPrice = price, totalAmount = total, isFullTank = true, paymentMethod = "FuelCard",
        });

    [Fact]
    public async Task Fuelings_RequireAuthentication()
    {
        (await factory.CreateClient().GetAsync("/api/v1/fuelings")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/fuelings", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/fuel/reports/costs", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.OperationsA, "/api/v1/fuel/reports/costs", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.OperationsA, "/api/v1/fuel/reports/consumption", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.MaintenanceA, "/api/v1/fuel/dashboard", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.DriverA, "/api/v1/fuelings", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.AdminA, "/api/v1/fuel/reports/prices", HttpStatusCode.OK)]
    public async Task Endpoints_FollowThePermissionsOfEachRole(string user, string url, HttpStatusCode expected)
    {
        var client = await factory.CreateSignedInClientAsync(user);
        (await client.GetAsync(url)).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Viewer_CannotRegisterOrConfigure()
    {
        var viewer = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);
        (await viewer.PostAsJsonAsync("/api/v1/fuelings", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsJsonAsync("/api/v1/fuel-stations", new { name = "Posto" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PutAsJsonAsync("/api/v1/fuel-settings", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Operations_RegistersFuelings_ButDoesNotSeeFleetSpending()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "DEF5G67", "00123456789", "9BSR6X400J1234567");
        var operations = await factory.CreateSignedInClientAsync(FleetApiFactory.OperationsA);
        var diesel = await DieselAsync(operations);

        var created = await FuelAsync(operations, vehicleId, diesel, 100_300, 150, 6.10m);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        (await JsonAsync(created)).GetProperty("totalAmount").GetDecimal().Should().Be(915.00m, "the author sees what they typed");

        var dashboard = await JsonAsync(await operations.GetAsync("/api/v1/fuel/dashboard"));
        dashboard.GetProperty("canSeeCosts").GetBoolean().Should().BeFalse();
        dashboard.GetProperty("totals").GetProperty("totalCost").ValueKind.Should().Be(JsonValueKind.Null);

        var adminView = await JsonAsync(await admin.GetAsync("/api/v1/fuel/dashboard"));
        adminView.GetProperty("totals").GetProperty("totalCost").GetDecimal().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Validation_UsesTheErrorContract()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "GHI8J90", "23456789013", "9BM958074AB123456");
        var diesel = await DieselAsync(admin);

        var zero = await FuelAsync(admin, vehicleId, diesel, 100_100, 0);
        zero.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(zero)).GetProperty("errors").GetProperty("quantity")[0].GetString().Should().Contain("maior que zero");

        var wrongTotal = await FuelAsync(admin, vehicleId, diesel, 100_100, 100, 6m, total: 700m);
        wrongTotal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(wrongTotal)).GetProperty("errors").GetProperty("totalAmount")[0].GetString().Should().Contain("600,00");

        var backwards = await FuelAsync(admin, vehicleId, diesel, 99_000, 100);
        backwards.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(backwards)).GetProperty("errors").GetProperty("odometerKm")[0].GetString().Should().Contain("100.000 km");
    }

    [Fact]
    public async Task EndToEnd_NormalFueling_ConsumptionAndAbnormalQuantity()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "JKL1M23", "34567890125", "9BVAG40D5KE765432");
        var diesel = await DieselAsync(admin);
        // Back-to-back test requests would trip the frequency rule; it is configurable (0 = off).
        (await admin.PutAsJsonAsync("/api/v1/fuel-settings", new
        {
            tankTolerancePercent = 5, priceDeviationPercent = 20, consumptionDeviationPercent = 20, minHoursBetweenFuelings = 0,
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // seção 53/56: 100,000 → 101,000 km with 200 L = 5.0 km/L, total calculated by the server.
        (await FuelAsync(admin, vehicleId, diesel, 100_000, 300)).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await JsonAsync(await FuelAsync(admin, vehicleId, diesel, 101_000, 200, 6.20m));
        second.GetProperty("totalAmount").GetDecimal().Should().Be(1_240m);
        second.GetProperty("consumption").GetDecimal().Should().Be(5.0m);
        second.GetProperty("status").GetString().Should().Be("Valid");

        var vehicle = await JsonAsync(await admin.GetAsync($"/api/v1/vehicles/{vehicleId}"));
        vehicle.GetProperty("currentOdometerKm").GetInt32().Should().Be(101_000);

        // seção 55: 500 L in a 400 L tank → saved, requires review.
        var suspicious = await JsonAsync(await FuelAsync(admin, vehicleId, diesel, 101_100, 500));
        suspicious.GetProperty("status").GetString().Should().Be("PendingReview");
        suspicious.GetProperty("anomalies")[0].GetProperty("type").GetString().Should().Be("ExcessiveQuantity");
        var id = suspicious.GetProperty("id").GetGuid();

        var pending = await JsonAsync(await admin.GetAsync("/api/v1/fuelings?status=PendingReview"));
        pending.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Contain(id);

        var reviewed = await admin.PostAsJsonAsync($"/api/v1/fuelings/{id}/review", new { reason = "Conferido com o cupom" });
        reviewed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(reviewed)).GetProperty("status").GetString().Should().Be("Valid");

        var fuel = await JsonAsync(await admin.GetAsync($"/api/v1/vehicles/{vehicleId}/fuel"));
        fuel.GetProperty("totals").GetProperty("fuelings").GetInt32().Should().Be(3);
        fuel.GetProperty("expectedConsumption").GetDecimal().Should().Be(5.0m);
    }

    [Fact]
    public async Task AuditHistory_IsAvailableForFuelingsAndWorkOrders()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "MNO1P23", "45678901236", "9BWAB45Z0CP123456");
        var fueling = await JsonAsync(await FuelAsync(admin, vehicleId, await DieselAsync(admin), 100_200, 100));

        var history = await admin.GetAsync($"/api/v1/audit/Fueling/{fueling.GetProperty("id").GetGuid()}");
        history.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(history)).GetArrayLength().Should().BeGreaterThan(0);
        (await admin.GetAsync($"/api/v1/audit/WorkOrder/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OtherCompany_Gets404()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "ABC1D23", "12345678900", "9BWZZZ377VT004251");
        var fueling = await JsonAsync(await FuelAsync(admin, vehicleId, await DieselAsync(admin), 100_200, 100));
        var id = fueling.GetProperty("id").GetGuid();

        var other = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminB);
        (await other.GetAsync($"/api/v1/fuelings/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsJsonAsync($"/api/v1/fuelings/{id}/cancel", new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.GetAsync($"/api/v1/vehicles/{vehicleId}/fuel")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
