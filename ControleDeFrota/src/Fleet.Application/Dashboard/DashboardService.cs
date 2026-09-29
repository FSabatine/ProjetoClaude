using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Drivers;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Dashboard;

public sealed record DashboardIndicators(
    int ActiveVehicles,
    int AvailableVehicles,
    int VehiclesUnderMaintenance,
    int ActiveDrivers);

public enum AlertType
{
    LicenseExpired,
    LicenseExpiringSoon,
}

public enum AlertSeverity
{
    Warning,
    Critical,
}

public sealed record DashboardAlert(
    AlertType Type,
    AlertSeverity Severity,
    string Title,
    string Description,
    string EntityType,
    Guid EntityId,
    DateOnly DueDate);

public sealed record DashboardResponse(DashboardIndicators Indicators, IReadOnlyList<DashboardAlert> Alerts, int TotalAlerts);

/// <summary>
/// Each indicator/alert source is an independent method, so future KPIs (fuel, maintenance, costs,
/// documents) are added alongside without touching the existing ones.
/// </summary>
public sealed class DashboardService(IFleetDbContext db, IClock clock, ICurrentUser currentUser)
{
    public const int MaxAlerts = 10;
    // Literal slashes + invariant culture: "/" in a format string is the *culture's* separator, and Windows
    // regional overrides can turn it into "." even for pt-BR.
    private const string BrazilianDate = "dd'/'MM'/'yyyy";

    public async Task<DashboardResponse> GetAsync(CancellationToken ct)
    {
        var indicators = await GetIndicatorsAsync(ct);
        // Alerts name drivers (personal data), so they require drivers.view on top of dashboard.view.
        var (alerts, total) = currentUser.HasPermission(Permissions.Drivers.View)
            ? await GetLicenseAlertsAsync(ct)
            : ([], 0);
        return new DashboardResponse(indicators, alerts, total);
    }

    private async Task<DashboardIndicators> GetIndicatorsAsync(CancellationToken ct)
    {
        var vehiclesByStatus = await db.Vehicles
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        int Count(VehicleStatus s) => vehiclesByStatus.GetValueOrDefault(s);

        var activeDrivers = await db.Drivers.CountAsync(d => d.Status == DriverStatus.Active, ct);

        return new DashboardIndicators(
            ActiveVehicles: vehiclesByStatus.Where(kv => kv.Key != VehicleStatus.Inactive).Sum(kv => kv.Value),
            AvailableVehicles: Count(VehicleStatus.Available),
            VehiclesUnderMaintenance: Count(VehicleStatus.UnderMaintenance),
            ActiveDrivers: activeDrivers);
    }

    private async Task<(IReadOnlyList<DashboardAlert> Alerts, int Total)> GetLicenseAlertsAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var limit = today.AddDays(Driver.LicenseExpiryAlertDays);
        var query = db.Drivers.Where(d => d.Status != DriverStatus.Inactive && d.LicenseExpiresOn <= limit);

        var total = await query.CountAsync(ct);
        var drivers = await query
            .OrderBy(d => d.LicenseExpiresOn)
            .Take(MaxAlerts)
            .Select(d => new { d.Id, d.FullName, d.LicenseExpiresOn })
            .ToListAsync(ct);

        var alerts = drivers.Select(d =>
        {
            var expired = d.LicenseExpiresOn < today;
            var days = d.LicenseExpiresOn.DayNumber - today.DayNumber;
            var date = d.LicenseExpiresOn.ToString(BrazilianDate, System.Globalization.CultureInfo.InvariantCulture);
            return new DashboardAlert(
                expired ? AlertType.LicenseExpired : AlertType.LicenseExpiringSoon,
                expired ? AlertSeverity.Critical : AlertSeverity.Warning,
                expired ? "CNH vencida" : "CNH vencendo",
                expired
                    ? $"A CNH de {d.FullName} venceu em {date}."
                    : days switch
                    {
                        0 => $"A CNH de {d.FullName} vence hoje.",
                        1 => $"A CNH de {d.FullName} vence amanhã.",
                        _ => $"A CNH de {d.FullName} vence em {days} dias ({date}).",
                    },
                "Driver", d.Id, d.LicenseExpiresOn);
        }).ToList();

        return (alerts, total);
    }
}
