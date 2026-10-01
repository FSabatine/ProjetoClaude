using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Fleet.Api.Tests;

/// <summary>Phase 3 over the real HTTP pipeline: permissions per role, error contract and the end-to-end work order flow.</summary>
public class MaintenanceApiTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task<Guid> CreateVehicleAsync(HttpClient admin, string plate, string renavam, string chassis)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/vehicles", new
        {
            licensePlate = plate, renavam, chassis, manufacturer = "Volvo", model = "FH 540", manufacturingYear = 2022, modelYear = 2022,
            type = "TruckTractor", fuelType = "DieselS10", currentOdometerKm = 10_000, status = "Available",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Workshops_OperationsCannotManage_MaintenanceCan()
    {
        var operations = await factory.CreateSignedInClientAsync(FleetApiFactory.OperationsA);
        (await operations.PostAsJsonAsync("/api/v1/workshops", new { name = "Oficina X" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var maintenance = await factory.CreateSignedInClientAsync(FleetApiFactory.MaintenanceA);
        var created = await maintenance.PostAsJsonAsync("/api/v1/workshops", new { name = "Oficina X" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task MaintenanceRequest_OperationsCanCreate_ButCannotApprove()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "ABC1D23", "12345678900", "9BWZZZ377VT004251");
        var operations = await factory.CreateSignedInClientAsync(FleetApiFactory.OperationsA);

        var created = await operations.PostAsJsonAsync("/api/v1/maintenance-requests", new
        {
            vehicleId, maintenanceType = "Corrective", priority = "High", description = "Barulho no freio",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var requestId = (await JsonAsync(created)).GetProperty("id").GetGuid();

        (await operations.PostAsync($"/api/v1/maintenance-requests/{requestId}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var maintenance = await factory.CreateSignedInClientAsync(FleetApiFactory.MaintenanceA);
        var approved = await maintenance.PostAsync($"/api/v1/maintenance-requests/{requestId}/approve", null);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(approved)).GetProperty("status").GetString().Should().Be("Converted");
    }

    [Fact]
    public async Task WorkOrder_FullLifecycle_MovesVehicleUnderMaintenanceAndBackToAvailable()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "DEF5G67", "00123456789", "9BSR6X400J1234567");

        var created = await admin.PostAsJsonAsync("/api/v1/work-orders", new
        {
            vehicleId, type = "Corrective", priority = "Medium", description = "Revisão geral",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var workOrder = await JsonAsync(created);
        var id = workOrder.GetProperty("id").GetGuid();
        workOrder.GetProperty("number").GetString().Should().StartWith("OS-");

        (await admin.PostAsJsonAsync($"/api/v1/work-orders/{id}/status", new { status = "Approved" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync($"/api/v1/work-orders/{id}/status", new { status = "InProgress" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var vehicleDuring = await JsonAsync(await admin.GetAsync($"/api/v1/vehicles/{vehicleId}"));
        vehicleDuring.GetProperty("status").GetString().Should().Be("UnderMaintenance");

        var part = await admin.PostAsJsonAsync($"/api/v1/work-orders/{id}/parts", new { partName = "Filtro de ar", quantity = 1, unitCost = 90 });
        part.StatusCode.Should().Be(HttpStatusCode.Created);

        var completed = await admin.PostAsJsonAsync($"/api/v1/work-orders/{id}/status", new { status = "Completed", resolution = "Revisão concluída" });
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(completed)).GetProperty("totalCost").GetDecimal().Should().Be(90);

        var vehicleAfter = await JsonAsync(await admin.GetAsync($"/api/v1/vehicles/{vehicleId}"));
        vehicleAfter.GetProperty("status").GetString().Should().Be("Available");

        var history = await JsonAsync(await admin.GetAsync($"/api/v1/vehicles/{vehicleId}/history"));
        history.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("type").GetString()).Should().Contain("WorkOrderCompleted");
    }

    [Fact]
    public async Task WorkOrder_CrossTenantAccess_Returns404()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicleId = await CreateVehicleAsync(admin, "GHI8J90", "23456789013", "9BM958074AB123456");
        var created = await admin.PostAsJsonAsync("/api/v1/work-orders", new { vehicleId, type = "Corrective", priority = "Low", description = "Lâmpada queimada" });
        var id = (await JsonAsync(created)).GetProperty("id").GetGuid();

        var otherTenant = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminB);
        (await otherTenant.GetAsync($"/api/v1/work-orders/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DriverRole_HasNoMaintenanceAccess()
    {
        var driver = await factory.CreateSignedInClientAsync(FleetApiFactory.DriverA);

        (await driver.GetAsync("/api/v1/work-orders")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await driver.GetAsync("/api/v1/maintenance-plans")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
