using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Occurrences;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Auditing;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Operations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Occurrences;

public class OccurrenceServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private OccurrenceService Service => Services.Occurrences(_t);

    private async Task<OccurrenceResponse> CreateAsync(OccurrenceSeverity severity = OccurrenceSeverity.Medium)
    {
        await Scenario.SignedInAsync(_t);
        var vehicle = await Scenario.VehicleAsync(_t);
        return await Service.CreateAsync(new OccurrenceRequest
        {
            VehicleId = vehicle.Id, Type = OccurrenceType.TireProblem, Severity = severity, Description = "Pressão baixa no pneu traseiro",
            Location = "Pátio Curitiba",
        }, default);
    }

    private Task<OccurrenceResponse> MoveAsync(Guid id, OccurrenceStatus to, string? resolution = null) =>
        Service.ChangeStatusAsync(id, new OccurrenceStatusRequest { Status = to, Resolution = resolution }, default);

    [Fact]
    public async Task CreateAsync_Valid_IsOpenWithEventAndAudit()
    {
        var occurrence = await CreateAsync();

        occurrence.Status.Should().Be(OccurrenceStatus.Open);
        occurrence.NextStatuses.Should().Equal(OccurrenceStatus.InAnalysis, OccurrenceStatus.Resolved, OccurrenceStatus.Cancelled);
        occurrence.LicensePlate.Should().Be("ABC1D23");
        var db = _t.NewContext();
        (await db.OperationalEvents.SingleAsync(e => e.Type == OperationalEventType.OccurrenceCreated)).VehicleId.Should().Be(occurrence.VehicleId);
        (await db.AuditLogs.AnyAsync(a => a.EntityName == "Occurrence" && a.Action == AuditAction.Created)).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithoutVehicleDriverOrImplement_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);

        var act = () => Service.CreateAsync(new OccurrenceRequest
        {
            Type = OccurrenceType.GeneralObservation, Severity = OccurrenceSeverity.Low, Description = "x",
        }, default);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*veículo, o motorista ou o implemento*");
    }

    [Fact]
    public async Task ChangeStatusAsync_OpenToAnalysisToResolved_RecordsClosure()
    {
        var occurrence = await CreateAsync();

        await MoveAsync(occurrence.Id, OccurrenceStatus.InAnalysis);
        var resolved = await MoveAsync(occurrence.Id, OccurrenceStatus.Resolved, "Pneu calibrado");

        resolved.Status.Should().Be(OccurrenceStatus.Resolved);
        resolved.Resolution.Should().Be("Pneu calibrado");
        resolved.ClosedAt.Should().Be(_t.Clock.UtcNow);
        resolved.NextStatuses.Should().BeEmpty();
        (await _t.NewContext().OperationalEvents.CountAsync(e => e.Type == OperationalEventType.OccurrenceStatusChanged)).Should().Be(2);
    }

    [Theory]
    [InlineData(OccurrenceStatus.Resolved)]
    [InlineData(OccurrenceStatus.Cancelled)]
    public async Task ChangeStatusAsync_ClosingWithoutText_ThrowsValidation(OccurrenceStatus to)
    {
        var occurrence = await CreateAsync();

        var act = () => MoveAsync(occurrence.Id, to);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ChangeStatusAsync_FromClosed_IsRefused()
    {
        var occurrence = await CreateAsync();
        await MoveAsync(occurrence.Id, OccurrenceStatus.Cancelled, "Registro duplicado");

        var reopen = () => MoveAsync(occurrence.Id, OccurrenceStatus.Open);

        await reopen.Should().ThrowAsync<BusinessRuleException>().WithMessage("*encerrada*");
    }

    [Fact]
    public async Task ChangeStatusAsync_BackToOpen_IsNotAllowed()
    {
        var occurrence = await CreateAsync();
        await MoveAsync(occurrence.Id, OccurrenceStatus.InAnalysis);

        var act = () => MoveAsync(occurrence.Id, OccurrenceStatus.Open);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*não é permitida*");
    }

    [Fact]
    public async Task UpdateAsync_ClosedOccurrence_IsRefused()
    {
        var occurrence = await CreateAsync();
        await MoveAsync(occurrence.Id, OccurrenceStatus.Resolved, "ok");

        var act = () => Service.UpdateAsync(occurrence.Id, new OccurrenceRequest
        {
            VehicleId = occurrence.VehicleId, Type = OccurrenceType.TireProblem, Severity = OccurrenceSeverity.Low, Description = "editado",
        }, default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task ListAsync_FiltersOpenOnlySeverityAndPeriod()
    {
        var critical = await CreateAsync(OccurrenceSeverity.Critical);
        var low = await Service.CreateAsync(new OccurrenceRequest
        {
            VehicleId = critical.VehicleId, Type = OccurrenceType.GeneralObservation, Severity = OccurrenceSeverity.Low, Description = "Risco na porta",
        }, default);
        await MoveAsync(low.Id, OccurrenceStatus.Resolved, "Polido");

        (await Service.ListAsync(new() { OpenOnly = true }, default)).Items.Should().ContainSingle(o => o.Id == critical.Id);
        (await Service.ListAsync(new() { Severity = OccurrenceSeverity.Low }, default)).Items.Should().ContainSingle(o => o.Id == low.Id);
        (await Service.ListAsync(new() { From = _t.Clock.Today.AddDays(1) }, default)).TotalCount.Should().Be(0);
        (await Service.ListAsync(new() { Search = "ABC1D23" }, default)).TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_OccurrenceOfAnotherCompany_ThrowsNotFound()
    {
        var occurrence = await CreateAsync();
        await Scenario.SignedInAsync(_t, cnpj: "12ABC34501DE35");

        var act = () => Service.GetAsync(occurrence.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
