using FluentAssertions;
using Fleet.Application.Checklists;
using Fleet.Application.Common;
using Fleet.Application.Tests.Files;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Checklists;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Operations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Checklists;

public class ChecklistServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private ChecklistService Service => Services.Checklists(_t);

    private static ChecklistTemplateRequest Template(ChecklistFrequency frequency = ChecklistFrequency.Daily) => new()
    {
        Name = "Inspeção diária",
        Frequency = frequency,
        Items =
        [
            new() { Section = "Pneus", Label = "Pneu dianteiro esquerdo", RequiresPhotoOnFail = true, FailureOccurrenceType = OccurrenceType.TireProblem },
            new() { Section = "Iluminação", Label = "Faróis", FailureSeverity = OccurrenceSeverity.High },
            new() { Label = "Nível de combustível", ResponseType = ChecklistResponseType.Number, Unit = "%", IsRequired = false },
            new() { Label = "Observação do motorista", ResponseType = ChecklistResponseType.Text, IsRequired = false },
        ],
    };

    private async Task<(Guid VehicleId, ChecklistTemplateResponse Template)> ArrangeAsync()
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t, change: r => r with { CurrentOdometerKm = 50_000 });
        var template = await Services.ChecklistTemplates(_t).CreateAsync(Template(), default);
        return (vehicle.Id, template);
    }

    private static ChecklistExecutionRequest Submission(Guid vehicleId, ChecklistTemplateResponse template,
        Func<ChecklistTemplateItemResponse, ChecklistAnswerRequest?>? answer = null, int? odometer = null) => new()
    {
        VehicleId = vehicleId,
        TemplateId = template.Id,
        TemplateVersion = template.Version,
        OdometerKm = odometer,
        Answers = template.Items
            .Select(i => answer?.Invoke(i) ?? (i.ResponseType == ChecklistResponseType.PassFail
                ? new ChecklistAnswerRequest { TemplateItemId = i.Id, Choice = ChecklistChoice.Pass }
                : null))
            .OfType<ChecklistAnswerRequest>()
            .ToList(),
    };

    [Fact]
    public async Task SubmitAsync_AllPass_IsApprovedWithSnapshotAndEvent()
    {
        var (vehicleId, template) = await ArrangeAsync();

        var execution = await Service.SubmitAsync(Submission(vehicleId, template), default);

        execution.Result.Should().Be(ChecklistResult.Approved);
        execution.TemplateName.Should().Be("Inspeção diária");
        execution.Answers.Should().HaveCount(4);
        execution.Answers[0].Should().Match<ChecklistAnswerResponse>(a => a.Section == "Pneus" && a.Label == "Pneu dianteiro esquerdo" && a.Choice == ChecklistChoice.Pass);
        (await _t.NewContext().Occurrences.CountAsync()).Should().Be(0);
        (await _t.NewContext().OperationalEvents.CountAsync(e => e.Type == OperationalEventType.ChecklistCompleted)).Should().Be(1);
    }

    [Fact]
    public async Task SubmitAsync_RequiredItemUnanswered_ReportsTheItem()
    {
        var (vehicleId, template) = await ArrangeAsync();
        var headlights = template.Items[1];

        var act = () => Service.SubmitAsync(Submission(vehicleId, template, i => i.Id == headlights.Id
            ? new ChecklistAnswerRequest { TemplateItemId = i.Id } : null), default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should()
            .ContainSingle(e => e.PropertyName == $"answers.{headlights.Id}" && e.ErrorMessage.Contains("Faróis"));
    }

    [Fact]
    public async Task SubmitAsync_FailedItem_OpensOccurrenceWithItemTypeAndSeverity()
    {
        var (vehicleId, template) = await ArrangeAsync();
        var headlights = template.Items[1];

        var execution = await Service.SubmitAsync(Submission(vehicleId, template, i => i.Id == headlights.Id
            ? new ChecklistAnswerRequest { TemplateItemId = i.Id, Choice = ChecklistChoice.Fail, Comment = "Farol queimado" } : null), default);

        execution.Result.Should().Be(ChecklistResult.Failed);
        execution.FailedItems.Should().Be(1);
        var occurrence = await _t.NewContext().Occurrences.SingleAsync();
        occurrence.Should().Match<Occurrence>(o => o.VehicleId == vehicleId && o.Source == OccurrenceSource.Checklist &&
                                                   o.Severity == OccurrenceSeverity.High && o.Status == OccurrenceStatus.Open &&
                                                   o.ChecklistExecutionId == execution.Id);
        occurrence.Description.Should().Contain("Iluminação — Faróis").And.Contain("Farol queimado");
        execution.Answers.Single(a => a.Label == "Faróis").OccurrenceId.Should().Be(occurrence.Id);
        var events = await _t.NewContext().OperationalEvents.Select(e => e.Type).ToListAsync();
        events.Should().Contain([OperationalEventType.ChecklistFailed, OperationalEventType.OccurrenceCreated]);
    }

    [Fact]
    public async Task SubmitAsync_FailureThatNeedsPhoto_WithoutPhoto_ThrowsValidation_WithPhoto_ShowsItOnTheOccurrence()
    {
        var (vehicleId, template) = await ArrangeAsync();
        var tire = template.Items[0];

        var noPhoto = () => Service.SubmitAsync(Submission(vehicleId, template, i => i.Id == tire.Id
            ? new ChecklistAnswerRequest { TemplateItemId = i.Id, Choice = ChecklistChoice.Fail } : null), default);
        (await noPhoto.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Contain("foto");

        var photo = await Services.Files(_t).UploadAsync(new MemoryStream(FileServiceTests.Png), "pneu.png", default);
        await Service.SubmitAsync(Submission(vehicleId, template, i => i.Id == tire.Id
            ? new ChecklistAnswerRequest { TemplateItemId = i.Id, Choice = ChecklistChoice.Fail, FileIds = [photo.Id], Comment = "Desgaste excessivo" } : null), default);

        var occurrence = await _t.NewContext().Occurrences.SingleAsync();
        occurrence.Type.Should().Be(OccurrenceType.TireProblem);
        var detail = await Services.Occurrences(_t).GetAsync(occurrence.Id, default);
        detail.Files.Should().ContainSingle(f => f.Id == photo.Id);
    }

    [Fact]
    public async Task TemplateEdit_BumpsVersion_AndKeepsPastExecutionsIntact()
    {
        var (vehicleId, template) = await ArrangeAsync();
        var execution = await Service.SubmitAsync(Submission(vehicleId, template), default);

        var edited = await Services.ChecklistTemplates(_t).UpdateAsync(template.Id, Template() with
        {
            Name = "Inspeção diária v2",
            Items = [new() { Label = "Extintor" }],
        }, default);

        edited.Version.Should().Be(2);
        var past = await Service.GetAsync(execution.Id, default);
        past.TemplateName.Should().Be("Inspeção diária");
        past.TemplateVersion.Should().Be(1);
        past.Answers.Select(a => a.Label).Should().Equal("Pneu dianteiro esquerdo", "Faróis", "Nível de combustível", "Observação do motorista");

        var stale = () => Service.SubmitAsync(Submission(vehicleId, template), default);
        await stale.Should().ThrowAsync<ConflictException>().WithMessage("*alterado*");
    }

    [Fact]
    public async Task TemplateEdit_WithoutItemChanges_KeepsVersion()
    {
        var (_, template) = await ArrangeAsync();
        var request = Template() with
        {
            Description = "Antes de sair do pátio",
            Items = template.Items.Select(i => new ChecklistTemplateItemRequest
            {
                Id = i.Id, Section = i.Section, Label = i.Label, ResponseType = i.ResponseType, IsRequired = i.IsRequired, Unit = i.Unit,
                RequiresPhotoOnFail = i.RequiresPhotoOnFail, FailureOccurrenceType = i.FailureOccurrenceType, FailureSeverity = i.FailureSeverity,
            }).ToList(),
        };

        (await Services.ChecklistTemplates(_t).UpdateAsync(template.Id, request, default)).Version.Should().Be(1);
    }

    [Fact]
    public async Task SubmitAsync_WithOdometer_RecordsReadingThroughTheMileageRules()
    {
        var (vehicleId, template) = await ArrangeAsync();
        _t.Clock.UtcNow = _t.Clock.UtcNow.AddDays(1);

        var execution = await Service.SubmitAsync(Submission(vehicleId, template, odometer: 50_700), default);

        execution.OdometerStatus.Should().Be(OdometerReadingStatus.Valid);
        (await Services.Vehicles(_t).GetAsync(vehicleId, default)).CurrentOdometerKm.Should().Be(50_700);
        (await _t.NewContext().OdometerReadings.SingleAsync(r => r.ChecklistExecutionId == execution.Id)).Source.Should().Be(OdometerReadingSource.Checklist);

        var lower = () => Service.SubmitAsync(Submission(vehicleId, template, odometer: 40_000), default);
        (await lower.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("odometerKm");
    }

    [Fact]
    public async Task SubmitAsync_DefaultsToTheVehiclesCurrentDriver()
    {
        var (vehicleId, template) = await ArrangeAsync();
        var driver = await Scenario.DriverAsync(_t);
        await Services.Assignments(_t).AssignAsync(vehicleId, new() { DriverId = driver.Id }, default);

        var execution = await Service.SubmitAsync(Submission(vehicleId, template), default);

        execution.DriverId.Should().Be(driver.Id);
    }

    [Fact]
    public async Task PendingAsync_DailyChecklist_IsOwedByAssignedVehiclesUntilDone()
    {
        var (vehicleId, template) = await ArrangeAsync();
        await Scenario.VehicleAsync(_t, 1); // pool vehicle without driver: not expected
        var driver = await Scenario.DriverAsync(_t);
        await Services.Assignments(_t).AssignAsync(vehicleId, new() { DriverId = driver.Id }, default);

        (await Service.PendingAsync(default)).Should().ContainSingle(p => p.VehicleId == vehicleId && p.TemplateId == template.Id);

        await Service.SubmitAsync(Submission(vehicleId, template), default);
        (await Service.PendingAsync(default)).Should().BeEmpty();

        _t.Clock.UtcNow = _t.Clock.UtcNow.AddDays(1);
        (await Service.PendingAsync(default)).Should().ContainSingle("a new day needs a new daily inspection");
    }

    [Fact]
    public async Task SubmitAsync_InactiveTemplate_IsRefused()
    {
        var (vehicleId, template) = await ArrangeAsync();
        await Services.ChecklistTemplates(_t).UpdateAsync(template.Id, Template() with { IsActive = false,
            Items = template.Items.Select(i => new ChecklistTemplateItemRequest { Id = i.Id, Section = i.Section, Label = i.Label,
                ResponseType = i.ResponseType, IsRequired = i.IsRequired, Unit = i.Unit, RequiresPhotoOnFail = i.RequiresPhotoOnFail,
                FailureOccurrenceType = i.FailureOccurrenceType, FailureSeverity = i.FailureSeverity }).ToList() }, default);

        var act = () => Service.SubmitAsync(Submission(vehicleId, template), default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*desativado*");
    }
}
