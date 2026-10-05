using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Fleet.Api.Tests;

/// <summary>Phase 6 over the real HTTP pipeline: permissions per role, cost visibility and tenant isolation.</summary>
public class FinanceApiTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task<Guid> InsuranceCategoryAsync(HttpClient client)
    {
        var categories = await JsonAsync(await client.GetAsync("/api/v1/expense-categories?includeInactive=true"));
        return categories.EnumerateArray().Single(c => c.GetProperty("code").GetString() == "INSURANCE").GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> CreateExpenseAsync(HttpClient client, Guid categoryId, decimal amount = 500m) =>
        client.PostAsJsonAsync("/api/v1/expenses", new
        {
            expenseCategoryId = categoryId, description = "Seguro anual", expenseDate = DateOnly.FromDateTime(DateTime.UtcNow), amount,
        });

    [Fact]
    public async Task Expenses_RequireAuthentication()
    {
        (await factory.CreateClient().GetAsync("/api/v1/expenses")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/expenses", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/finance/ranking", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.ViewerA, "/api/v1/finance/dashboard", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.OperationsA, "/api/v1/expenses", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.MaintenanceA, "/api/v1/expenses", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.DriverA, "/api/v1/expenses", HttpStatusCode.Forbidden)]
    [InlineData(FleetApiFactory.AdminA, "/api/v1/expense-categories", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.AdminA, "/api/v1/cost-centers", HttpStatusCode.OK)]
    [InlineData(FleetApiFactory.AdminA, "/api/v1/budgets/vs-actual?year=2026", HttpStatusCode.OK)]
    public async Task Endpoints_FollowThePermissionsOfEachRole(string user, string url, HttpStatusCode expected)
    {
        var client = await factory.CreateSignedInClientAsync(user);
        (await client.GetAsync(url)).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Viewer_CannotCreateOrConfigure()
    {
        var viewer = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);
        (await viewer.PostAsJsonAsync("/api/v1/expenses", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsJsonAsync("/api/v1/expense-categories", new { name = "X", code = "X" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsJsonAsync("/api/v1/cost-centers", new { code = "X", name = "X" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_SeesNoAmountsWithoutViewCosts()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var categoryId = await InsuranceCategoryAsync(admin);
        var created = await JsonAsync(await CreateExpenseAsync(admin, categoryId, 777m));
        created.GetProperty("amount").GetDecimal().Should().Be(777m);

        var viewer = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);
        var viewerGet = await JsonAsync(await viewer.GetAsync($"/api/v1/expenses/{created.GetProperty("id").GetGuid()}"));
        viewerGet.GetProperty("amount").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task SystemCategory_RejectsManualExpense()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var categories = await JsonAsync(await admin.GetAsync("/api/v1/expense-categories?includeInactive=true"));
        var fuelCategoryId = categories.EnumerateArray().Single(c => c.GetProperty("code").GetString() == "FUEL").GetProperty("id").GetGuid();

        var response = await CreateExpenseAsync(admin, fuelCategoryId);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task EndToEnd_CreatePayAndCancel()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var categoryId = await InsuranceCategoryAsync(admin);
        var created = await JsonAsync(await CreateExpenseAsync(admin, categoryId, 1000m));
        created.GetProperty("status").GetString().Should().Be("Pending");
        var id = created.GetProperty("id").GetGuid();

        var paid = await JsonAsync(await admin.PostAsJsonAsync($"/api/v1/expenses/{id}/payment", new
        {
            paidAmount = 1000m, paymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        }));
        paid.GetProperty("status").GetString().Should().Be("Paid");

        var secondExpense = await JsonAsync(await CreateExpenseAsync(admin, categoryId, 50m));
        var cancelled = await JsonAsync(await admin.PostAsJsonAsync(
            $"/api/v1/expenses/{secondExpense.GetProperty("id").GetGuid()}/cancel", new { reason = "Lançamento de teste" }));
        cancelled.GetProperty("status").GetString().Should().Be("Cancelled");
    }

    [Fact]
    public async Task OtherCompany_Gets404()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var categoryId = await InsuranceCategoryAsync(admin);
        var created = await JsonAsync(await CreateExpenseAsync(admin, categoryId));
        var id = created.GetProperty("id").GetGuid();

        var other = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminB);
        (await other.GetAsync($"/api/v1/expenses/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuditHistory_IsAvailableForExpenses()
    {
        var admin = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var categoryId = await InsuranceCategoryAsync(admin);
        var created = await JsonAsync(await CreateExpenseAsync(admin, categoryId));

        var history = await admin.GetAsync($"/api/v1/audit/Expense/{created.GetProperty("id").GetGuid()}");
        history.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(history)).GetArrayLength().Should().BeGreaterThan(0);
    }
}
