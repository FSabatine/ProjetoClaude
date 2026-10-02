using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Fleet.Api.Tests;

/// <summary>Phase 5 over the real HTTP pipeline: permissions per role, cost visibility, tenant isolation and the main flow.</summary>
public class TireApiTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task<Guid> CreateVehicleAsync(HttpClient admin, string plate, string renavam, string chassis)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/vehicles", new
        {
            licensePlate = plate, renavam, chassis, manufacturer = "Volvo", model = "FH 540", manufacturingYear = 2022, modelYear = 2022,
            type = "Truck", fuelType = "DieselS10", currentOdometerKm = 80_000, status = "Available",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>Vehicle with the default "Caminhão toco 4x2" layout + one tire in stock.</summary>
    private static async Task<(Guid VehicleId, Guid TireId)> ArrangeAsync(HttpClient admin, string plate, string renavam, string chassis, string size)
    {
        var vehicleId = await CreateVehicleAsync(admin, plate, renavam, chassis);
        var layouts = await JsonAsync(await admin.GetAsync("/api/v1/tire-layouts?target=Vehicle"));
        var layoutId = layouts.EnumerateArray().Single(l => l.GetProperty("name").GetString() == "Caminhão toco 4x2").GetProperty("id").GetGuid();
        (await admin.PutAsJsonAsync($"/api/v1/vehicles/{vehicleId}/tires/layout", new { layoutId })).StatusCode.Should().Be(HttpStatusCode.OK);

        var model = await admin.PostAsJsonAsync("/api/v1/tire-models", new { brand = "Bridgestone", name = $"R268 {plate}", size, originalTreadDepthMm = 16 });
        model.StatusCode.Should().Be(HttpStatusCode.Created, await model.Content.ReadAsStringAsync());
        var tire = await admin.PostAsJsonAsync("/api/v1/tires", new
        {
            tireModelId = (await JsonAsync(model)).GetProperty("id").GetGuid(), purchasePrice = 2_100, dot = "DOT XX 1225",
        });
        tire.StatusCode.Should().Be(HttpStatusCode.Created, await tire.Content.ReadAsStringAsync());
        return (vehicleId, (await JsonAsync(tire)).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Tires_RequireAuthentication() =>
        (await factory.CreateClient().GetAsync("/api/v1/tires")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Theory]
    [InlineData(FleetApiFactory.ViewerA, "GET", "/api/v1/tires", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "GET", "/api/v1/tires/dashboard", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "POST", "/api/v1/tires", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.ViewerA, "PUT", "/api/v1/tire-settings", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.ViewerA, "GET", "/api/v1/tires/reports/costs", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.OperationsA, "POST", "/api/v1/tires/00000000-0000-0000-0000-000000000001/install", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.OperationsA, "POST", "/api/v1/tires/00000000-0000-0000-0000-000000000001/remove", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.OperationsA, "POST", "/api/v1/tire-rotations", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.OperationsA, "POST", "/api/v1/tires/00000000-0000-0000-0000-000000000001/dispose", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.OperationsA, "GET", "/api/v1/tires/reports/lifecycle", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.MaintenanceA, "GET", "/api/v1/tires/reports/costs", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.DriverA, "GET", "/api/v1/tires", HttpStatusCode.Forbidden)]
    public async Task Endpoints_FollowThePermissionsOfEachRole(string user, string method, string url, HttpStatusCode expected)
    {
        var client = await factory.CreateSignedInClientAsync(user);
        var response = method == "GET" ? await client.GetAsync(url)
            : method == "PUT" ? await client.PutAsJsonAsync(url, new { })
            : await client.PostAsJsonAsync(url, new { });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task EndToEnd_InstallInspectAndSeeTheLayout_WithCostsHiddenFromOperations()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var (vehicleId, tireId) = await ArrangeAsync(admin, "DEF5G67", "00123456789", "9BSR6X400J1234567", "295/80R22.5");

        var installed = await admin.PostAsJsonAsync($"/api/v1/tires/{tireId}/install", new { vehicleId, positionCode = "2EE" });
        installed.StatusCode.Should().Be(HttpStatusCode.OK, await installed.Content.ReadAsStringAsync());
        (await JsonAsync(installed)).GetProperty("status").GetString().Should().Be("Installed");

        var layout = await JsonAsync(await admin.GetAsync($"/api/v1/vehicles/{vehicleId}/tires"));
        var position = layout.GetProperty("positions").EnumerateArray().Single(p => p.GetProperty("position").GetProperty("code").GetString() == "2EE");
        position.GetProperty("tire").GetProperty("installedOdometerKm").GetInt32().Should().Be(80_000);

        var operations = await factory.CreateSignedInClientAsync(FleetApiFactory.OperationsA);
        var inspection = await operations.PostAsJsonAsync($"/api/v1/tires/{tireId}/inspections", new
        {
            treadDepthMm = 12.5, pressure = 7.2, pressureUnit = "Bar", condition = "Good", wearPattern = "Normal",
        });
        inspection.StatusCode.Should().Be(HttpStatusCode.Created, await inspection.Content.ReadAsStringAsync());

        var seenByOperations = await JsonAsync(await operations.GetAsync($"/api/v1/tires/{tireId}"));
        seenByOperations.GetProperty("purchasePrice").ValueKind.Should().Be(JsonValueKind.Null);
        seenByOperations.GetProperty("costs").ValueKind.Should().Be(JsonValueKind.Null);
        seenByOperations.GetProperty("currentTreadDepthMm").GetDecimal().Should().Be(12.5m);
        var seenByAdmin = await JsonAsync(await admin.GetAsync($"/api/v1/tires/{tireId}"));
        seenByAdmin.GetProperty("costs").GetProperty("total").GetDecimal().Should().Be(2_100m);

        var history = await JsonAsync(await admin.GetAsync($"/api/v1/tires/{tireId}/history"));
        history.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        (await admin.GetAsync($"/api/v1/audit/Tire/{tireId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Install_ErrorsUseTheErrorContract()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var (vehicleId, tireId) = await ArrangeAsync(admin, "GHI8J90", "23456789013", "9BM958074AB123456", "275/80R22.5");

        var invalid = await admin.PostAsJsonAsync($"/api/v1/tires/{tireId}/install", new { vehicleId, positionCode = "7EE" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(invalid)).GetProperty("errors").GetProperty("positionCode")[0].GetString().Should().Contain("não existe");

        (await admin.PostAsJsonAsync($"/api/v1/tires/{tireId}/install", new { vehicleId, positionCode = "1E" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var secondTire = await admin.PostAsJsonAsync("/api/v1/tires", new
        {
            tireModelId = (await JsonAsync(await admin.GetAsync($"/api/v1/tires/{tireId}"))).GetProperty("tireModelId").GetGuid(),
        });
        var occupied = await admin.PostAsJsonAsync($"/api/v1/tires/{(await JsonAsync(secondTire)).GetProperty("id").GetGuid()}/install",
            new { vehicleId, positionCode = "1E" });
        occupied.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(occupied)).GetProperty("title").GetString().Should().Contain("Substituir");
    }

    [Fact]
    public async Task OtherCompany_Gets404()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var (vehicleId, tireId) = await ArrangeAsync(admin, "JKL1M23", "34567890125", "9BVAG40D5KE765432", "315/80R22.5");

        var other = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminB);
        (await other.GetAsync($"/api/v1/tires/{tireId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.GetAsync($"/api/v1/vehicles/{vehicleId}/tires")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsJsonAsync($"/api/v1/tires/{tireId}/dispose", new { reason = "EndOfLife" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var list = await JsonAsync(await other.GetAsync("/api/v1/tires"));
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().NotContain(tireId);
    }
}
