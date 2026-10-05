using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Fleet.Api.Tests;

/// <summary>Alerts, notifications and automation over the real HTTP pipeline: permissions, audiences and tenant isolation.</summary>
public class IntelligenceApiTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Creates an overdue expense, runs the scan as admin A and returns the id of the resulting alert.</summary>
    private async Task<Guid> OverdueExpenseAlertAsync(HttpClient admin)
    {
        var categories = await JsonAsync(await admin.GetAsync("/api/v1/expense-categories?includeInactive=true"));
        var categoryId = categories.EnumerateArray().Single(c => c.GetProperty("code").GetString() == "INSURANCE").GetProperty("id").GetGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var description = $"Seguro {Guid.NewGuid():N}";
        (await admin.PostAsJsonAsync("/api/v1/expenses", new
        {
            expenseCategoryId = categoryId, description, expenseDate = today.AddDays(-30), dueDate = today.AddDays(-10), amount = 300m,
        })).EnsureSuccessStatusCode();
        (await admin.PostAsync("/api/v1/automation/run", null)).EnsureSuccessStatusCode();

        var alerts = await JsonAsync(await admin.GetAsync($"/api/v1/alerts?search={description}"));
        return alerts.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Alerts_RequireAuthentication()
    {
        (await factory.CreateClient().GetAsync("/api/v1/alerts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateClient().GetAsync("/api/v1/notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/alerts", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/alerts/summary", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/notifications", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/automation/rules", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.DriverA, "/api/v1/alerts", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.DriverA, "/api/v1/notifications", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.OperationsA, "/api/v1/dashboard/attention", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.AdminA, "/api/v1/automation/rules", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.AdminA, "/api/v1/automation/catalog", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/analytics/fleet-performance", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/analytics/recurring-problems", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/analytics/insights", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/search?q=abc", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.DriverA, "/api/v1/analytics/fleet-performance", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.DriverA, "/api/v1/search?q=abc", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.DriverA, "/api/v1/analytics/insights", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/tracking/fleet", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.DriverA, "/api/v1/tracking/fleet", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/tracking/devices", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/integrations", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.AdminA, "/api/v1/integrations", HttpStatusCode.OK)]
    public async Task Endpoints_FollowThePermissionsOfEachRole(string user, string url, HttpStatusCode expected)
    {
        var client = await factory.CreateSignedInClientAsync(user);
        (await client.GetAsync(url)).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Run_RequiresAutomationManage()
    {
        var viewer = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);
        (await viewer.PostAsync("/api/v1/automation/run", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Alert_OfAnotherCompany_Is404()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var alertId = await OverdueExpenseAlertAsync(admin);

        var otherCompany = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminB);
        (await otherCompany.GetAsync($"/api/v1/alerts/{alertId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherCompany.PostAsJsonAsync($"/api/v1/alerts/{alertId}/status", new { status = "Resolved" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Alert_OutsideTheReadersAudience_Is404()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var alertId = await OverdueExpenseAlertAsync(admin);

        // Maintenance role: alerts.view but no finance.view → a finance alert does not exist for them.
        var maintenance = await factory.CreateSignedInClientAsync(FleetApiFactory.MaintenanceA);
        (await maintenance.GetAsync($"/api/v1/alerts/{alertId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewer_SeesButCannotHandleAlerts()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var alertId = await OverdueExpenseAlertAsync(admin);

        var viewer = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);
        var alert = await JsonAsync(await viewer.GetAsync($"/api/v1/alerts/{alertId}"));
        alert.GetProperty("nextStatuses").GetArrayLength().Should().Be(0);
        (await viewer.PostAsJsonAsync($"/api/v1/alerts/{alertId}/status", new { status = "Resolved" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dismiss_WithoutReason_Is400WithFieldError()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var alertId = await OverdueExpenseAlertAsync(admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/alerts/{alertId}/status", new { status = "Dismissed" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("notes", out _).Should().BeTrue();
    }

    [Fact]
    public async Task VehicleHealth_UnknownOrForeignVehicle_Is404()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        (await admin.GetAsync($"/api/v1/vehicles/{Guid.NewGuid()}/health")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Compare_WithoutVehicles_Is422()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        (await admin.GetAsync("/api/v1/analytics/compare")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task FleetPerformance_InvertedPeriod_Is400()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        (await admin.GetAsync("/api/v1/analytics/fleet-performance?from=2026-10-10&to=2026-10-01")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Assistant_RequiresPermission_AndAnswersWithCalculatedModeWithoutProvider()
    {
        var driver = await factory.CreateSignedInClientAsync(FleetApiFactory.DriverA);
        (await driver.PostAsJsonAsync("/api/v1/assistant/ask", new { question = "Quanto gastamos este mês?" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var viewer = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);
        var status = await JsonAsync(await viewer.GetAsync("/api/v1/assistant/status"));
        status.GetProperty("aiEnabled").GetBoolean().Should().BeFalse();

        var response = await viewer.PostAsJsonAsync("/api/v1/assistant/ask", new { question = "O que merece atenção agora?" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("mode").GetString().Should().Be("Calculated");
    }

    [Fact]
    public async Task Assistant_EmptyQuestion_Is400()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        (await admin.PostAsJsonAsync("/api/v1/assistant/ask", new { question = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TrackingIngest_AuthenticatesByDeviceKey_AndPositionsAppearOnlyInThatCompany()
    {
        var anonymous = factory.CreateClient();
        var body = new { positions = new[] { new { recordedAt = DateTime.UtcNow.AddMinutes(-1), latitude = -25.43m, longitude = -49.27m, speedKmh = 50m } } };
        (await anonymous.PostAsJsonAsync("/api/v1/tracking/ingest", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var provider = await JsonAsync(await admin.PostAsJsonAsync("/api/v1/tracking/providers", new { name = $"Prov {Guid.NewGuid():N}" }));
        var device = await JsonAsync(await admin.PostAsJsonAsync("/api/v1/tracking/devices",
            new { trackingProviderId = provider.GetProperty("id").GetGuid(), identifier = $"IMEI-{Guid.NewGuid():N}"[..20] }));
        var key = device.GetProperty("apiKey").GetString()!;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tracking/ingest") { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Device-Key", key);
        var response = await anonymous.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("accepted").GetInt32().Should().Be(1);

        // The key is never returned again by the listing.
        var list = await admin.GetStringAsync("/api/v1/tracking/devices");
        list.Should().NotContain(key);
    }
}
