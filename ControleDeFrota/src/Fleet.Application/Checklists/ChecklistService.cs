using Fleet.Application.Assignments;
using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Application.Mileage;
using Fleet.Application.Occurrences;
using Fleet.Application.Operations;
using Fleet.Domain.Checklists;
using Fleet.Domain.Files;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Operations;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Checklists;

public sealed record ChecklistAnswerRequest
{
    public Guid? TemplateItemId { get; init; }
    public ChecklistChoice? Choice { get; init; }
    public decimal? NumberValue { get; init; }
    public string? TextValue { get; init; }
    public string? Comment { get; init; }
    /// <summary>Severity of a failure; defaults to the item's configured severity.</summary>
    public OccurrenceSeverity? Severity { get; init; }
    public IReadOnlyList<Guid>? FileIds { get; init; }
}

public sealed record ChecklistExecutionRequest
{
    public Guid? VehicleId { get; init; }
    public Guid? TemplateId { get; init; }
    /// <summary>Version the user filled in — refused if the template changed meanwhile.</summary>
    public int? TemplateVersion { get; init; }
    /// <summary>Driver inspected with; defaults to the vehicle's current driver.</summary>
    public Guid? DriverId { get; init; }
    public int? OdometerKm { get; init; }
    public string? Location { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<ChecklistAnswerRequest> Answers { get; init; } = [];
}

public sealed class ChecklistListRequest : ListRequest
{
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? TemplateId { get; set; }
    public ChecklistResult? Result { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed record ChecklistListItemResponse(
    Guid Id, DateTime PerformedAt, Guid VehicleId, string LicensePlate, Guid? DriverId, string? DriverName,
    Guid TemplateId, string TemplateName, int TemplateVersion, ChecklistResult Result, int FailedItems, string? PerformedByName);

public sealed record ChecklistAnswerResponse(
    Guid Id, int Position, string? Section, string Label, ChecklistResponseType ResponseType, bool IsRequired, string? Unit,
    ChecklistChoice? Choice, decimal? NumberValue, string? TextValue, string? Comment, OccurrenceSeverity? Severity,
    Guid? OccurrenceId, IReadOnlyList<FileResponse> Files);

public sealed record ChecklistExecutionResponse(
    Guid Id, DateTime PerformedAt, Guid VehicleId, string LicensePlate, Guid? DriverId, string? DriverName,
    Guid TemplateId, string TemplateName, int TemplateVersion, ChecklistFrequency Frequency, int? OdometerKm,
    OdometerReadingStatus? OdometerStatus, ChecklistResult Result, int FailedItems, string? Location, string? Notes,
    string? PerformedByName, IReadOnlyList<ChecklistAnswerResponse> Answers);

public sealed record PendingChecklistResponse(
    Guid VehicleId, string LicensePlate, string? DriverName, Guid TemplateId, string TemplateName, ChecklistFrequency Frequency,
    DateTime? LastPerformedAt);

public sealed class ChecklistListRequestValidator : AbstractValidator<ChecklistListRequest>
{
    public ChecklistListRequestValidator() => this.ValidPeriod(x => x.From, x => x.To, "to");
}

public sealed class ChecklistExecutionRequestValidator : AbstractValidator<ChecklistExecutionRequest>
{
    public ChecklistExecutionRequestValidator()
    {
        RuleFor(x => x.VehicleId).NotNull().WithMessage("Veículo: campo obrigatório.");
        RuleFor(x => x.TemplateId).NotNull().WithMessage("Modelo de checklist: campo obrigatório.");
        RuleFor(x => x.TemplateVersion).NotNull().WithMessage("Versão do modelo não informada. Recarregue a página.");
        RuleFor(x => x.OdometerKm).InclusiveBetween(0, OdometerPolicy.MaxOdometerKm).WithMessage("Hodômetro inválido.");
        RuleFor(x => x.Location).MaxLen(ChecklistExecution.LocationMaxLength);
        RuleFor(x => x.Notes).MaxLen(ChecklistExecution.NotesMaxLength);
        RuleForEach(x => x.Answers).ChildRules(a =>
        {
            a.RuleFor(x => x.TemplateItemId).NotNull().WithMessage("Item não identificado.");
            a.RuleFor(x => x.TextValue).MaxLen(ChecklistAnswer.TextMaxLength);
            a.RuleFor(x => x.Comment).MaxLen(ChecklistAnswer.CommentMaxLength);
            a.RuleFor(x => x.Choice).IsInEnum().WithMessage("Resposta inválida.");
            a.RuleFor(x => x.Severity).IsInEnum().WithMessage("Gravidade inválida.");
        });
    }
}

/// <summary>
/// Executes inspections (ADR-024): snapshot of the items, required-item validation, one occurrence per failed item,
/// optional odometer reading (same rules as MileageService) — all saved in one unit of work.
/// </summary>
public sealed class ChecklistService(
    IFleetDbContext db,
    IClock clock,
    FileService files,
    MileageService mileage,
    OccurrenceService occurrences,
    OperationalEventLog events,
    IValidator<ChecklistListRequest> listValidator,
    IValidator<ChecklistExecutionRequest> validator)
{
    public async Task<PagedResult<ChecklistListItemResponse>> ListAsync(ChecklistListRequest request, CancellationToken ct)
    {
        await listValidator.ValidateAndThrowAsync(request, ct);
        var query = db.ChecklistExecutions.AsQueryable();
        if (request.VehicleId is { } vehicleId) query = query.Where(e => e.VehicleId == vehicleId);
        if (request.DriverId is { } driverId) query = query.Where(e => e.DriverId == driverId);
        if (request.TemplateId is { } templateId) query = query.Where(e => e.TemplateId == templateId);
        if (request.Result is { } result) query = query.Where(e => e.Result == result);
        if (request.From is { } from) query = query.Where(e => e.PerformedOn >= from);
        if (request.To is { } to) query = query.Where(e => e.PerformedOn <= to);
        if (request.SearchTerm is { } term)
        {
            var plate = LicensePlate.Normalize(term);
            query = query.Where(e => e.Vehicle.LicensePlate.Contains(plate) || e.TemplateName.Contains(term) ||
                                     (e.Driver != null && e.Driver.FullName.Contains(term)));
        }

        var page = await query.OrderByDescending(e => e.PerformedAt).ThenByDescending(e => e.Id).ToPagedResultAsync(request, e => new
        {
            e.Id, e.PerformedAt, e.VehicleId, e.Vehicle.LicensePlate, e.DriverId, DriverName = e.Driver != null ? e.Driver.FullName : null,
            e.TemplateId, e.TemplateName, e.TemplateVersion, e.Result, e.FailedItems, e.CreatedBy,
        }, ct);
        var names = await UserNames.LoadAsync(db, page.Items.Select(e => e.CreatedBy), ct);
        var items = page.Items.Select(e => new ChecklistListItemResponse(
            e.Id, e.PerformedAt, e.VehicleId, e.LicensePlate, e.DriverId, e.DriverName, e.TemplateId, e.TemplateName,
            e.TemplateVersion, e.Result, e.FailedItems, names.Get(e.CreatedBy))).ToList();
        return new PagedResult<ChecklistListItemResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<ChecklistExecutionResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var execution = await db.ChecklistExecutions.Include(e => e.Answers).Include(e => e.Vehicle).Include(e => e.Driver)
            .SingleOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException("Checklist não encontrado.");
        var odometerStatus = await db.OdometerReadings.Where(r => r.ChecklistExecutionId == id)
            .Select(r => (OdometerReadingStatus?)r.Status).FirstOrDefaultAsync(ct);
        var names = await UserNames.LoadAsync(db, [execution.CreatedBy], ct);
        var photos = await files.ListByOwnersAsync(FileOwnerType.ChecklistAnswer, execution.Answers.Select(a => a.Id).ToList(), ct);

        return new ChecklistExecutionResponse(
            execution.Id, execution.PerformedAt, execution.VehicleId, execution.Vehicle.LicensePlate, execution.DriverId,
            execution.Driver?.FullName, execution.TemplateId, execution.TemplateName, execution.TemplateVersion, execution.Frequency,
            execution.OdometerKm, odometerStatus, execution.Result, execution.FailedItems, execution.Location, execution.Notes,
            names.Get(execution.CreatedBy),
            execution.Answers.OrderBy(a => a.Position).Select(a => new ChecklistAnswerResponse(
                a.Id, a.Position, a.Section, a.Label, a.ResponseType, a.IsRequired, a.Unit, a.Choice, a.NumberValue, a.TextValue,
                a.Comment, a.Severity, a.OccurrenceId, photos.GetValueOrDefault(a.Id) ?? [])).ToList());
    }

    public async Task<ChecklistExecutionResponse> SubmitAsync(ChecklistExecutionRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var template = await db.ChecklistTemplates.Include(t => t.Items).SingleOrDefaultAsync(t => t.Id == request.TemplateId, ct)
            ?? throw ValidationErrors.ForField("templateId", "Modelo de checklist não encontrado.");
        if (!template.IsActive)
            throw new BusinessRuleException("Este modelo de checklist foi desativado. Escolha outro modelo.");
        if (template.Version != request.TemplateVersion)
            throw new ConflictException("O modelo de checklist foi alterado enquanto você preenchia. Recarregue a página para ver a versão atual.");

        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == request.VehicleId, ct)
            ?? throw ValidationErrors.ForField("vehicleId", "Veículo não encontrado. Selecione um veículo da lista.");
        if (vehicle.Status == VehicleStatus.Inactive)
            throw new BusinessRuleException("Este veículo está inativo e não pode ser inspecionado.");

        var driverId = await ResolveDriverAsync(request.DriverId, vehicle.Id, ct);
        var now = clock.UtcNow;
        var execution = new ChecklistExecution
        {
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            DriverId = driverId,
            TemplateId = template.Id,
            TemplateName = template.Name,
            TemplateVersion = template.Version,
            Frequency = template.Frequency,
            PerformedAt = now,
            PerformedOn = clock.Today,
            OdometerKm = request.OdometerKm,
            Location = request.Location.TrimToNull(),
            Notes = request.Notes.TrimToNull(),
            Answers = BuildAnswers(template, request.Answers),
        };
        execution.FailedItems = execution.Answers.Count(a => a.IsFailure);
        execution.Result = execution.FailedItems > 0 ? ChecklistResult.Failed : ChecklistResult.Approved;
        db.ChecklistExecutions.Add(execution);

        foreach (var (answer, answerRequest) in Pair(execution.Answers, request.Answers))
            await files.AttachAsync(answerRequest.FileIds, FileOwnerType.ChecklistAnswer, answer.Id, $"answers.{answer.TemplateItemId}", ct);

        if (request.OdometerKm is { } km)
            await mileage.AddReadingAsync(vehicle, km, now, OdometerReadingSource.Checklist, $"Checklist {template.Name}", execution.Id, "odometerKm", ct);

        await OpenOccurrencesAsync(execution, template, ct);
        RecordEvents(execution, vehicle);
        await db.SaveChangesAsync(ct);
        return await GetAsync(execution.Id, ct);
    }

    /// <summary>
    /// Recurring checklists (daily/weekly) that vehicles in operation still owe for the current period
    /// (ChecklistSchedule). Computed in memory: a company has few recurring templates.
    /// </summary>
    public async Task<IReadOnlyList<PendingChecklistResponse>> PendingAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var templates = await db.ChecklistTemplates
            .Where(t => t.IsActive && t.Frequency != ChecklistFrequency.OnDemand)
            .Select(t => new { t.Id, t.Name, t.Frequency })
            .ToListAsync(ct);
        if (templates.Count == 0) return [];

        var vehicles = await db.Vehicles
            .Where(v => v.Status == VehicleStatus.Available || v.Status == VehicleStatus.OnTrip)
            .Select(v => new
            {
                v.Id, v.LicensePlate,
                DriverName = AssignmentService.Active(db).Where(a => a.VehicleId == v.Id).Select(a => a.Driver.FullName).FirstOrDefault(),
            })
            .Where(v => v.DriverName != null)
            .ToListAsync(ct);

        var since = templates.Min(t => ChecklistSchedule.PeriodStart(t.Frequency, today));
        var templateIds = templates.Select(t => t.Id).ToList();
        var done = await db.ChecklistExecutions
            .Where(e => e.PerformedOn >= since && templateIds.Contains(e.TemplateId))
            .GroupBy(e => new { e.VehicleId, e.TemplateId })
            .Select(g => new { g.Key.VehicleId, g.Key.TemplateId, LastOn = g.Max(e => e.PerformedOn), LastAt = g.Max(e => e.PerformedAt) })
            .ToListAsync(ct);
        var lastDone = done.ToDictionary(d => (d.VehicleId, d.TemplateId));

        return (from vehicle in vehicles
                from template in templates
                let last = lastDone.GetValueOrDefault((vehicle.Id, template.Id))
                where last is null || last.LastOn < ChecklistSchedule.PeriodStart(template.Frequency, today)
                orderby vehicle.LicensePlate, template.Name
                select new PendingChecklistResponse(vehicle.Id, vehicle.LicensePlate, vehicle.DriverName, template.Id, template.Name,
                    template.Frequency, last?.LastAt)).ToList();
    }

    private async Task<Guid?> ResolveDriverAsync(Guid? requested, Guid vehicleId, CancellationToken ct)
    {
        if (requested is { } driverId)
        {
            return await db.Drivers.AnyAsync(d => d.Id == driverId, ct)
                ? driverId
                : throw ValidationErrors.ForField("driverId", "Motorista não encontrado. Selecione um motorista da lista.");
        }
        return await AssignmentService.Active(db).Where(a => a.VehicleId == vehicleId).Select(a => (Guid?)a.DriverId).FirstOrDefaultAsync(ct);
    }

    /// <summary>Copies each question from the template (snapshot) and validates the answer against it.</summary>
    private static List<ChecklistAnswer> BuildAnswers(ChecklistTemplate template, IReadOnlyList<ChecklistAnswerRequest> requests)
    {
        var failures = new List<ValidationFailure>();
        var byItem = new Dictionary<Guid, ChecklistAnswerRequest>();
        foreach (var answer in requests)
        {
            var itemId = answer.TemplateItemId!.Value;
            if (template.Items.All(i => i.Id != itemId))
                failures.Add(new ValidationFailure("answers", "Uma das respostas não pertence a este modelo. Recarregue a página."));
            else if (!byItem.TryAdd(itemId, answer))
                failures.Add(new ValidationFailure($"answers.{itemId}", "Item respondido mais de uma vez."));
        }

        var answers = new List<ChecklistAnswer>();
        foreach (var item in template.Items.OrderBy(i => i.Position))
        {
            var request = byItem.GetValueOrDefault(item.Id);
            var answer = new ChecklistAnswer
            {
                TemplateItemId = item.Id,
                Position = item.Position,
                Section = item.Section,
                Label = item.Label,
                ResponseType = item.ResponseType,
                IsRequired = item.IsRequired,
                Unit = item.Unit,
                Comment = request?.Comment.TrimToNull(),
            };
            var key = $"answers.{item.Id}";
            switch (item.ResponseType)
            {
                case ChecklistResponseType.PassFail:
                    answer.Choice = request?.Choice;
                    if (request is { NumberValue: not null } or { TextValue: not null })
                        failures.Add(new ValidationFailure(key, "Responda com Conforme, Não conforme ou Não se aplica."));
                    break;
                case ChecklistResponseType.Number:
                    answer.NumberValue = request?.NumberValue;
                    break;
                default:
                    answer.TextValue = request?.TextValue.TrimToNull();
                    break;
            }

            if (item.IsRequired && !answer.IsAnswered)
                failures.Add(new ValidationFailure(key, $"Responda o item obrigatório \"{item.Label}\"."));
            if (answer.IsFailure)
            {
                answer.Severity = request?.Severity ?? item.FailureSeverity;
                if (item.RequiresPhotoOnFail && (request?.FileIds is null || request.FileIds.Count == 0))
                    failures.Add(new ValidationFailure(key, $"Anexe uma foto do problema em \"{item.Label}\"."));
            }
            answers.Add(answer);
        }

        if (failures.Count > 0) throw new ValidationException(failures);
        return answers;
    }

    /// <summary>Checklist → failed item → occurrence: the future maintenance module consumes these.</summary>
    private async Task OpenOccurrencesAsync(ChecklistExecution execution, ChecklistTemplate template, CancellationToken ct)
    {
        var items = template.Items.ToDictionary(i => i.Id);
        foreach (var answer in execution.Answers.Where(a => a.IsFailure))
        {
            var item = items[answer.TemplateItemId];
            var what = answer.Section is null ? answer.Label : $"{answer.Section} — {answer.Label}";
            var occurrence = new Occurrence
            {
                VehicleId = execution.VehicleId,
                DriverId = execution.DriverId,
                Type = item.FailureOccurrenceType,
                Severity = answer.Severity!.Value,
                OccurredAt = execution.PerformedAt,
                Location = execution.Location,
                Description = Truncate($"{what}: não conforme no checklist \"{execution.TemplateName}\"." +
                                       (answer.Comment is null ? string.Empty : $" {answer.Comment}"), Occurrence.DescriptionMaxLength),
                Source = OccurrenceSource.Checklist,
                ChecklistExecutionId = execution.Id,
            };
            db.Occurrences.Add(occurrence);
            answer.OccurrenceId = occurrence.Id;
            await occurrences.RecordCreatedAsync(occurrence, ct);
        }
    }

    private void RecordEvents(ChecklistExecution execution, Vehicle vehicle)
    {
        var subject = new EventSubject(nameof(ChecklistExecution), execution.Id, execution.VehicleId, execution.DriverId);
        var plate = LicensePlate.Format(vehicle.LicensePlate);
        events.Record(OperationalEventType.ChecklistCompleted, subject,
            execution.Result == ChecklistResult.Approved
                ? $"Checklist \"{execution.TemplateName}\" realizado em {plate}: aprovado."
                : $"Checklist \"{execution.TemplateName}\" realizado em {plate}: {execution.FailedItems} item(ns) não conforme(s).",
            new { execution.TemplateId, execution.TemplateVersion, execution.Result, execution.FailedItems });
        if (execution.Result == ChecklistResult.Failed)
        {
            events.Record(OperationalEventType.ChecklistFailed, subject,
                $"Checklist \"{execution.TemplateName}\" reprovado em {plate}: {execution.FailedItems} ocorrência(s) aberta(s).",
                new { execution.TemplateId, execution.FailedItems });
        }
    }

    private static IEnumerable<(ChecklistAnswer Answer, ChecklistAnswerRequest Request)> Pair(
        IEnumerable<ChecklistAnswer> answers, IReadOnlyList<ChecklistAnswerRequest> requests) =>
        from answer in answers
        join request in requests on answer.TemplateItemId equals request.TemplateItemId
        select (answer, request);

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
