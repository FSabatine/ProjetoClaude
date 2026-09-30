using Fleet.Application.Common;

namespace Fleet.Infrastructure;

public sealed class SystemClock : IClock
{
    // IANA id works on Windows (.NET 6+ with ICU) and Linux containers alike.
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly Today => ToBusinessDate(UtcNow);

    public DateTime ToBusinessDateTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), BusinessTimeZone);

    public DateOnly ToBusinessDate(DateTime utc) => DateOnly.FromDateTime(ToBusinessDateTime(utc));

    public DateTime StartOfBusinessDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), BusinessTimeZone);
}
