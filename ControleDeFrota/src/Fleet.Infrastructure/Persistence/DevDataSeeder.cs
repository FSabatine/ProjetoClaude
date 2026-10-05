using Fleet.Application.Common;
using Fleet.Application.Documents;
using Fleet.Domain.Assignments;
using Fleet.Domain.Checklists;
using Fleet.Domain.Documents;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
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
        if (await db.Companies.IgnoreQueryFilters().AnyAsync(ct))
        {
            // Databases created in earlier phases get the newer samples once (each step is idempotent).
            if (includeSampleData)
            {
                await SeedOperationsAsync(ct);
                await SeedFuelAsync(ct);
                await SeedTiresAsync(ct);
                await SeedFinanceAsync(ct);
                await SeedTrackingAsync(ct);
            }
            return;
        }

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
        if (includeSampleData)
        {
            await SeedOperationsAsync(ct);
            await SeedFuelAsync(ct);
            await SeedTiresAsync(ct);
            await SeedFinanceAsync(ct);
            await SeedTrackingAsync(ct);
        }
    }

    /// <summary>Final phase samples: simulated trackers and positions (see DevTrackingSeeder).</summary>
    private async Task SeedTrackingAsync(CancellationToken ct)
    {
        var main = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Cnpj == MainCnpj, ct);
        if (main is null) return;
        if (await new DevTrackingSeeder(db, clock).SeedAsync(main.Id, ct))
            logger.LogWarning("Development tracking samples seeded (final phase)");
    }

    /// <summary>Phase 6 samples on the main demo company (see DevFinanceSeeder). Runs last: reads the Phase 4/5 vehicles.</summary>
    private async Task SeedFinanceAsync(CancellationToken ct)
    {
        var main = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Cnpj == MainCnpj, ct);
        if (main is null) return;
        if (await new DevFinanceSeeder(db, clock).SeedAsync(main.Id, ct))
            logger.LogWarning("Development finance samples seeded (Phase 6)");
    }

    /// <summary>Phase 5 samples on the main demo company (see DevTireSeeder). Runs after the fuel samples: it reads their odometer history.</summary>
    private async Task SeedTiresAsync(CancellationToken ct)
    {
        var main = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Cnpj == MainCnpj, ct);
        if (main is null) return;
        if (await new DevTireSeeder(db, clock).SeedAsync(main.Id, ct))
            logger.LogWarning("Development tire samples seeded (Phase 5)");
    }

    /// <summary>Phase 4 samples on the main demo company (see DevFuelSeeder).</summary>
    private async Task SeedFuelAsync(CancellationToken ct)
    {
        var main = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Cnpj == MainCnpj, ct);
        if (main is null) return;
        if (await new DevFuelSeeder(db, clock).SeedAsync(main.Id, ct))
            logger.LogWarning("Development fuel samples seeded (Phase 4)");
    }

    private const string MainCnpj = "11222333000181";

    /// <summary>
    /// Phase 2 samples on the main demo company: operation users, document types, a daily checklist, assignments,
    /// odometer history, documents in every state and an open occurrence. Idempotent (skips when templates exist).
    /// Seeding runs at startup without a signed-in user: filters are bypassed and every tenant row gets its CompanyId explicitly.
    /// </summary>
    private async Task SeedOperationsAsync(CancellationToken ct)
    {
        var main = await db.Companies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Cnpj == MainCnpj, ct);
        if (main is null || await db.ChecklistTemplates.IgnoreQueryFilters().AnyAsync(t => t.CompanyId == main.Id, ct)) return;

        var today = clock.Today;
        var now = clock.UtcNow;
        foreach (var (name, email, role) in new[]
                 {
                     ("Operação Diária", "operacao@frota.local", SystemRoles.Operations),
                     ("Equipe de Manutenção", "manutencao@frota.local", SystemRoles.Maintenance),
                 })
        {
            if (!await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct)) db.Users.Add(NewUser(main, name, email, role));
        }

        var types = await db.DocumentTypes.IgnoreQueryFilters().Where(t => t.CompanyId == main.Id).ToListAsync(ct);
        if (types.Count == 0)
        {
            types = DocumentTypeService.CreateDefaults(main.Id).ToList();
            db.DocumentTypes.AddRange(types);
        }

        db.ChecklistTemplates.Add(NewDailyChecklist(main.Id));

        var vehicles = await db.Vehicles.IgnoreQueryFilters().Where(v => v.CompanyId == main.Id && v.DeletedAt == null).ToListAsync(ct);
        var drivers = await db.Drivers.IgnoreQueryFilters().Where(d => d.CompanyId == main.Id && d.DeletedAt == null).ToListAsync(ct);
        var scania = vehicles.FirstOrDefault(v => v.LicensePlate == "RDX1A23");
        var amarok = vehicles.FirstOrDefault(v => v.LicensePlate == "ABC1234");
        var volvo = vehicles.FirstOrDefault(v => v.LicensePlate == "RDX3C45");
        var joao = drivers.FirstOrDefault(d => d.FullName.StartsWith("João"));
        var maria = drivers.FirstOrDefault(d => d.FullName.StartsWith("Maria"));

        if (scania is not null)
        {
            // Two weeks of history ending with the current odometer.
            var km = scania.CurrentOdometerKm - 1_950;
            foreach (var (daysAgo, step) in new[] { (14, 0), (10, 480), (6, 650), (2, 820) })
            {
                km += step;
                db.OdometerReadings.Add(new OdometerReading
                {
                    CompanyId = main.Id, VehicleId = scania.Id, OdometerKm = km, ReadAt = now.AddDays(-daysAgo), Source = OdometerReadingSource.Manual,
                });
            }
            scania.CurrentOdometerKm = km;
            scania.OdometerUpdatedAt = now.AddDays(-2);

            // A typo (extra digit) waiting for review.
            var typo = km * 10;
            db.OdometerReadings.Add(new OdometerReading
            {
                CompanyId = main.Id, VehicleId = scania.Id, OdometerKm = typo, ReadAt = now.AddDays(-1), Source = OdometerReadingSource.Manual,
                Status = OdometerReadingStatus.PendingReview,
                Anomaly = OdometerPolicy.Evaluate(typo, now.AddDays(-1), new OdometerBaseline(km, now.AddDays(-2))).Anomaly,
            });

            if (joao is not null)
                db.VehicleAssignments.Add(new VehicleAssignment { CompanyId = main.Id, VehicleId = scania.Id, DriverId = joao.Id, StartedAt = now.AddDays(-20) });
            AddDocument(main.Id, types, "Seguro", DocumentOwnerType.Vehicle, vehicleId: scania.Id, expiresOn: today.AddDays(20));
            AddDocument(main.Id, types, "CRLV / Licenciamento anual", DocumentOwnerType.Vehicle, vehicleId: scania.Id, expiresOn: today.AddMonths(8));
            db.Occurrences.Add(new Occurrence
            {
                CompanyId = main.Id, VehicleId = scania.Id, DriverId = joao?.Id, Type = OccurrenceType.TireProblem,
                Severity = OccurrenceSeverity.Medium, OccurredAt = now.AddDays(-1), Location = "Pátio Curitiba",
                Description = "Pressão baixa no pneu traseiro direito.",
            });
        }
        if (amarok is not null && maria is not null)
            db.VehicleAssignments.Add(new VehicleAssignment { CompanyId = main.Id, VehicleId = amarok.Id, DriverId = maria.Id, StartedAt = now.AddDays(-5) });
        if (volvo is not null)
            AddDocument(main.Id, types, "CRLV / Licenciamento anual", DocumentOwnerType.Vehicle, vehicleId: volvo.Id, expiresOn: today.AddDays(-4));
        if (maria is not null)
            AddDocument(main.Id, types, "Exame toxicológico", DocumentOwnerType.Driver, driverId: maria.Id, expiresOn: today.AddDays(8));
        AddDocument(main.Id, types, "RNTRC (ANTT)", DocumentOwnerType.Company, expiresOn: today.AddYears(2));

        await db.SaveChangesAsync(ct);
        logger.LogWarning("Development operational samples seeded (Phase 2)");
    }

    private void AddDocument(Guid companyId, List<DocumentType> types, string typeName, DocumentOwnerType owner,
        Guid? vehicleId = null, Guid? driverId = null, DateOnly? expiresOn = null)
    {
        var type = types.FirstOrDefault(t => t.Name == typeName && t.OwnerType == owner);
        if (type is null) return;
        db.Documents.Add(new Document
        {
            CompanyId = companyId, DocumentType = type, OwnerType = owner, VehicleId = vehicleId, DriverId = driverId,
            Number = "DEMO-2026", ExpiresOn = expiresOn,
            AlertStartsOn = DocumentExpiryPolicy.AlertStartsOn(expiresOn, type.HasExpiration, type.AlertDaysBefore),
        });
    }

    private static ChecklistTemplate NewDailyChecklist(Guid companyId)
    {
        (string Section, string Label, OccurrenceType Type, bool Photo)[] items =
        [
            ("Pneus", "Pneus: calibragem e desgaste", OccurrenceType.TireProblem, true),
            ("Freios", "Freios e freio de estacionamento", OccurrenceType.MechanicalIssue, false),
            ("Iluminação", "Faróis e lanternas", OccurrenceType.MechanicalIssue, false),
            ("Iluminação", "Setas e pisca-alerta", OccurrenceType.MechanicalIssue, false),
            ("Motor", "Nível do óleo do motor", OccurrenceType.MechanicalIssue, false),
            ("Motor", "Nível do líquido de arrefecimento", OccurrenceType.MechanicalIssue, false),
            ("Cabine", "Para-brisa e limpadores", OccurrenceType.VehicleDamage, true),
            ("Cabine", "Retrovisores", OccurrenceType.VehicleDamage, true),
            ("Segurança", "Extintor, triângulo e macaco", OccurrenceType.MissingEquipment, false),
            ("Documentação", "CRLV e documentos do veículo a bordo", OccurrenceType.DocumentationProblem, false),
        ];
        var templateItems = items.Select((item, i) => new ChecklistTemplateItem
        {
            Position = i + 1,
            Section = item.Section,
            Label = item.Label,
            FailureOccurrenceType = item.Type,
            RequiresPhotoOnFail = item.Photo,
            FailureSeverity = item.Type == OccurrenceType.MechanicalIssue ? OccurrenceSeverity.High : OccurrenceSeverity.Medium,
        }).ToList();
        templateItems.Add(new ChecklistTemplateItem
        {
            Position = items.Length + 1, Section = "Observações", Label = "Observações do motorista",
            ResponseType = ChecklistResponseType.Text, IsRequired = false,
        });

        return new ChecklistTemplate
        {
            CompanyId = companyId,
            Name = "Inspeção diária",
            Description = "Verificação antes do primeiro uso do dia.",
            Frequency = ChecklistFrequency.Daily,
            Items = templateItems,
        };
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
        FuelType = VehicleFuelType.DieselS10,
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
