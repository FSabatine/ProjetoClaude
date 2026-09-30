using System.Text.Json;
using System.Text.Json.Serialization;
using Fleet.Application.Common;
using Fleet.Domain.Operations;

namespace Fleet.Application.Operations;

/// <summary>What an event is about: the timelines it appears in and the record that caused it.</summary>
public sealed record EventSubject(string Type, Guid Id, Guid? VehicleId = null, Guid? DriverId = null, Guid? ImplementId = null);

/// <summary>
/// ADR-025: modules report what happened here instead of calling each other. The event is added to the current unit of
/// work, so it is saved atomically with the change (no event without the change, no change without the event).
/// Future consumers (notifications, maintenance) read OperationalEvents; producers never know who listens.
/// </summary>
public sealed class OperationalEventLog(IFleetDbContext db, IClock clock, ICurrentUser currentUser)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public void Record(OperationalEventType type, EventSubject subject, string summary, object? data = null) =>
        db.OperationalEvents.Add(Create(type, subject, summary, data, clock.UtcNow, currentUser.UserId, companyId: null));

    /// <summary>For system jobs that run outside a request (no current company): the company is explicit.</summary>
    public static OperationalEvent Create(
        OperationalEventType type, EventSubject subject, string summary, object? data, DateTime occurredAt, Guid? userId, Guid? companyId) => new()
    {
        CompanyId = companyId ?? Guid.Empty,
        Type = type,
        OccurredAt = occurredAt,
        UserId = userId,
        VehicleId = subject.VehicleId,
        DriverId = subject.DriverId,
        ImplementId = subject.ImplementId,
        SubjectType = subject.Type,
        SubjectId = subject.Id,
        Summary = summary.Length <= OperationalEvent.SummaryMaxLength ? summary : summary[..(OperationalEvent.SummaryMaxLength - 1)] + "…",
        Data = JsonSerializer.Serialize(data ?? new { }, JsonOptions),
    };
}
