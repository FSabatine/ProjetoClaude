using System.Security.Cryptography;
using System.Text;
using Fleet.Domain.Common;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Tracking;

public enum TrackingProviderKind
{
    /// <summary>Any tracker/platform that can POST positions to the ingestion API with a device key.</summary>
    GenericHttp,
    /// <summary>Test/demo data (development seed, homologation).</summary>
    Simulator,
}

/// <summary>
/// A GPS/telematics source (spec §20, ADR-051). Today every provider pushes positions to the same ingestion API; a
/// provider that only offers a pull API becomes a new kind plus an adapter job — the rest of the model is unchanged.
/// </summary>
public class TrackingProvider : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 100;
    public const int NotesMaxLength = 500;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TrackingProviderKind Kind { get; set; } = TrackingProviderKind.GenericHttp;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>
/// A tracker unit (no "last position" column on purpose: the device is audited and every fix would write an audit row —
/// the last position is read from VehiclePositions by index). It authenticates to the ingestion API with its own key: only the SHA-256 hash is stored (the key
/// is random, high-entropy and shown once), plus a short prefix to recognize it on screen.
/// </summary>
public class TrackingDevice : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int IdentifierMaxLength = 50;
    public const int ModelMaxLength = 100;
    public const int KeyPrefixLength = 8;

    public Guid CompanyId { get; set; }
    public Guid TrackingProviderId { get; set; }
    public TrackingProvider TrackingProvider { get; set; } = null!;
    /// <summary>IMEI, serial number or the provider's own id.</summary>
    public string Identifier { get; set; } = string.Empty;
    public string? Model { get; set; }
    public bool IsActive { get; set; } = true;
    public string ApiKeyHash { get; set; } = string.Empty;
    public string ApiKeyPrefix { get; set; } = string.Empty;
    public DateTime? KeyIssuedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Which vehicle carries the device, with validity (history is never overwritten — same idea as assignments).</summary>
public class VehicleDevice : AuditableEntity, ITenantScoped, IAuditable
{
    public Guid CompanyId { get; set; }
    public Guid TrackingDeviceId { get; set; }
    public TrackingDevice TrackingDevice { get; set; } = null!;
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

/// <summary>One GPS fix. Append-only and high-volume: not audited, indexed by (company, vehicle, time).</summary>
public class VehiclePosition : ITenantScoped
{
    public long Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid TrackingDeviceId { get; set; }
    /// <summary>Vehicle carrying the device when the fix was received (null when the device was not installed).</summary>
    public Guid? VehicleId { get; set; }
    /// <summary>Device clock (UTC).</summary>
    public DateTime RecordedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? SpeedKmh { get; set; }
    public int? Heading { get; set; }
    public bool? Ignition { get; set; }
    /// <summary>Device odometer, stored as received. NOT applied to the mileage history (extension point, ADR-051).</summary>
    public int? DeviceOdometerKm { get; set; }
}

/// <summary>
/// Latest fix of each device (one row per device), upserted by the ingestion — the map and the device list read this
/// small table instead of grouping the whole position history (ADR-051). Not audited (updated on every fix).
/// </summary>
public class TrackingDeviceLastPosition : ITenantScoped
{
    public Guid TrackingDeviceId { get; set; }
    public Guid CompanyId { get; set; }
    /// <summary>Vehicle carrying the device when this fix was received.</summary>
    public Guid? VehicleId { get; set; }
    public DateTime RecordedAt { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? SpeedKmh { get; set; }
    public int? Heading { get; set; }
    public bool? Ignition { get; set; }
}

public enum TrackingConnectionStatus
{
    NeverConnected,
    Online,
    Recent,
    Offline,
}

public sealed record PositionInput(DateTime RecordedAt, decimal Latitude, decimal Longitude, decimal? SpeedKmh, int? Heading, bool? Ignition, int? DeviceOdometerKm);

/// <summary>Pure rules of the tracking foundation (ADR-051).</summary>
public static class TrackingRules
{
    public const int OnlineMinutes = 10;
    public const int RecentHours = 24;
    public const int MaxPositionsPerRequest = 500;
    public const int MaxPositionAgeDays = 30;
    public const int MaxClockSkewMinutes = 5;
    public const decimal MaxSpeedKmh = 250m;
    public const int MaxHistoryDays = 7;
    public const int MaxHistoryPoints = 2000;

    public static TrackingConnectionStatus Status(DateTime? lastPositionAt, DateTime now) => lastPositionAt switch
    {
        null => TrackingConnectionStatus.NeverConnected,
        { } at when now - at <= TimeSpan.FromMinutes(OnlineMinutes) => TrackingConnectionStatus.Online,
        { } at when now - at <= TimeSpan.FromHours(RecentHours) => TrackingConnectionStatus.Recent,
        _ => TrackingConnectionStatus.Offline,
    };

    /// <summary>Why a fix is rejected (null = accepted). Rejected fixes are counted, never stored.</summary>
    public static string? Reject(PositionInput p, DateTime now)
    {
        if (p.Latitude is < -90 or > 90 || p.Longitude is < -180 or > 180) return "coordenadas fora do intervalo";
        if (p.Latitude == 0 && p.Longitude == 0) return "coordenada 0,0 (sem sinal de GPS)";
        if (p.SpeedKmh is < 0 or > MaxSpeedKmh) return "velocidade fora do intervalo";
        if (p.Heading is < 0 or > 359) return "direção fora do intervalo";
        if (p.DeviceOdometerKm is < 0) return "hodômetro negativo";
        var recorded = DateTime.SpecifyKind(p.RecordedAt, DateTimeKind.Utc);
        if (recorded > now.AddMinutes(MaxClockSkewMinutes)) return "data no futuro";
        if (recorded < now.AddDays(-MaxPositionAgeDays)) return "posição muito antiga";
        return null;
    }

    /// <summary>A new random device key ("fk_" + 40 url-safe chars) — shown once to the user.</summary>
    public static string NewApiKey() =>
        "fk_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(30)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>SHA-256 is enough for a 240-bit random key (no dictionary to attack, unlike a password).</summary>
    public static string HashApiKey(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
