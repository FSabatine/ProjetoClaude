using Fleet.Application.Common;
using Fleet.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Infrastructure.Persistence;

/// <summary>
/// DEVELOPMENT ONLY (final phase, stage D). A "Simulador" provider with three trackers on the sample trucks: one
/// transmitting now on BR-277 (Curitiba → Paranaguá), one seen hours ago near Ponta Grossa, one offline for days.
/// Keys are random and never shown (use "Gerar nova chave" on screen to test the ingestion API). Idempotent.
/// </summary>
internal sealed class DevTrackingSeeder(FleetDbContext db, IClock clock)
{
    private sealed record Route(string Plate, string Identifier, (decimal Lat, decimal Lon) From, (decimal Lat, decimal Lon) To, int Points, TimeSpan EndsAgo, decimal Speed);

    public async Task<bool> SeedAsync(Guid companyId, CancellationToken ct)
    {
        if (await db.TrackingProviders.IgnoreQueryFilters().AnyAsync(p => p.CompanyId == companyId, ct)) return false;
        var vehicles = await db.Vehicles.IgnoreQueryFilters().Where(v => v.CompanyId == companyId && v.DeletedAt == null)
            .ToDictionaryAsync(v => v.LicensePlate, v => v.Id, ct);
        var routes = new[]
        {
            new Route("RDX2B34", "SIM-0001", (-25.4284m, -49.2733m), (-25.5163m, -48.5225m), 36, TimeSpan.FromMinutes(2), 78m),
            new Route("RDX1A23", "SIM-0002", (-25.0945m, -50.1633m), (-25.3300m, -49.6200m), 24, TimeSpan.FromHours(5), 64m),
            new Route("RDX3C45", "SIM-0003", (-26.3045m, -48.8487m), (-26.2600m, -48.8000m), 6, TimeSpan.FromDays(3), 0m),
        }.Where(r => vehicles.ContainsKey(r.Plate)).ToList();
        if (routes.Count == 0) return false;

        var provider = new TrackingProvider { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Simulador (dados de exemplo)", Kind = TrackingProviderKind.Simulator };
        db.TrackingProviders.Add(provider);
        var now = clock.UtcNow;
        foreach (var route in routes)
        {
            var key = TrackingRules.NewApiKey();
            var device = new TrackingDevice
            {
                Id = Guid.NewGuid(), CompanyId = companyId, TrackingProviderId = provider.Id, Identifier = route.Identifier, Model = "Simulador",
                ApiKeyHash = TrackingRules.HashApiKey(key), ApiKeyPrefix = key[..TrackingDevice.KeyPrefixLength], KeyIssuedAt = now,
            };
            db.TrackingDevices.Add(device);
            var vehicleId = vehicles[route.Plate];
            db.VehicleDevices.Add(new VehicleDevice { CompanyId = companyId, TrackingDeviceId = device.Id, VehicleId = vehicleId, StartedAt = now.AddDays(-10) });
            var end = now - route.EndsAgo;
            for (var i = 0; i < route.Points; i++)
            {
                var t = route.Points == 1 ? 1m : i / (decimal)(route.Points - 1);
                // Small deterministic wobble so the line looks like a road, not a ruler.
                var wobble = (decimal)Math.Sin(i * 0.9) * 0.004m;
                db.VehiclePositions.Add(new VehiclePosition
                {
                    CompanyId = companyId, TrackingDeviceId = device.Id, VehicleId = vehicleId,
                    RecordedAt = end.AddMinutes(-5 * (route.Points - 1 - i)), ReceivedAt = end.AddMinutes(-5 * (route.Points - 1 - i)),
                    Latitude = Math.Round(route.From.Lat + (route.To.Lat - route.From.Lat) * t + wobble, 6),
                    Longitude = Math.Round(route.From.Lon + (route.To.Lon - route.From.Lon) * t, 6),
                    SpeedKmh = route.Speed == 0 ? 0 : route.Speed + (i % 5) * 3, Heading = 100, Ignition = route.Speed > 0,
                });
            }
        }
        await db.SaveChangesAsync(ct);
        return true;
    }
}
