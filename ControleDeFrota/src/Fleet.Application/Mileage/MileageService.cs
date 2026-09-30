using Fleet.Application.Common;
using Fleet.Application.Operations;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Mileage;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Mileage;

public sealed record OdometerReadingRequest
{
    public int? OdometerKm { get; init; }
    /// <summary>When the odometer was read. Defaults to now; never in the future.</summary>
    public DateTime? ReadAt { get; init; }
    public string? Notes { get; init; }
    /// <summary>Audited correction (mileage.manage): may be lower than the previous reading; requires the reason in Notes.</summary>
    public bool IsCorrection { get; init; }
}

public sealed record OdometerReviewRequest
{
    public string? Notes { get; init; }
}

public sealed class OdometerReadingListRequest : ListRequest
{
    public OdometerReadingStatus? Status { get; set; }
}

public sealed record OdometerReadingResponse(
    Guid Id,
    Guid VehicleId,
    int OdometerKm,
    DateTime ReadAt,
    OdometerReadingSource Source,
    OdometerReadingStatus Status,
    string? Anomaly,
    string? Notes,
    string? RecordedByName,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByName,
    string? ReviewNotes);

public sealed class OdometerReadingRequestValidator : AbstractValidator<OdometerReadingRequest>
{
    public OdometerReadingRequestValidator(IClock clock)
    {
        RuleFor(x => x.OdometerKm)
            .NotNull().WithMessage("Hodômetro: informe a leitura em km.")
            .InclusiveBetween(0, OdometerPolicy.MaxOdometerKm)
            .WithMessage($"Hodômetro deve estar entre 0 e {BrazilianFormat.Number(OdometerPolicy.MaxOdometerKm)} km.");
        RuleFor(x => x.ReadAt)
            .Must(d => d is null || d.Value <= clock.UtcNow.Add(MileageService.ClockTolerance))
            .WithMessage("A data da leitura não pode ser futura.");
        RuleFor(x => x.Notes).MaxLen(OdometerReading.NotesMaxLength);
        RuleFor(x => x.Notes)
            .Must(n => !string.IsNullOrWhiteSpace(n)).When(x => x.IsCorrection)
            .WithMessage("Informe o motivo da correção (ex.: troca do painel, leitura anterior digitada errada).");
    }
}

public sealed class OdometerReviewRequestValidator : AbstractValidator<OdometerReviewRequest>
{
    public OdometerReviewRequestValidator() => RuleFor(x => x.Notes).MaxLen(OdometerReading.NotesMaxLength);
}

/// <summary>
/// Odometer history and the vehicle's current mileage (ADR-019). This service is the only writer of
/// Vehicle.CurrentOdometerKm after registration: every change goes through a reading.
/// </summary>
public sealed class MileageService(
    IFleetDbContext db,
    IClock clock,
    ICurrentUser currentUser,
    OperationalEventLog events,
    IValidator<OdometerReadingRequest> validator,
    IValidator<OdometerReviewRequest> reviewValidator)
{
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    public async Task<PagedResult<OdometerReadingResponse>> ListAsync(Guid vehicleId, OdometerReadingListRequest request, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");

        var query = db.OdometerReadings.Where(r => r.VehicleId == vehicleId);
        if (request.Status is { } status) query = query.Where(r => r.Status == status);
        var page = await query.OrderByDescending(r => r.ReadAt).ThenByDescending(r => r.CreatedAt)
            .ToPagedResultAsync(request, r => r, ct);
        return await ToResponsesAsync(page, ct);
    }

    public async Task<OdometerReadingResponse> RecordAsync(Guid vehicleId, OdometerReadingRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        if (request.IsCorrection && !currentUser.HasPermission(Permissions.Mileage.Manage))
            throw new ForbiddenException("Somente gestores podem registrar correções de hodômetro. Registre uma leitura comum ou peça a correção a um gestor.");
        if (!request.IsCorrection && !currentUser.HasPermission(Permissions.Mileage.Record) && !currentUser.HasPermission(Permissions.Mileage.Manage))
            throw new ForbiddenException("Você não tem permissão para registrar leituras de hodômetro.");

        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException("Veículo não encontrado. Ele pode ter sido excluído.");
        if (vehicle.Status == VehicleStatus.Inactive && !request.IsCorrection)
            throw new BusinessRuleException("Este veículo está inativo. Reative-o para registrar novas leituras.");

        var reading = await AddReadingAsync(vehicle, request.OdometerKm!.Value, Min(request.ReadAt ?? clock.UtcNow, clock.UtcNow),
            request.IsCorrection ? OdometerReadingSource.Correction : OdometerReadingSource.Manual,
            request.Notes.TrimToNull(), checklistExecutionId: null, field: "odometerKm", ct);
        await db.SaveChangesAsync(ct);
        return (await ToResponsesAsync([reading], ct)).Single();
    }

    /// <summary>
    /// Validates and adds a reading to the unit of work WITHOUT saving — reused by the checklist execution so the
    /// reading and the inspection are saved together. Valid readings update the vehicle; suspicious ones wait for review.
    /// </summary>
    public async Task<OdometerReading> AddReadingAsync(Vehicle vehicle, int odometerKm, DateTime readAt, OdometerReadingSource source,
        string? notes, Guid? checklistExecutionId, string field, CancellationToken ct)
    {
        var baseline = await BaselineAsync(vehicle, ct);
        if (readAt < baseline.ReadAt)
            throw ValidationErrors.ForField(field == "odometerKm" ? "readAt" : field,
                $"A leitura não pode ser anterior à última leitura registrada ({clock.FormatDateTime(baseline.ReadAt)}). " +
                "Para ajustar o histórico, peça uma correção a um gestor.");

        var reading = new OdometerReading
        {
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            OdometerKm = odometerKm,
            ReadAt = readAt,
            Source = source,
            Notes = notes,
            ChecklistExecutionId = checklistExecutionId,
        };
        var plate = LicensePlate.Format(vehicle.LicensePlate);

        if (source == OdometerReadingSource.Correction)
        {
            db.OdometerReadings.Add(reading);
            var previous = vehicle.CurrentOdometerKm;
            Apply(vehicle, reading);
            events.Record(OperationalEventType.MileageCorrected, Subject(reading),
                $"Hodômetro de {plate} corrigido de {Km(previous)} para {Km(odometerKm)}. Motivo: {notes}",
                new { previousKm = previous, odometerKm });
            return reading;
        }

        var evaluation = OdometerPolicy.Evaluate(odometerKm, readAt, baseline);
        if (evaluation.Check == OdometerCheck.Decrease)
        {
            throw ValidationErrors.ForField(field,
                $"A leitura ({Km(odometerKm)}) é menor que a última leitura válida ({Km(baseline.OdometerKm)} em " +
                $"{BrazilianFormat.Date(clock.ToBusinessDate(baseline.ReadAt))}). Confira o valor. Se o hodômetro foi trocado ou a " +
                "leitura anterior estava errada, peça uma correção a um gestor.");
        }

        db.OdometerReadings.Add(reading);
        switch (evaluation.Check)
        {
            case OdometerCheck.SuspiciousJump:
                reading.Status = OdometerReadingStatus.PendingReview;
                reading.Anomaly = evaluation.Anomaly;
                events.Record(OperationalEventType.MileageAnomalyDetected, Subject(reading),
                    $"Leitura suspeita em {plate}: {Km(odometerKm)}. {evaluation.Anomaly} Aguardando revisão.",
                    new { odometerKm, baselineKm = baseline.OdometerKm, evaluation.Anomaly });
                break;
            default:
                Apply(vehicle, reading);
                events.Record(OperationalEventType.MileageRecorded, Subject(reading),
                    $"Hodômetro de {plate} atualizado para {Km(odometerKm)}.",
                    new { odometerKm, deltaKm = odometerKm - baseline.OdometerKm, source });
                break;
        }
        return reading;
    }

    public async Task<OdometerReadingResponse> ApproveAsync(Guid readingId, OdometerReviewRequest request, CancellationToken ct)
    {
        await reviewValidator.ValidateAndThrowAsync(request, ct);
        var reading = await LoadPendingAsync(readingId, ct);
        var baseline = await BaselineAsync(reading.Vehicle, ct);
        // Approving is only safe while this is still the newest reading and does not go backwards.
        if (baseline.ReadAt > reading.ReadAt || reading.OdometerKm < baseline.OdometerKm)
            throw new BusinessRuleException(
                "Há leituras válidas posteriores a esta, então ela não pode mais ser aplicada. Rejeite-a ou registre uma correção.");

        reading.Status = OdometerReadingStatus.Valid;
        Review(reading, request.Notes);
        Apply(reading.Vehicle, reading);
        events.Record(OperationalEventType.MileageReviewed, Subject(reading),
            $"Leitura de {Km(reading.OdometerKm)} em {LicensePlate.Format(reading.Vehicle.LicensePlate)} confirmada após revisão.",
            new { reading.OdometerKm, approved = true });
        await db.SaveChangesAsync(ct);
        return (await ToResponsesAsync([reading], ct)).Single();
    }

    public async Task<OdometerReadingResponse> RejectAsync(Guid readingId, OdometerReviewRequest request, CancellationToken ct)
    {
        await reviewValidator.ValidateAndThrowAsync(request, ct);
        if (string.IsNullOrWhiteSpace(request.Notes))
            throw ValidationErrors.ForField("notes", "Informe o motivo da rejeição.");
        var reading = await LoadPendingAsync(readingId, ct);

        reading.Status = OdometerReadingStatus.Rejected;
        Review(reading, request.Notes);
        events.Record(OperationalEventType.MileageReviewed, Subject(reading),
            $"Leitura de {Km(reading.OdometerKm)} em {LicensePlate.Format(reading.Vehicle.LicensePlate)} rejeitada: {reading.ReviewNotes}",
            new { reading.OdometerKm, approved = false });
        await db.SaveChangesAsync(ct);
        return (await ToResponsesAsync([reading], ct)).Single();
    }

    /// <summary>
    /// The last valid reading. Vehicles registered before the reading history existed fall back to the stored
    /// current odometer, so the "never go backwards" rule holds for them too.
    /// </summary>
    private async Task<OdometerBaseline> BaselineAsync(Vehicle vehicle, CancellationToken ct)
    {
        var last = await db.OdometerReadings
            .Where(r => r.VehicleId == vehicle.Id && r.Status == OdometerReadingStatus.Valid)
            .OrderByDescending(r => r.ReadAt).ThenByDescending(r => r.CreatedAt)
            .Select(r => new OdometerBaseline(r.OdometerKm, r.ReadAt))
            .FirstOrDefaultAsync(ct);
        return last ?? new OdometerBaseline(vehicle.CurrentOdometerKm, vehicle.OdometerUpdatedAt ?? vehicle.CreatedAt);
    }

    private static void Apply(Vehicle vehicle, OdometerReading reading)
    {
        vehicle.CurrentOdometerKm = reading.OdometerKm;
        vehicle.OdometerUpdatedAt = reading.ReadAt;
    }

    private void Review(OdometerReading reading, string? notes)
    {
        reading.ReviewedAt = clock.UtcNow;
        reading.ReviewedBy = currentUser.UserId;
        reading.ReviewNotes = notes.TrimToNull();
    }

    private async Task<OdometerReading> LoadPendingAsync(Guid readingId, CancellationToken ct)
    {
        var reading = await db.OdometerReadings.Include(r => r.Vehicle).SingleOrDefaultAsync(r => r.Id == readingId, ct)
            ?? throw new NotFoundException("Leitura de hodômetro não encontrada.");
        if (reading.Status != OdometerReadingStatus.PendingReview)
            throw new BusinessRuleException("Esta leitura já foi revisada.");
        return reading;
    }

    private async Task<PagedResult<OdometerReadingResponse>> ToResponsesAsync(PagedResult<OdometerReading> page, CancellationToken ct)
    {
        var items = await ToResponsesAsync(page.Items, ct);
        return new PagedResult<OdometerReadingResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    private async Task<IReadOnlyList<OdometerReadingResponse>> ToResponsesAsync(IReadOnlyList<OdometerReading> readings, CancellationToken ct)
    {
        var names = await UserNames.LoadAsync(db, readings.SelectMany(r => new[] { r.CreatedBy, r.ReviewedBy }), ct);
        return readings.Select(r => new OdometerReadingResponse(
            r.Id, r.VehicleId, r.OdometerKm, r.ReadAt, r.Source, r.Status, r.Anomaly, r.Notes, names.Get(r.CreatedBy), r.CreatedAt,
            r.ReviewedAt, names.Get(r.ReviewedBy), r.ReviewNotes)).ToList();
    }

    private static EventSubject Subject(OdometerReading r) => new(nameof(OdometerReading), r.Id, r.VehicleId);

    private static string Km(int km) => $"{BrazilianFormat.Number(km)} km";

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
