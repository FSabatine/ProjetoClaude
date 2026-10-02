using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Application.Maintenance;
using Fleet.Domain.Files;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tires;

public sealed record TireInspectionRequest
{
    public DateTime? InspectedAt { get; init; }
    /// <summary>Vehicle odometer read now (optional) — recorded in the mileage history like any reading.</summary>
    public int? OdometerKm { get; init; }
    public decimal? TreadDepthMm { get; init; }
    public decimal? Pressure { get; init; }
    public TirePressureUnit? PressureUnit { get; init; }
    public TireCondition? Condition { get; init; }
    public TireWearPattern WearPattern { get; init; } = TireWearPattern.Normal;
    public IReadOnlyList<TireDamageType>? Damages { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
    /// <summary>The occurrence this inspection follows up (e.g. a checklist "pneu" item that failed).</summary>
    public Guid? OccurrenceId { get; init; }
}

public sealed record TireInspectionResponse(
    Guid Id, Guid TireId, string TireCode, DateTime InspectedAt, TireInspectionSource Source, Guid? VehicleId, Guid? ImplementId, string? AssetLabel,
    string? PositionCode, string? PositionLabel, int? OdometerKm, int? TireKm, decimal? TreadDepthMm, decimal? Pressure, TirePressureUnit? PressureUnit,
    TirePressureCheck? PressureCheck, TireCondition Condition, TireWearPattern WearPattern, IReadOnlyList<TireDamageType> Damages, string? Notes,
    bool RequiresAction, Guid? OccurrenceId, Guid? MaintenanceRequestId, string? InspectedByName, IReadOnlyList<FileResponse> Files);

public sealed record TireAnomalyReviewRequest
{
    public string? Notes { get; init; }
}

public sealed class TireInspectionRequestValidator : AbstractValidator<TireInspectionRequest>
{
    public TireInspectionRequestValidator()
    {
        RuleFor(x => x.Condition).NotNull().WithMessage("Condição visual: campo obrigatório.").IsInEnum().WithMessage("Condição inválida.");
        RuleFor(x => x.WearPattern).IsInEnum().WithMessage("Tipo de desgaste inválido.");
        RuleFor(x => x.PressureUnit).IsInEnum().WithMessage("Unidade de pressão inválida.");
        TireMeasurementRules.Apply(this, x => x.TreadDepthMm, x => x.Pressure, x => x.Damages!);
        RuleFor(x => x.Notes).MaxLen(TireInspection.NotesMaxLength);
        RuleFor(x => x.Notes).Must(n => !string.IsNullOrWhiteSpace(n))
            .When(x => x.Damages?.Contains(TireDamageType.Other) == true)
            .WithMessage("Descreva o dano marcado como \"Outro\".");
    }
}

/// <summary>
/// Tire inspections (seções 16–21, 68): measurements, findings, photos and the alerts/rules they trigger. A failed inspection
/// may open a maintenance request only when the company turned that rule on (seções 36/62) — never by default.
/// </summary>
public sealed class TireInspectionService(
    IFleetDbContext db,
    TireLifecycle lifecycle,
    TireMonitoring monitoring,
    TireService tires,
    TireSettingsService settingsService,
    MaintenanceRequestService maintenanceRequests,
    FileService files,
    IValidator<TireInspectionRequest> validator)
{
    public async Task<TireInspectionResponse> InspectAsync(Guid tireId, TireInspectionRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var tire = await lifecycle.LoadTireAsync(tireId, ct);
        if (!TireWorkflow.CanInspect(tire.Status))
            throw new BusinessRuleException($"O pneu {tire.Code} foi baixado: não recebe novas inspeções.");
        if (request.OccurrenceId is { } occurrenceId && !await db.Occurrences.AnyAsync(o => o.Id == occurrenceId, ct))
            throw ValidationErrors.ForField("occurrenceId", "Ocorrência não encontrada.");

        // An inspection may be typed later, but is not allowed in the future; it does not move the lifecycle clock.
        var now = lifecycle.Now;
        var at = request.InspectedAt is { } requested ? requested : now;
        if (at > now.AddMinutes(5)) throw ValidationErrors.ForField("inspectedAt", "A data da inspeção não pode ser futura.");
        if (at > now) at = now;
        var stint = await lifecycle.OpenStintAsync(tireId, ct);
        if (stint is not null && at < stint.InstalledAt) stint = null; // before the current installation: the tire was not here
        var asset = stint is null ? null : await lifecycle.AssetOfAsync(stint, ct);
        var settings = await settingsService.CurrentAsync(ct);

        TireInspection inspection = null!;
        await lifecycle.RunAsync(async () =>
        {
            var km = asset is null ? null : await lifecycle.OdometerAsync(asset, at, request.OdometerKm, $"Informado na inspeção do pneu {tire.Code}.", ct);
            inspection = await monitoring.RecordAsync(tire, stint, asset, at, new TireMeasurementInput(TireInspectionSource.Inspection, km,
                request.TreadDepthMm, request.Pressure, request.PressureUnit, request.Condition!.Value, request.WearPattern, request.Damages ?? [],
                request.Notes.TrimToNull(), request.OccurrenceId), settings, ct);
            tire.Version++;
            await files.AttachAsync(request.FileIds, FileOwnerType.TireInspection, inspection.Id, "fileIds", ct);
            RecordEvents(tire, asset, inspection, settings);

            if (settings.AutoMaintenanceRequestOnUnfit && inspection.Condition == TireCondition.Unfit && asset?.Vehicle is { } vehicle)
            {
                var maintenance = maintenanceRequests.AddAutomatic(vehicle,
                    $"Pneu {tire.Code} ({inspection.PositionLabel}) inspecionado como impróprio para uso. " +
                    (inspection.Notes ?? "Verificar o pneu e a posição.") + " (Regra da empresa: inspeção imprópria abre solicitação.)",
                    MaintenancePriority.High, km);
                inspection.MaintenanceRequestId = maintenance.Id;
            }
            await db.SaveChangesAsync(ct);
        }, ct);
        return (await ToResponsesAsync([inspection.Id], ct)).Single();
    }

    public async Task<PagedResult<TireInspectionResponse>> ListAsync(Guid tireId, ListRequest request, CancellationToken ct)
    {
        if (!await db.Tires.AnyAsync(t => t.Id == tireId, ct)) throw new NotFoundException("Pneu não encontrado. Ele pode ter sido excluído.");
        var page = await db.TireInspections.Where(i => i.TireId == tireId)
            .OrderByDescending(i => i.InspectedAt).ThenByDescending(i => i.CreatedAt)
            .ToPagedResultAsync(request, i => i.Id, ct);
        return new PagedResult<TireInspectionResponse>(await ToResponsesAsync(page.Items, ct), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<TireResponse> ReviewAnomalyAsync(Guid anomalyId, TireAnomalyReviewRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Notes))
            throw ValidationErrors.ForField("notes", "Descreva o que foi verificado (ex.: \"alinhamento conferido, sem problema\").");
        if (request.Notes.Trim().Length > TireInstallation.NotesMaxLength)
            throw ValidationErrors.ForField("notes", $"Use no máximo {TireInstallation.NotesMaxLength} caracteres.");
        var anomaly = await db.TireAnomalies.SingleOrDefaultAsync(a => a.Id == anomalyId, ct)
            ?? throw new NotFoundException("Alerta do pneu não encontrado.");
        if (anomaly.ReviewedAt is not null) throw new BusinessRuleException("Este alerta já foi revisado.");
        anomaly.ReviewedAt = lifecycle.Now;
        anomaly.ReviewedBy = lifecycle.UserId;
        anomaly.ReviewNotes = request.Notes.Trim();
        await db.SaveChangesAsync(ct);
        return await tires.GetAsync(anomaly.TireId, ct);
    }

    private void RecordEvents(Tire tire, TireAsset? asset, TireInspection inspection, TireSettings settings)
    {
        var where = inspection.PositionLabel is { } label ? $" em {asset!.Label}, {label}" : "";
        var facts = new List<string>();
        if (inspection.TreadDepthMm is { } tread) facts.Add($"sulco {TireLifecycle.Mm(tread)}");
        if (inspection.Pressure is { } pressure) facts.Add($"pressão {Fleet.Domain.Common.BrazilianFormat.Compact(pressure)} {PressureUnitText(inspection.PressureUnit)}");
        var data = new { tireId = tire.Id, inspectionId = inspection.Id, inspection.TreadDepthMm, inspection.Condition, inspection.PressureCheck };
        lifecycle.Record(OperationalEventType.TireInspected, tire, asset, inspection.InspectedAt,
            $"Pneu {tire.Code} inspecionado{where}" + (facts.Count > 0 ? $": {string.Join(", ", facts)}." : "."), data);
        if (inspection.RequiresAction)
            lifecycle.Record(OperationalEventType.TireInspectionFailed, tire, asset, inspection.InspectedAt,
                $"Inspeção do pneu {tire.Code}{where} requer atenção: " + FindingsText(inspection), data);
        if (inspection.TreadDepthMm is { } t && t <= settings.MinTreadDepthMm)
            lifecycle.Record(OperationalEventType.TireTreadLow, tire, asset, inspection.InspectedAt,
                $"Sulco do pneu {tire.Code} ({TireLifecycle.Mm(t)}) no mínimo configurado pela empresa ({TireLifecycle.Mm(settings.MinTreadDepthMm)}).", data,
                tireOnly: asset is null);
        if (inspection.PressureCheck == TirePressureCheck.Low)
            lifecycle.Record(OperationalEventType.TirePressureLow, tire, asset, inspection.InspectedAt,
                $"Pressão do pneu {tire.Code}{where} abaixo da referência.", data, tireOnly: asset is null);
    }

    private static string FindingsText(TireInspection i)
    {
        var parts = new List<string>();
        if (i.Condition == TireCondition.Unfit) parts.Add("impróprio para uso");
        else if (i.Condition == TireCondition.Attention) parts.Add("requer acompanhamento");
        if (i.Damages.Count > 0) parts.Add($"{i.Damages.Count} dano(s) registrado(s)");
        if (i.WearPattern is not (TireWearPattern.Normal or TireWearPattern.Unknown)) parts.Add("desgaste irregular");
        return string.Join(", ", parts) + ".";
    }

    private static string PressureUnitText(TirePressureUnit? unit) => unit switch
    {
        TirePressureUnit.Bar => "bar",
        TirePressureUnit.Kpa => "kPa",
        _ => "psi",
    };

    private async Task<IReadOnlyList<TireInspectionResponse>> ToResponsesAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var rows = await db.TireInspections.Where(i => ids.Contains(i.Id))
            .Select(i => new
            {
                Inspection = i,
                Damages = i.Damages.Select(d => d.Type).ToList(),
                i.Tire.Code,
                Plate = i.Vehicle != null ? i.Vehicle.LicensePlate : i.Implement != null ? i.Implement.LicensePlate : null,
            }).ToListAsync(ct);
        var names = await UserNames.LoadAsync(db, rows.Select(r => r.Inspection.CreatedBy), ct);
        var photos = await files.ListByOwnersAsync(FileOwnerType.TireInspection, ids.ToList(), ct);
        return ids.Select(id => rows.Single(r => r.Inspection.Id == id)).Select(r =>
        {
            var i = r.Inspection;
            return new TireInspectionResponse(i.Id, i.TireId, r.Code, i.InspectedAt, i.Source, i.VehicleId, i.ImplementId,
                r.Plate is null ? null : Fleet.Domain.Validation.LicensePlate.Format(r.Plate), i.PositionCode, i.PositionLabel, i.OdometerKm, i.TireKm,
                i.TreadDepthMm, i.Pressure, i.PressureUnit, i.PressureCheck, i.Condition, i.WearPattern, r.Damages.OrderBy(d => d).ToList(),
                i.Notes, i.Condition != TireCondition.Good || r.Damages.Count > 0 || i.WearPattern is not (TireWearPattern.Normal or TireWearPattern.Unknown), i.OccurrenceId, i.MaintenanceRequestId, names.Get(i.CreatedBy), photos.GetValueOrDefault(i.Id) ?? []);
        }).ToList();
    }
}
