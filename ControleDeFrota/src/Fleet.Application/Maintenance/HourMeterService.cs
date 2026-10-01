using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Maintenance;

public sealed record HourMeterReadingRequest
{
    public decimal? Hours { get; init; }
    public DateTime? ReadAt { get; init; }
    public string? Notes { get; init; }
    public bool IsCorrection { get; init; }
}

public sealed record HourMeterReviewRequest
{
    public string? Notes { get; init; }
}

public sealed class HourMeterReadingListRequest : ListRequest
{
    public HourMeterReadingStatus? Status { get; set; }
}

public sealed record HourMeterReadingResponse(
    Guid Id, Guid VehicleId, decimal Hours, DateTime ReadAt, HourMeterReadingSource Source, HourMeterReadingStatus Status,
    string? Anomaly, string? Notes, string? RecordedByName, DateTime CreatedAt,
    DateTime? ReviewedAt, string? ReviewedByName, string? ReviewNotes);

public sealed class HourMeterReadingRequestValidator : AbstractValidator<HourMeterReadingRequest>
{
    public HourMeterReadingRequestValidator(IClock clock)
    {
        RuleFor(x => x.Hours)
            .NotNull().WithMessage("Horímetro: informe a leitura em horas.")
            .InclusiveBetween(0, HourMeterPolicy.MaxHours)
            .WithMessage($"Horímetro deve estar entre 0 e {BrazilianFormat.Number(HourMeterPolicy.MaxHours, 1)} h.");
        RuleFor(x => x.ReadAt)
            .Must(d => d is null || d.Value <= clock.UtcNow.Add(HourMeterService.ClockTolerance))
            .WithMessage("A data da leitura não pode ser futura.");
        RuleFor(x => x.Notes).MaxLen(HourMeterReading.NotesMaxLength);
        RuleFor(x => x.Notes)
            .Must(n => !string.IsNullOrWhiteSpace(n)).When(x => x.IsCorrection)
            .WithMessage("Informe o motivo da correção.");
    }
}

public sealed class HourMeterReviewRequestValidator : AbstractValidator<HourMeterReviewRequest>
{
    public HourMeterReviewRequestValidator() => RuleFor(x => x.Notes).MaxLen(HourMeterReading.NotesMaxLength);
}

/// <summary>
/// Engine-hour history, mirroring MileageService/ADR-019 exactly — the only writer of Vehicle.HourMeter
/// after registration. Feeds hour-based preventive maintenance (MaintenanceScheduleService).
/// </summary>
public sealed class HourMeterService(
    IFleetDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    OperationalEventLog events,
    IValidator<HourMeterReadingRequest> validator,
    IValidator<HourMeterReviewRequest> reviewValidator)
{
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    public async Task<PagedResult<HourMeterReadingResponse>> ListAsync(Guid vehicleId, HourMeterReadingListRequest request, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");

        var query = db.HourMeterReadings.Where(r => r.VehicleId == vehicleId);
        if (request.Status is { } status) query = query.Where(r => r.Status == status);
        var page = await query.OrderByDescending(r => r.ReadAt).ThenByDescending(r => r.CreatedAt)
            .ToPagedResultAsync(request, r => r, ct);
        return await ToResponsesAsync(page, ct);
    }

    public async Task<HourMeterReadingResponse> RecordAsync(Guid vehicleId, HourMeterReadingRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        if (request.IsCorrection && !currentUser.HasPermission(Permissions.Maintenance.ManageWorkOrders))
            throw new ForbiddenException("Somente gestores de manutenção podem registrar correções de horímetro.");

        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
        if (vehicle.Status == VehicleStatus.Inactive && !request.IsCorrection)
            throw new BusinessRuleException("Este veículo está inativo. Reative-o para registrar novas leituras.");

        var reading = await AddReadingAsync(vehicle, request.Hours!.Value, Min(request.ReadAt ?? clock.UtcNow, clock.UtcNow),
            request.IsCorrection ? HourMeterReadingSource.Correction : HourMeterReadingSource.Manual,
            request.Notes.TrimToNull(), "hours", ct);
        await db.SaveChangesAsync(ct);
        return (await ToResponsesAsync([reading], ct)).Single();
    }

    /// <summary>Adds a reading to the unit of work WITHOUT saving — reused by WorkOrderService on completion.</summary>
    public async Task<HourMeterReading> AddReadingAsync(Vehicle vehicle, decimal hours, DateTime readAt,
        HourMeterReadingSource source, string? notes, string field, CancellationToken ct)
    {
        var baseline = await BaselineAsync(vehicle, ct);
        if (readAt < baseline.ReadAt)
            throw ValidationErrors.ForField(field == "hours" ? "readAt" : field,
                $"A leitura não pode ser anterior à última leitura registrada ({clock.FormatDateTime(baseline.ReadAt)}).");

        var reading = new HourMeterReading { VehicleId = vehicle.Id, Vehicle = vehicle, Hours = hours, ReadAt = readAt, Source = source, Notes = notes };
        var plate = LicensePlate.Format(vehicle.LicensePlate);

        if (source == HourMeterReadingSource.Correction)
        {
            db.HourMeterReadings.Add(reading);
            var previous = vehicle.HourMeter;
            Apply(vehicle, reading);
            events.Record(OperationalEventType.HourMeterCorrected, Subject(reading),
                $"Horímetro de {plate} corrigido de {Hours(previous ?? 0)} para {Hours(hours)}. Motivo: {notes}",
                new { previousHours = previous, hours });
            return reading;
        }

        var evaluation = HourMeterPolicy.Evaluate(hours, readAt, baseline);
        if (evaluation.Check == HourMeterCheck.Decrease)
        {
            throw ValidationErrors.ForField(field,
                $"A leitura ({Hours(hours)}) é menor que a última leitura válida ({Hours(baseline.Hours)} em " +
                $"{BrazilianFormat.Date(clock.ToBusinessDate(baseline.ReadAt))}). Se o horímetro foi trocado, peça uma correção a um gestor.");
        }

        db.HourMeterReadings.Add(reading);
        switch (evaluation.Check)
        {
            case HourMeterCheck.SuspiciousJump:
                reading.Status = HourMeterReadingStatus.PendingReview;
                reading.Anomaly = evaluation.Anomaly;
                events.Record(OperationalEventType.HourMeterAnomalyDetected, Subject(reading),
                    $"Leitura de horímetro suspeita em {plate}: {Hours(hours)}. {evaluation.Anomaly} Aguardando revisão.",
                    new { hours, baselineHours = baseline.Hours, evaluation.Anomaly });
                break;
            default:
                Apply(vehicle, reading);
                events.Record(OperationalEventType.HourMeterRecorded, Subject(reading),
                    $"Horímetro de {plate} atualizado para {Hours(hours)}.",
                    new { hours, deltaHours = hours - baseline.Hours, source });
                break;
        }
        return reading;
    }

    public async Task<HourMeterReadingResponse> ApproveAsync(Guid readingId, HourMeterReviewRequest request, CancellationToken ct)
    {
        await reviewValidator.ValidateAndThrowAsync(request, ct);
        var reading = await LoadPendingAsync(readingId, ct);
        var baseline = await BaselineAsync(reading.Vehicle, ct);
        if (baseline.ReadAt > reading.ReadAt || reading.Hours < baseline.Hours)
            throw new BusinessRuleException("Há leituras válidas posteriores a esta; rejeite-a ou registre uma correção.");

        reading.Status = HourMeterReadingStatus.Valid;
        Review(reading, request.Notes);
        Apply(reading.Vehicle, reading);
        events.Record(OperationalEventType.HourMeterReviewed, Subject(reading),
            $"Leitura de {Hours(reading.Hours)} em {LicensePlate.Format(reading.Vehicle.LicensePlate)} confirmada após revisão.",
            new { reading.Hours, approved = true });
        await db.SaveChangesAsync(ct);
        return (await ToResponsesAsync([reading], ct)).Single();
    }

    public async Task<HourMeterReadingResponse> RejectAsync(Guid readingId, HourMeterReviewRequest request, CancellationToken ct)
    {
        await reviewValidator.ValidateAndThrowAsync(request, ct);
        if (string.IsNullOrWhiteSpace(request.Notes))
            throw ValidationErrors.ForField("notes", "Informe o motivo da rejeição.");
        var reading = await LoadPendingAsync(readingId, ct);

        reading.Status = HourMeterReadingStatus.Rejected;
        Review(reading, request.Notes);
        events.Record(OperationalEventType.HourMeterReviewed, Subject(reading),
            $"Leitura de {Hours(reading.Hours)} em {LicensePlate.Format(reading.Vehicle.LicensePlate)} rejeitada: {reading.ReviewNotes}",
            new { reading.Hours, approved = false });
        await db.SaveChangesAsync(ct);
        return (await ToResponsesAsync([reading], ct)).Single();
    }

    private async Task<HourMeterBaseline> BaselineAsync(Vehicle vehicle, CancellationToken ct)
    {
        var last = await db.HourMeterReadings
            .Where(r => r.VehicleId == vehicle.Id && r.Status == HourMeterReadingStatus.Valid)
            .OrderByDescending(r => r.ReadAt).ThenByDescending(r => r.CreatedAt)
            .Select(r => new HourMeterBaseline(r.Hours, r.ReadAt))
            .FirstOrDefaultAsync(ct);
        return last ?? new HourMeterBaseline(vehicle.HourMeter ?? 0, vehicle.HourMeterUpdatedAt ?? vehicle.CreatedAt);
    }

    private static void Apply(Vehicle vehicle, HourMeterReading reading)
    {
        vehicle.HourMeter = reading.Hours;
        vehicle.HourMeterUpdatedAt = reading.ReadAt;
    }

    private void Review(HourMeterReading reading, string? notes)
    {
        reading.ReviewedAt = clock.UtcNow;
        reading.ReviewedBy = currentUser.UserId;
        reading.ReviewNotes = notes.TrimToNull();
    }

    private async Task<HourMeterReading> LoadPendingAsync(Guid readingId, CancellationToken ct)
    {
        var reading = await db.HourMeterReadings.Include(r => r.Vehicle).SingleOrDefaultAsync(r => r.Id == readingId, ct)
            ?? throw new NotFoundException("Leitura de horímetro não encontrada.");
        if (reading.Status != HourMeterReadingStatus.PendingReview)
            throw new BusinessRuleException("Esta leitura já foi revisada.");
        return reading;
    }

    private async Task<PagedResult<HourMeterReadingResponse>> ToResponsesAsync(PagedResult<HourMeterReading> page, CancellationToken ct)
    {
        var items = await ToResponsesAsync(page.Items, ct);
        return new PagedResult<HourMeterReadingResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private async Task<IReadOnlyList<HourMeterReadingResponse>> ToResponsesAsync(IReadOnlyList<HourMeterReading> readings, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, readings.SelectMany(r => new[] { r.CreatedBy, r.ReviewedBy }), ct);
        return readings.Select(r => new HourMeterReadingResponse(
            r.Id, r.VehicleId, r.Hours, r.ReadAt, r.Source, r.Status, r.Anomaly, r.Notes, names.Get(r.CreatedBy), r.CreatedAt,
            r.ReviewedAt, names.Get(r.ReviewedBy), r.ReviewNotes)).ToList();
    }

    private static EventSubject Subject(HourMeterReading r) => new(nameof(HourMeterReading), r.Id, r.VehicleId);

    private static string Hours(decimal hours) => $"{BrazilianFormat.Number(hours, 1)} h";

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
