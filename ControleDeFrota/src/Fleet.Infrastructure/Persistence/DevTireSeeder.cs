using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Application.Tires;
using Fleet.Domain.Implements;
using Fleet.Domain.Mileage;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Infrastructure.Persistence;

/// <summary>
/// DEVELOPMENT ONLY (Phase 5 samples). Tire models, axle layouts on the sample vehicles and implement, a full set of tires on the
/// Scania (installed 75 days ago at the km the odometer history had then), inspections with a worn, a damaged and a low-tread tire,
/// stock tires, one at the retreader and one disposed after its life. Lifecycle events are written like the services do, so the
/// tire timeline and the vehicle timeline have content. Idempotent: skips when the company already has tires.
/// </summary>
internal sealed class DevTireSeeder(FleetDbContext db, IClock clock)
{
    public async Task<bool> SeedAsync(Guid companyId, CancellationToken ct)
    {
        if (await db.Tires.IgnoreQueryFilters().AnyAsync(t => t.CompanyId == companyId, ct)) return false;
        var vehicles = await db.Vehicles.IgnoreQueryFilters().Where(v => v.CompanyId == companyId && v.DeletedAt == null).ToListAsync(ct);
        var scania = vehicles.FirstOrDefault(v => v.LicensePlate == "RDX1A23");
        if (scania is null) return false;
        var implements = await db.Implements.IgnoreQueryFilters().Where(i => i.CompanyId == companyId && i.DeletedAt == null).ToListAsync(ct);

        var layouts = await db.TireLayouts.IgnoreQueryFilters().Include(l => l.Axles).Where(l => l.CompanyId == companyId).ToListAsync(ct);
        if (layouts.Count == 0)
        {
            layouts = TireLayoutService.CreateDefaults(companyId).ToList();
            db.TireLayouts.AddRange(layouts);
        }
        TireLayout Layout(string name) => layouts.First(l => l.Name == name);
        // Reference pressure on the truck layouts, so the inspections below are compared with something.
        foreach (var axle in Layout("Cavalo mecânico 6x2").Axles) axle.RecommendedPressurePsi = axle.Type == AxleType.Steer ? 120 : 110;

        Assign(vehicles, "RDX1A23", Layout("Cavalo mecânico 6x2"));
        Assign(vehicles, "RDX2B34", Layout("Cavalo mecânico 6x4"));
        Assign(vehicles, "RDX3C45", Layout("Cavalo mecânico 6x2"));
        Assign(vehicles, "ABC1234", Layout("Carro / picape (2 eixos simples)"));
        if (implements.FirstOrDefault(i => i.LicensePlate == "RDX4D56") is { } trailer) trailer.TireLayoutId = Layout("Semirreboque 3 eixos").Id;

        var steer = Model(companyId, "Michelin", "X Multi Z", "295/80R22.5", TireApplication.Steer, 16);
        var drive = Model(companyId, "Bridgestone", "M729", "295/80R22.5", TireApplication.Drive, 21);
        var pickup = Model(companyId, "Pirelli", "Scorpion ATR", "255/70R16", TireApplication.AllPosition, 9);
        db.TireModels.AddRange(steer, drive, pickup);

        var sequence = 0;
        Tire NewTire(TireModel model, decimal price, int daysAgoBought, int retreads = 0, string? storage = null) => new()
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Sequence = ++sequence, Code = Tire.FormatCode(sequence), Model = model,
            TireModelId = model.Id, Dot = $"DOT 4B7X {(sequence % 40) + 10:D2}24", ManufacturedOn = TireDot.ManufacturedOn($"{(sequence % 40) + 10:D2}24"),
            PurchasedOn = clock.Today.AddDays(-daysAgoBought), PurchasePrice = price, Supplier = "Distribuidora Pneus Sul",
            OriginalTreadDepthMm = model.OriginalTreadDepthMm, CurrentTreadDepthMm = model.OriginalTreadDepthMm, RetreadCount = retreads,
            StorageLocation = storage, Status = TireStatus.InStock,
        };

        // ---- Scania RDX1A23: every position filled 75 days ago ----
        var installedAt = clock.UtcNow.AddDays(-75);
        var installedKm = await OdometerAtAsync(companyId, scania.Id, installedAt, ct) ?? scania.CurrentOdometerKm;
        var positions = TirePositions.For(Layout("Cavalo mecânico 6x2"));
        var tires = new List<Tire>();
        foreach (var position in positions)
        {
            var tire = NewTire(position.AxleType == AxleType.Steer ? steer : drive, position.AxleType == AxleType.Steer ? 2_350m : 2_180m, 90,
                retreads: position.AxleType == AxleType.Free ? 1 : 0);
            tires.Add(tire);
            db.Tires.Add(tire);
            Install(companyId, tire, scania, position, installedAt, installedKm);
        }
        // Inspections ten days ago: normal wear, one near the minimum, one at it, one with irregular wear, one with a puncture.
        var inspectedAt = clock.UtcNow.AddDays(-10);
        var inspectedKm = await OdometerAtAsync(companyId, scania.Id, inspectedAt, ct) ?? scania.CurrentOdometerKm;
        var conditions = new (decimal Tread, decimal Psi, TireCondition Condition, TireWearPattern Wear, TireDamageType? Damage)[]
        {
            (13.5m, 118, TireCondition.Good, TireWearPattern.Normal, null),
            (13.0m, 120, TireCondition.Good, TireWearPattern.Normal, null),
            (17.5m, 109, TireCondition.Good, TireWearPattern.Normal, null),
            (3.6m, 104, TireCondition.Attention, TireWearPattern.Normal, null),
            (17.0m, 92, TireCondition.Attention, TireWearPattern.Normal, TireDamageType.Puncture),
            (16.5m, 110, TireCondition.Good, TireWearPattern.Normal, null),
            (8.0m, 110, TireCondition.Attention, TireWearPattern.ShoulderWear, null),
            (2.8m, 108, TireCondition.Unfit, TireWearPattern.Normal, null),
            (9.5m, 111, TireCondition.Good, TireWearPattern.Normal, null),
            (9.0m, 110, TireCondition.Good, TireWearPattern.Normal, null),
        };
        foreach (var (tire, position, c) in tires.Zip(positions).Where(x => !x.Second.IsSpare).Zip(conditions).Select(x => (x.First.First, x.First.Second, x.Second)))
            Inspect(companyId, tire, scania, position, inspectedAt, inspectedKm, installedKm, c.Tread, c.Psi, c.Condition, c.Wear, c.Damage);

        // ---- stock, retreader and a disposed tire ----
        foreach (var i in Enumerable.Range(0, 3))
        {
            var spare = NewTire(drive, 2_180m, 20, storage: $"Almoxarifado Curitiba · Prateleira {i + 1}");
            db.Tires.Add(spare);
            Event(companyId, OperationalEventType.TireRegistered, spare, null, clock.UtcNow.AddDays(-20),
                $"Pneu {spare.Code} ({drive.Brand} {drive.Name} {drive.Size}) cadastrado em {spare.StorageLocation}.");
        }
        var atRetreader = NewTire(drive, 2_050m, 400, retreads: 1);
        atRetreader.Status = TireStatus.UnderRetread;
        atRetreader.AccumulatedKm = 118_400;
        atRetreader.CurrentTreadDepthMm = 4.0m;
        atRetreader.LastMovementAt = clock.UtcNow.AddDays(-6);
        db.Tires.Add(atRetreader);
        db.TireServiceOrders.Add(new TireServiceOrder
        {
            CompanyId = companyId, Tire = atRetreader, TireId = atRetreader.Id, Kind = TireServiceKind.Retread, SentAt = clock.UtcNow.AddDays(-6),
            ProviderName = "Recapadora Paraná", RetreadNumber = 2, Description = "Banda de tração",
        });
        Event(companyId, OperationalEventType.TireRetreadStarted, atRetreader, null, clock.UtcNow.AddDays(-6),
            $"Pneu {atRetreader.Code} enviado para a recapagem nº 2 (Recapadora Paraná).");

        var disposed = NewTire(steer, 2_300m, 700, retreads: 0);
        disposed.Status = TireStatus.Disposed;
        disposed.AccumulatedKm = 142_000;
        disposed.CurrentTreadDepthMm = 2.5m;
        disposed.DisposedAt = clock.UtcNow.AddDays(-40);
        disposed.DisposalReason = TireDisposalReason.EndOfLife;
        disposed.DisposalDestination = "Reciclagem — Reciclanip";
        disposed.LastMovementAt = disposed.DisposedAt;
        db.Tires.Add(disposed);
        Event(companyId, OperationalEventType.TireEndOfLife, disposed, null, disposed.DisposedAt.Value,
            $"Pneu {disposed.Code} baixado (fim de vida) com 142.000 km rodados. Destino: Reciclagem — Reciclanip.");

        db.TireCosts.Add(new TireCost
        {
            CompanyId = companyId, TireId = tires[4].Id, Type = TireCostType.Installation, IncurredOn = clock.ToBusinessDate(installedAt),
            Amount = 45m, Description = "Montagem e balanceamento",
        });

        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void Assign(IEnumerable<Vehicle> vehicles, string plate, TireLayout layout)
    {
        if (vehicles.FirstOrDefault(v => v.LicensePlate == plate) is { } vehicle) vehicle.TireLayoutId = layout.Id;
    }

    private static TireModel Model(Guid companyId, string brand, string name, string size, TireApplication application, decimal tread) => new()
    {
        Id = Guid.NewGuid(), CompanyId = companyId, Brand = brand, Name = name, Size = size, Application = application,
        Construction = TireConstruction.Radial, OriginalTreadDepthMm = tread, LoadIndex = size.EndsWith("R16") ? "112" : "152/148",
        SpeedRating = size.EndsWith("R16") ? "T" : "M",
    };

    private void Install(Guid companyId, Tire tire, Vehicle vehicle, TirePosition position, DateTime at, int km)
    {
        db.TireInstallations.Add(new TireInstallation
        {
            CompanyId = companyId, TireId = tire.Id, VehicleId = vehicle.Id, PositionCode = position.Code, PositionLabel = position.Label,
            AxleNumber = position.AxleNumber, IsSpare = position.IsSpare, InstalledAt = at, InstalledOdometerKm = km,
            InstallReason = TireInstallReason.Installation,
        });
        tire.Status = TireStatus.Installed;
        tire.StorageLocation = null;
        tire.InspectionReferenceAt = at;
        tire.LastMovementAt = at;
        Event(companyId, OperationalEventType.TireInstalled, tire, vehicle.Id, at,
            $"Pneu {tire.Code} ({tire.Model.Brand} {tire.Model.Name} {tire.Model.Size}) instalado em {LicensePlate.Format(vehicle.LicensePlate)}, " +
            $"{position.Label}, com {Fleet.Domain.Common.BrazilianFormat.Number(km)} km.");
    }

    private void Inspect(Guid companyId, Tire tire, Vehicle vehicle, TirePosition position, DateTime at, int km, int installedKm, decimal tread,
        decimal psi, TireCondition condition, TireWearPattern wear, TireDamageType? damage)
    {
        var inspection = new TireInspection
        {
            CompanyId = companyId, TireId = tire.Id, VehicleId = vehicle.Id, PositionCode = position.Code, PositionLabel = position.Label,
            InspectedAt = at, OdometerKm = km, TireKm = Math.Max(0, km - installedKm), TreadDepthMm = tread, Pressure = psi,
            PressureUnit = TirePressureUnit.Psi, PressureCheck = TirePressure.Check(psi, TirePressureUnit.Psi, position.RecommendedPressurePsi, 10),
            Condition = condition, WearPattern = wear,
            Damages = damage is { } d ? [new TireInspectionDamage { CompanyId = companyId, Type = d }] : [],
            Notes = damage is not null ? "Furo na banda de rodagem, sem dano estrutural aparente." : wear != TireWearPattern.Normal ? "Desgaste no ombro externo." : null,
        };
        db.TireInspections.Add(inspection);
        tire.CurrentTreadDepthMm = tread;
        tire.TreadMeasuredAt = at;
        tire.LastInspectedAt = at;
        tire.LastWearPattern = wear;
        tire.LastInspectionHasDamage = damage is not null;
        tire.LastPressureCheck = inspection.PressureCheck;
        tire.InspectionReferenceAt = at;
        Event(companyId, OperationalEventType.TireInspected, tire, vehicle.Id, at,
            $"Pneu {tire.Code} inspecionado em {LicensePlate.Format(vehicle.LicensePlate)}, {position.Label}: sulco {Fleet.Domain.Common.BrazilianFormat.Compact(tread)} mm, pressão {psi} psi.");
    }

    private void Event(Guid companyId, OperationalEventType type, Tire tire, Guid? vehicleId, DateTime at, string summary) =>
        db.OperationalEvents.Add(OperationalEventLog.Create(type, new EventSubject(nameof(Tire), tire.Id, vehicleId, null, null, tire.Id), summary,
            new { tireId = tire.Id }, at, userId: null, companyId));

    private async Task<int?> OdometerAtAsync(Guid companyId, Guid vehicleId, DateTime at, CancellationToken ct) =>
        await db.OdometerReadings.IgnoreQueryFilters()
            .Where(r => r.CompanyId == companyId && r.VehicleId == vehicleId && r.Status == OdometerReadingStatus.Valid && r.ReadAt <= at)
            .OrderByDescending(r => r.ReadAt).Select(r => (int?)r.OdometerKm).FirstOrDefaultAsync(ct);
}
