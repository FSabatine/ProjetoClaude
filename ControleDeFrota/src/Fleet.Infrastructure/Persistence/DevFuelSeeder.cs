using Fleet.Application.Common;
using Fleet.Application.Fuel;
using Fleet.Domain.Common;
using Fleet.Domain.Fuel;
using Fleet.Domain.Mileage;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Infrastructure.Persistence;

/// <summary>
/// DEVELOPMENT ONLY (Phase 4 samples). Stations, reference prices and ~4 months of fuelings for the sample vehicles,
/// ending exactly at each vehicle's current odometer, each with its odometer reading (source Fueling). Consumption is
/// computed by the Domain calculator — the same rules as live data — and two deliberate anomalies wait for review.
/// Deterministic (fixed seed) and idempotent (skips when the company already has stations).
/// </summary>
internal sealed class DevFuelSeeder(FleetDbContext db, IClock clock)
{
    private sealed record Profile(string Plate, decimal Tank, decimal? SecondTank, decimal Expected, int KmPerTrip, int DaysPerTrip, int Days);

    // Typical figures: heavy trucks ~2.5 km/L with two tanks, the pickup ~10 km/L.
    private static readonly Profile[] Profiles =
    [
        new("RDX2B34", 400, 300, 2.6m, 1_050, 3, 120),
        new("RDX3C45", 400, 300, 2.4m, 900, 3, 110),
        new("ABC1234", 80, null, 10.0m, 600, 6, 120),
    ];

    /// <summary>Returns false when there was nothing to do.</summary>
    public async Task<bool> SeedAsync(Guid companyId, CancellationToken ct)
    {
        if (await db.FuelStations.IgnoreQueryFilters().AnyAsync(s => s.CompanyId == companyId, ct)) return false;
        var vehicles = await db.Vehicles.IgnoreQueryFilters()
            .Where(v => v.CompanyId == companyId && v.DeletedAt == null).ToListAsync(ct);
        if (vehicles.Count == 0) return false;

        var types = await db.FuelTypes.IgnoreQueryFilters().Where(t => t.CompanyId == companyId).ToListAsync(ct);
        if (types.Count == 0)
        {
            types = FuelTypeService.CreateDefaults(companyId).ToList();
            db.FuelTypes.AddRange(types);
        }
        var diesel = types.First(t => t.Code == "S10");

        var stations = new[]
        {
            Station(companyId, "Posto Rota Sul", "Curitiba", "PR", isInternal: false),
            Station(companyId, "Posto Graal BR-277", "Balsa Nova", "PR", isInternal: false),
            Station(companyId, "Tanque próprio — Pátio Curitiba", "Curitiba", "PR", isInternal: true),
        };
        db.FuelStations.AddRange(stations);
        var today = clock.Today;
        foreach (var (station, price) in stations.Zip(new[] { 6.19m, 6.29m, 5.89m }))
        {
            db.FuelPrices.Add(new FuelPrice { CompanyId = companyId, FuelStation = station, FuelType = diesel, Price = price - 0.15m, EffectiveFrom = today.AddDays(-120) });
            db.FuelPrices.Add(new FuelPrice { CompanyId = companyId, FuelStation = station, FuelType = diesel, Price = price, EffectiveFrom = today.AddDays(-45) });
        }

        var random = new Random(42);
        foreach (var profile in Profiles)
        {
            var vehicle = vehicles.FirstOrDefault(v => v.LicensePlate == profile.Plate);
            if (vehicle is null) continue;
            vehicle.FuelTankCapacity = profile.Tank;
            vehicle.SecondaryFuelTankCapacity = profile.SecondTank;
            vehicle.ExpectedConsumption = profile.Expected;
            SeedVehicle(companyId, vehicle, profile, diesel, stations, random);
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    private void SeedVehicle(Guid companyId, Vehicle vehicle, Profile profile, FuelType diesel, FuelStation[] stations, Random random)
    {
        var end = vehicle.OdometerUpdatedAt ?? clock.UtcNow.AddDays(-1);
        var trips = profile.Days / profile.DaysPerTrip;
        var fuelings = new List<Fueling>();
        // Distances first, so the history ends exactly at the vehicle's current odometer.
        var distances = Enumerable.Range(0, trips + 1).Select(i => i == 0 ? 0 : profile.KmPerTrip + random.Next(-60, 60)).ToList();
        var km = vehicle.CurrentOdometerKm - distances.Sum();
        // Fuel burned but not yet put back: a partial fill refills half of it, the next full tank the rest.
        var owed = 0m;
        for (var i = 0; i <= trips; i++)
        {
            var at = end.AddDays(-(trips - i) * profile.DaysPerTrip).AddHours(-random.Next(0, 6));
            km += distances[i];
            // Partial top-ups only on the two-tank trucks; a small tank is simply filled up.
            var partial = i % 7 == 3 && profile.SecondTank is not null;
            // Normal variation ±6% around the expected consumption; one planted deviation for the review queue.
            var efficiency = profile.Expected * (decimal)(0.94 + random.NextDouble() * 0.12);
            if (i == trips - 3 && profile.Plate == "RDX2B34") efficiency = profile.Expected * 0.72m;  // consumption well below expected
            owed += i == 0 ? profile.Tank * 0.8m : distances[i] / efficiency;
            var liters = Math.Round(partial ? owed * 0.5m : owed, 1);
            owed -= liters;
            var station = stations[random.Next(stations.Length)];
            var price = (station.IsInternal ? 5.89m : 6.19m) + (decimal)random.Next(-8, 12) / 100m;
            if (i == trips - 1 && profile.Plate == "RDX3C45") liters = 760m;  // above the 700 L of both tanks

            fuelings.Add(new Fueling
            {
                // Explicit id: the readings and anomalies below point at it before EF would assign one.
                Id = Guid.NewGuid(), CompanyId = companyId, Vehicle = vehicle, VehicleId = vehicle.Id, FuelType = diesel, FuelTypeId = diesel.Id, FuelStation = station,
                FueledAt = at, FueledOn = clock.ToBusinessDate(at), OdometerKm = km, Quantity = liters, UnitPrice = price,
                TotalAmount = FuelingAmounts.Total(liters, price), IsFullTank = !partial,
                PaymentMethod = station.IsInternal ? PaymentMethod.InternalTank : PaymentMethod.FuelCard,
            });
        }

        var outcomes = ConsumptionCalculator.Calculate(fuelings.Select(f => new ConsumptionChainEntry(
            f.Id, f.FueledAt, f.OdometerKm, f.Quantity, f.TotalAmount, f.IsFullTank, FuelUnit.Liter, false)).ToList(), []);
        var baseline = new ResolvedBaseline(profile.Expected, ConsumptionBaselineSource.Configured);
        var settings = FuelSettings.Defaults();
        var tank = profile.Tank + (profile.SecondTank ?? 0);
        foreach (var (fueling, outcome) in fuelings.Zip(outcomes))
        {
            fueling.ConsumptionResult = outcome.Result;
            if (outcome.Result == ConsumptionResult.Calculated)
            {
                fueling.SegmentDistanceKm = outcome.DistanceKm;
                fueling.SegmentQuantity = outcome.Quantity;
                fueling.SegmentCost = outcome.Cost;
                fueling.Consumption = outcome.Consumption;
                fueling.ExpectedConsumption = baseline.Value;
                fueling.BaselineSource = baseline.Source;
                fueling.ConsumptionDeviationPercent = FuelAnomalyRules.DeviationPercent(outcome.Consumption!.Value, baseline.Value);
                fueling.SegmentExpectedQuantity = Math.Round(outcome.DistanceKm!.Value / baseline.Value, 3);
                if (FuelAnomalyRules.EvaluateConsumption(outcome.Consumption.Value, baseline, FuelUnit.Liter, settings.ConsumptionDeviationPercent) is { } c)
                    fueling.Anomalies.Add(Anomaly(companyId, fueling, c));
            }
            if (fueling.Quantity > tank * (1 + settings.TankTolerancePercent / 100m))
            {
                var check = new FuelingCheck(fueling.Quantity, FuelUnit.Liter, fueling.UnitPrice, tank, null, null, vehicle.FuelType,
                    diesel.Category, diesel.Name, null);
                foreach (var found in FuelAnomalyRules.EvaluateRecord(check, settings)) fueling.Anomalies.Add(Anomaly(companyId, fueling, found));
            }
            fueling.RefreshStatus();
            db.Fuelings.Add(fueling);
            db.OdometerReadings.Add(new OdometerReading
            {
                CompanyId = companyId, VehicleId = vehicle.Id, OdometerKm = fueling.OdometerKm, ReadAt = fueling.FueledAt,
                Source = OdometerReadingSource.Fueling, FuelingId = fueling.Id,
            });
        }
        vehicle.OdometerUpdatedAt = fuelings[^1].FueledAt;
    }

    private FuelingAnomaly Anomaly(Guid companyId, Fueling fueling, DetectedAnomaly found) => new()
    {
        CompanyId = companyId, FuelingId = fueling.Id, Type = found.Type, Message = found.Message,
        ExpectedValue = found.Expected, ActualValue = found.Actual, DetectedAt = fueling.FueledAt,
    };

    private static FuelStation Station(Guid companyId, string name, string city, string state, bool isInternal) => new()
    {
        Id = Guid.NewGuid(), CompanyId = companyId, Name = name, IsInternal = isInternal,
        Address = new Address { City = city, State = state },
        Notes = isInternal ? "Bomba interna do pátio (sem controle de estoque nesta fase)." : null,
    };
}
