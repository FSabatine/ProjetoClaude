using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Companies;
using Fleet.Domain.Users;
using Fleet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fleet.Api.Tests;

/// <summary>Real HTTP pipeline (auth, policies, middleware) over SQLite in-memory, seeded with two tenants.</summary>
public sealed class FleetApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "SenhaTeste2026";
    public const string AdminA = "admin@a.com";
    public const string ViewerA = "viewer@a.com";
    public const string AdminB = "admin@b.com";
    public const string OperationsA = "operacoes@a.com";
    public const string MaintenanceA = "manutencao@a.com";
    public const string DriverA = "motorista@a.com";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Fleet", "Server=unused");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-32-bytes-min!");
        // TestServer speaks plain http, where browsers/HttpClient would drop a Secure cookie.
        builder.UseSetting("Auth:RefreshCookie:Secure", "false");
        builder.UseSetting("Auth:RequestsPerMinutePerIp", "10000");
        builder.UseSetting("Jobs:DocumentExpirationScan:Enabled", "false");
        builder.UseSetting("Storage:LocalRootPath", Path.Combine(Path.GetTempPath(), "fleet-api-tests", Guid.NewGuid().ToString("N")));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FleetDbContext>>();
            services.AddDbContext<FleetDbContext>(o => o.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            db.Database.EnsureCreated();
            Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        });
    }

    private static void Seed(FleetDbContext db, IPasswordHasher hasher)
    {
        var companyA = NewCompany("Empresa A", "11222333000181");
        var companyB = NewCompany("Empresa B", "12ABC34501DE35");
        db.Companies.AddRange(companyA, companyB);
        db.Users.AddRange(
            NewUser(companyA, AdminA, SystemRoles.Administrator, hasher),
            NewUser(companyA, ViewerA, SystemRoles.Viewer, hasher),
            NewUser(companyA, OperationsA, SystemRoles.Operations, hasher),
            NewUser(companyA, MaintenanceA, SystemRoles.Maintenance, hasher),
            NewUser(companyA, DriverA, SystemRoles.Driver, hasher),
            NewUser(companyB, AdminB, SystemRoles.Administrator, hasher));
        db.SaveChangesAsync().GetAwaiter().GetResult();
    }

    private static Company NewCompany(string name, string cnpj) => new()
    {
        LegalName = name,
        Cnpj = cnpj,
        Address = new Address { Street = "Rua", Number = "1", Neighborhood = "Centro", City = "Curitiba", State = "PR", ZipCode = "80010000" },
    };

    private static User NewUser(Company company, string email, string role, IPasswordHasher hasher) => new()
    {
        Company = company,
        Name = email,
        Email = email,
        PasswordHash = hasher.Hash(Password),
        UserRoles = [new UserRole { RoleId = SystemRoles.All.Single(r => r.Key == role).Id }],
    };

    public async Task<HttpClient> CreateSignedInClientAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    public sealed record LoginBody(string AccessToken, DateTime ExpiresAt);
}
