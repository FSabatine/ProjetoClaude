using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Companies;
using Fleet.Domain.Users;
using Fleet.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.TestSupport;

public sealed class TestCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public Guid? CompanyId { get; set; }
    public HashSet<string> PermissionSet { get; set; } = [];
    public IReadOnlySet<string> Permissions => PermissionSet;
    public string? TraceId => "test-trace";

    public void Anonymous()
    {
        UserId = null;
        CompanyId = null;
        PermissionSet = [];
    }
}

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
    public DateOnly Today => ToBusinessDate(UtcNow);
    public DateTime ToBusinessDateTime(DateTime utc) => DateTime.SpecifyKind(utc.AddHours(-3), DateTimeKind.Unspecified);
    public DateOnly ToBusinessDate(DateTime utc) => DateOnly.FromDateTime(ToBusinessDateTime(utc));
    public DateTime StartOfBusinessDayUtc(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(3);
}

public sealed class InMemoryFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        Files[key] = buffer.ToArray();
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct) =>
        Files.TryGetValue(key, out var bytes) ? Task.FromResult<Stream>(new MemoryStream(bytes)) : throw new FileNotFoundException(key);

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Real SQL (SQLite in-memory): unique indexes, filters and FKs behave like the database (ADR-004).
/// Each test gets its own database.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestCurrentUser CurrentUser { get; } = new();
    public FakeClock Clock { get; } = new();
    public InMemoryFileStorage Storage { get; } = new();
    public FleetDbContext Db { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Db = NewContext();
        Db.Database.EnsureCreated();
    }

    /// <summary>A fresh context on the same database — use it to assert what was really persisted.</summary>
    public FleetDbContext NewContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<FleetDbContext>().UseSqlite(_connection).AddInterceptors(interceptors).Options, CurrentUser, Clock);

    public async Task<Company> AddCompanyAsync(string cnpj = "11222333000181", string name = "Empresa Teste")
    {
        var company = new Company
        {
            LegalName = name,
            Cnpj = cnpj,
            Address = new Address { Street = "Rua A", Number = "1", Neighborhood = "Centro", City = "Curitiba", State = "PR", ZipCode = "80010000" },
        };
        Db.Companies.Add(company);
        await Db.SaveChangesAsync();
        return company;
    }

    public async Task<User> AddUserAsync(Company company, string email, string roleKey, string passwordHash = "hash")
    {
        var user = new User
        {
            CompanyId = company.Id,
            Name = email,
            Email = email,
            PasswordHash = passwordHash,
            UserRoles = [new UserRole { RoleId = SystemRoles.All.Single(r => r.Key == roleKey).Id }],
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    /// <summary>Acts as a user of <paramref name="company"/> holding the permissions of the given system role.</summary>
    public void SignInAs(Company company, string roleKey, Guid? userId = null)
    {
        CurrentUser.UserId = userId ?? Guid.NewGuid();
        CurrentUser.CompanyId = company.Id;
        CurrentUser.PermissionSet = SystemRoles.All.Single(r => r.Key == roleKey).Permissions.ToHashSet();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
