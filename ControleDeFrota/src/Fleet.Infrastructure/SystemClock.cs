using Fleet.Application.Common;

namespace Fleet.Infrastructure;

public sealed class SystemClock : IClock
{
    // IANA id works on Windows (.NET 6+ with ICU) and Linux containers alike.
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, BusinessTimeZone));
}
