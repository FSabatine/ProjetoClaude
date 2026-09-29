using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Fleet.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fleet.Api.Tests;

public class ApiTests(FleetApiFactory factory) : IClassFixture<FleetApiFactory>
{
    private static object Vehicle(string plate, string renavam, string chassis) => new
    {
        licensePlate = plate,
        renavam,
        chassis,
        manufacturer = "Volvo",
        model = "FH 540",
        manufacturingYear = 2022,
        modelYear = 2022,
        type = "TruckTractor",
        fuelType = "DieselS10",
        currentOdometerKm = 0,
        status = "Available",
    };

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401ProblemInPortuguese()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/vehicles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await JsonAsync(response)).GetProperty("title").GetString().Should().Contain("sessão");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithGenericMessage()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = FleetApiFactory.AdminA, password = "errada" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await JsonAsync(response)).GetProperty("title").GetString().Should().StartWith("E-mail ou senha inválidos");
    }

    [Fact]
    public async Task Me_ReturnsProfileWithPermissions()
    {
        var client = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);

        var me = await JsonAsync(await client.GetAsync("/api/v1/auth/me"));

        me.GetProperty("companyName").GetString().Should().Be("Empresa A");
        me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Contain("vehicles.view").And.NotContain("vehicles.create");
    }

    [Fact]
    public async Task CreateVehicle_AsViewer_Returns403()
    {
        var client = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);

        var response = await client.PostAsJsonAsync("/api/v1/vehicles", Vehicle("VWR1A23", "12345678900", "9BWZZZ377VT004251"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await JsonAsync(response)).GetProperty("title").GetString().Should().Contain("permissão");
    }

    [Fact]
    public async Task Vehicle_OfAnotherCompany_Returns404AndIsNotListed()
    {
        var clientB = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminB);
        var created = await clientB.PostAsJsonAsync("/api/v1/vehicles", Vehicle("BBB9B99", "00123456789", "9BSR6X400J1234567"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await JsonAsync(created)).GetProperty("id").GetString();

        var clientA = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        (await clientA.GetAsync($"/api/v1/vehicles/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await clientA.DeleteAsync($"/api/v1/vehicles/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var list = await JsonAsync(await clientA.GetAsync("/api/v1/vehicles?search=BBB9B99"));
        list.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task CreateVehicle_Invalid_Returns400WithCamelCaseFieldErrors()
    {
        var client = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);

        var response = await client.PostAsJsonAsync("/api/v1/vehicles", new { licensePlate = "X", type = "TruckTractor" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await JsonAsync(response)).GetProperty("errors");
        errors.TryGetProperty("licensePlate", out _).Should().BeTrue();
        errors.TryGetProperty("renavam", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateDriver_InvalidNestedAddress_ReturnsNestedErrorKey()
    {
        var client = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);

        var response = await client.PostAsJsonAsync("/api/v1/drivers", new
        {
            fullName = "Teste", cpf = "529.982.247-25", birthDate = "1990-01-01", licenseNumber = "04512345678",
            licenseCategory = "E", licenseExpiresOn = "2030-01-01", address = new { zipCode = "123" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("address.zipCode", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateVehicle_UnknownEnumValue_Returns400()
    {
        var client = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);

        var response = await client.PostAsJsonAsync("/api/v1/vehicles", new { status = "Banana" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("status", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateVehicle_Duplicate_Returns409WithField()
    {
        var client = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);
        var vehicle = Vehicle("DUP1A23", "23456789013", "9BM958074AB123456");
        (await client.PostAsJsonAsync("/api/v1/vehicles", vehicle)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync("/api/v1/vehicles", vehicle);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("licensePlate", out _).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshCookie_RotatesAndDetectsReplay()
    {
        var client = factory.CreateClient(); // keeps cookies between requests
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = FleetApiFactory.AdminB, password = FleetApiFactory.Password });
        var setCookie = login.Headers.GetValues("Set-Cookie").Single();
        setCookie.Should().Contain("httponly").And.Contain("samesite=strict").And.Contain("path=/api/v1/auth");
        var originalCookie = setCookie.Split(';')[0];

        var refreshed = await client.PostAsync("/api/v1/auth/refresh", null);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(refreshed)).GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();

        var attacker = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var replay = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        replay.Headers.Add("Cookie", originalCookie);
        (await attacker.SendAsync(replay)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Reuse detection revoked the legitimate session as well.
        (await client.PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_InvalidatesRefreshCookie()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new { email = FleetApiFactory.ViewerA, password = FleetApiFactory.Password });

        (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.PostAsync("/api/v1/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Responses_CarrySecurityHeadersAndTraceId()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.Contains("X-Trace-Id").Should().BeTrue();
    }

    [Fact]
    public async Task Dashboard_ReturnsIndicatorsForOwnCompany()
    {
        var client = await factory.CreateSignedInClientAsync(FleetApiFactory.ViewerA);

        var dashboard = await JsonAsync(await client.GetAsync("/api/v1/dashboard"));

        dashboard.GetProperty("indicators").TryGetProperty("activeVehicles", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Companies_ListIsPlatformOnly_CurrentIsAllowed()
    {
        var client = await factory.CreateSignedInClientAsync(FleetApiFactory.AdminA);

        (await client.GetAsync("/api/v1/companies")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var current = await JsonAsync(await client.GetAsync("/api/v1/companies/current"));
        current.GetProperty("legalName").GetString().Should().Be("Empresa A");
    }

    /// <summary>A01 guard: every action must declare its access rule explicitly.</summary>
    [Fact]
    public void EveryControllerAction_DeclaresPermissionOrAnonymousAccess()
    {
        var actions = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute));

        var unprotected = actions
            .Where(m => !m.IsDefined(typeof(HasPermissionAttribute)) && !m.IsDefined(typeof(AllowAnonymousAttribute)) &&
                        !m.IsDefined(typeof(AuthorizeAttribute)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        actions.Should().NotBeEmpty();
        unprotected.Should().BeEmpty();
    }
}
