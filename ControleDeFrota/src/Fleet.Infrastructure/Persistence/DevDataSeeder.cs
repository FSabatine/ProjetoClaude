using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Companies;
using Fleet.Domain.Drivers;
using Fleet.Domain.Implements;
using Fleet.Domain.Users;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fleet.Infrastructure.Persistence;

/// <summary>
/// DEVELOPMENT ONLY. Creates demo companies/users (and optionally sample fleet data) when the database is empty,
/// so the app can be used right after the first run. Credentials are documented in docs/README.md.
/// </summary>
public sealed class DevDataSeeder(FleetDbContext db, IPasswordHasher passwordHasher, IClock clock, ILogger<DevDataSeeder> logger)
{
    public const string DevPassword = "FrotaDev!2026";

    public async Task SeedAsync(bool includeSampleData, CancellationToken ct = default)
    {
        if (await db.Companies.IgnoreQueryFilters().AnyAsync(ct)) return;

        var main = NewCompany("Rodoxisto Transportes Ltda (Dev)", "Rodoxisto (Dev)", "11222333000181", "Curitiba", "PR", "80010000");
        var other = NewCompany("Transportadora Exemplo Ltda", "Exemplo Transportes", "12ABC34501DE35", "São Paulo", "SP", "01001000");
        db.Companies.AddRange(main, other);

        db.Users.AddRange(
            NewUser(main, "Administrador da Plataforma", "admin@frota.local", SystemRoles.PlatformAdministrator),
            NewUser(main, "Gestora de Frota", "gestor@frota.local", SystemRoles.FleetManager),
            NewUser(main, "Consulta", "consulta@frota.local", SystemRoles.Viewer),
            NewUser(other, "Operação Exemplo", "operacao@exemplo.local", SystemRoles.Administrator));

        if (includeSampleData) AddSampleFleet(main);

        await db.SaveChangesAsync(ct);
        logger.LogWarning("Development data seeded (demo users with password documented in docs/README.md)");
    }

    private void AddSampleFleet(Company company)
    {
        var today = clock.Today;
        db.Drivers.AddRange(
            NewDriver(company, "João Carlos Pereira", "123456789", "04512345678", LicenseCategory.E, today.AddYears(3), DriverStatus.Active),
            NewDriver(company, "Maria Aparecida Santos", "987654321", "05623456789", LicenseCategory.D, today.AddDays(12), DriverStatus.Active),
            NewDriver(company, "Paulo Roberto Lima", "456123789", "06734567890", LicenseCategory.E, today.AddDays(-10), DriverStatus.Active),
            NewDriver(company, "Ana Beatriz Rocha", "321654987", "07845678901", LicenseCategory.C, today.AddYears(2), DriverStatus.OnLeave));

        db.Vehicles.AddRange(
            NewVehicle(company, "RDX1A23", "1234567890", "9BSR6X400J1234567", "Scania", "R 450", 2022, VehicleType.TruckTractor, 185_430, VehicleStatus.Available),
            NewVehicle(company, "RDX2B34", "2345678901", "9BM958074AB123456", "Mercedes-Benz", "Actros 2651", 2023, VehicleType.TruckTractor, 98_120, VehicleStatus.OnTrip),
            NewVehicle(company, "RDX3C45", "3456789012", "9BVAG40D5KE765432", "Volvo", "FH 540", 2021, VehicleType.TruckTractor, 312_900, VehicleStatus.UnderMaintenance),
            NewVehicle(company, "ABC1234", "4567890123", "9BWZZZ377VT004251", "Volkswagen", "Amarok", 2020, VehicleType.Pickup, 76_540, VehicleStatus.Available));

        db.Implements.AddRange(
            NewImplement(company, "RDX4D56", "5678901234", "9EP07142RAA000123", "Randon", "Semirreboque Graneleiro", ImplementType.SemiTrailer, 32_000, CapacityUnit.Kg),
            NewImplement(company, "RDX5E67", "6789012345", "9A9SR1234AB098765", "Librelato", "Tanque 45 m³", ImplementType.Tanker, 45_000, CapacityUnit.Liters));
    }

    private static Company NewCompany(string legalName, string tradeName, string cnpj, string city, string state, string zip) => new()
    {
        LegalName = legalName,
        TradeName = tradeName,
        Cnpj = cnpj,
        Email = "contato@example.com",
        Address = new Address
        {
            Street = "Rua Exemplo", Number = "100", Neighborhood = "Centro", City = city, State = state, ZipCode = zip,
        },
    };

    private User NewUser(Company company, string name, string email, string roleKey) => new()
    {
        Company = company,
        Name = name,
        Email = email,
        PasswordHash = passwordHasher.Hash(DevPassword),
        PasswordChangedAt = clock.UtcNow,
        UserRoles = [new UserRole { RoleId = SystemRoles.All.Single(r => r.Key == roleKey).Id }],
    };

    private static Driver NewDriver(Company company, string name, string cpfBase, string license, LicenseCategory category,
        DateOnly licenseExpiresOn, DriverStatus status) => new()
    {
        CompanyId = company.Id,
        FullName = name,
        Cpf = WithMod11Digits(cpfBase, 2),
        BirthDate = new DateOnly(1985, 5, 20),
        Phone = "41999998888",
        LicenseNumber = license,
        LicenseCategory = category,
        LicenseExpiresOn = licenseExpiresOn,
        PerformsPaidActivity = true,
        Status = status,
    };

    private static Vehicle NewVehicle(Company company, string plate, string renavamBase, string chassis, string manufacturer,
        string model, short year, VehicleType type, int odometer, VehicleStatus status) => new()
    {
        CompanyId = company.Id,
        LicensePlate = plate,
        Renavam = WithRenavamDigit(renavamBase),
        Chassis = chassis,
        Manufacturer = manufacturer,
        Model = model,
        ManufacturingYear = year,
        ModelYear = year,
        Color = "Branco",
        Type = type,
        Category = type == VehicleType.Pickup ? VehicleCategory.Light : VehicleCategory.Heavy,
        FuelType = FuelType.DieselS10,
        CurrentOdometerKm = odometer,
        Status = status,
    };

    private static Implement NewImplement(Company company, string plate, string renavamBase, string chassis, string manufacturer,
        string model, ImplementType type, decimal capacity, CapacityUnit unit) => new()
    {
        CompanyId = company.Id,
        LicensePlate = plate,
        Renavam = WithRenavamDigit(renavamBase),
        Chassis = chassis,
        Manufacturer = manufacturer,
        Model = model,
        ManufacturingYear = 2022,
        ModelYear = 2022,
        Type = type,
        Capacity = capacity,
        CapacityUnit = unit,
    };

    // Sample documents need valid check digits; computing them keeps the seed self-evidently correct.
    private static string WithMod11Digits(string digits, int count)
    {
        for (var n = 0; n < count; n++)
        {
            var sum = 0;
            for (var i = 0; i < digits.Length; i++) sum += (digits[i] - '0') * (digits.Length + 1 - i);
            var remainder = sum % 11;
            digits += remainder < 2 ? "0" : (11 - remainder).ToString();
        }
        return digits;
    }

    private static string WithRenavamDigit(string tenDigits)
    {
        int[] weights = [3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum = tenDigits.Select((c, i) => (c - '0') * weights[i]).Sum();
        var digit = sum * 10 % 11;
        return tenDigits + (digit == 10 ? 0 : digit);
    }
}
