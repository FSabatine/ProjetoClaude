using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Documents;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Documents;
using Fleet.Domain.Operations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Documents;

public class DocumentServiceTests : IDisposable
{
    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private DateOnly Today => _t.Clock.Today;

    private DocumentService Service => Services.Documents(_t);

    private async Task<(Guid VehicleId, DocumentTypeResponse Insurance, DocumentTypeResponse Other)> ArrangeAsync(string role = SystemRoles.FleetManager)
    {
        await Scenario.SignedInAsync(_t, role);
        var vehicle = await Scenario.VehicleAsync(_t);
        var types = await Services.DocumentTypes(_t).ListAsync(DocumentOwnerType.Vehicle, includeInactive: false, default);
        return (vehicle.Id, types.Single(t => t.Name == "Seguro"), types.Single(t => !t.HasExpiration));
    }

    private Task<DocumentResponse> CreateAsync(Guid typeId, Guid ownerId, DateOnly? expiresOn) =>
        Service.CreateAsync(new DocumentCreateRequest { DocumentTypeId = typeId, OwnerId = ownerId, Number = "APL-123", ExpiresOn = expiresOn }, default);

    [Fact]
    public async Task DocumentTypes_AreSeededOnceForTheCompany()
    {
        await Scenario.SignedInAsync(_t);

        var first = await Services.DocumentTypes(_t).ListAsync(null, includeInactive: true, default);
        var second = await Services.DocumentTypes(_t).ListAsync(null, includeInactive: true, default);

        first.Should().HaveCount(DocumentTypeDefaults.All.Count);
        second.Should().HaveCount(first.Count);
        first.Should().NotContain(t => t.Name.Contains("CNH"), "the CNH validity lives in the driver record");
    }

    [Theory]
    [InlineData(60, DocumentStatus.Valid)]
    [InlineData(30, DocumentStatus.ExpiringSoon)]
    [InlineData(0, DocumentStatus.ExpiringSoon)]
    [InlineData(-1, DocumentStatus.Expired)]
    public async Task CreateAsync_ComputesStatusFromDates(int days, DocumentStatus expected)
    {
        var (vehicleId, insurance, _) = await ArrangeAsync();

        var document = await CreateAsync(insurance.Id, vehicleId, Today.AddDays(days));

        document.Status.Should().Be(expected);
        document.DaysUntilExpiration.Should().Be(days);
        document.OwnerName.Should().Be("ABC1D23");
    }

    [Fact]
    public async Task CreateAsync_TypeWithoutExpiration_IsNoExpiration_AndRejectsADate()
    {
        var (vehicleId, _, other) = await ArrangeAsync();

        (await CreateAsync(other.Id, vehicleId, null)).Status.Should().Be(DocumentStatus.NoExpiration);
        var act = () => CreateAsync(other.Id, vehicleId, Today.AddDays(10));
        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("expiresOn");
    }

    [Fact]
    public async Task CreateAsync_ExpiringTypeWithoutDate_ThrowsValidation()
    {
        var (vehicleId, insurance, _) = await ArrangeAsync();

        var act = () => CreateAsync(insurance.Id, vehicleId, null);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Contain("Informe o vencimento");
    }

    [Fact]
    public async Task CreateAsync_ExpirationBeforeIssue_ThrowsValidation()
    {
        var (vehicleId, insurance, _) = await ArrangeAsync();

        var act = () => Service.CreateAsync(new DocumentCreateRequest
        {
            DocumentTypeId = insurance.Id, OwnerId = vehicleId, IssuedOn = Today, ExpiresOn = Today.AddDays(-1),
        }, default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "ExpiresOn");
    }

    [Fact]
    public async Task CreateAsync_OwnerOfAnotherCompany_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);
        var foreignVehicle = await Scenario.VehicleAsync(_t);
        var (_, insurance, _) = await ArrangeAsyncInOtherCompanyAsync();

        var act = () => CreateAsync(insurance.Id, foreignVehicle.Id, Today.AddDays(90));

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("ownerId");
    }

    private async Task<(Guid, DocumentTypeResponse, DocumentTypeResponse)> ArrangeAsyncInOtherCompanyAsync()
    {
        await Scenario.SignedInAsync(_t, cnpj: "12ABC34501DE35");
        var types = await Services.DocumentTypes(_t).ListAsync(DocumentOwnerType.Vehicle, false, default);
        return (Guid.Empty, types.Single(t => t.Name == "Seguro"), types.Single(t => !t.HasExpiration));
    }

    [Fact]
    public async Task ListAsync_StatusFiltersAgreeWithThePolicy()
    {
        var (vehicleId, insurance, other) = await ArrangeAsync();
        var created = new List<DocumentResponse>
        {
            await CreateAsync(insurance.Id, vehicleId, Today.AddDays(90)),
            await CreateAsync(insurance.Id, vehicleId, Today.AddDays(10)),
            await CreateAsync(insurance.Id, vehicleId, Today),
            await CreateAsync(insurance.Id, vehicleId, Today.AddDays(-5)),
            await CreateAsync(other.Id, vehicleId, null),
        };

        foreach (var status in new[] { DocumentStatus.Valid, DocumentStatus.ExpiringSoon, DocumentStatus.Expired, DocumentStatus.NoExpiration })
        {
            var listed = await Service.ListAsync(new DocumentListRequest { Status = status }, default);
            listed.Items.Select(d => d.Id).Should().BeEquivalentTo(created.Where(d => d.Status == status).Select(d => d.Id), $"filter {status}");
            listed.Items.Should().OnlyContain(d => d.Status == status);
        }
        (await Service.ListAsync(new DocumentListRequest { AlertsOnly = true }, default)).TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task CreateAsync_Renewal_ReplacesTheOldDocumentAndLeavesTheAlerts()
    {
        var (vehicleId, insurance, _) = await ArrangeAsync();
        var old = await CreateAsync(insurance.Id, vehicleId, Today.AddDays(-2));

        var renewal = await Service.CreateAsync(new DocumentCreateRequest
        {
            DocumentTypeId = insurance.Id, OwnerId = vehicleId, ExpiresOn = Today.AddYears(1), ReplacesDocumentId = old.Id,
        }, default);

        (await Service.GetAsync(old.Id, default)).Status.Should().Be(DocumentStatus.Replaced);
        renewal.Status.Should().Be(DocumentStatus.Valid);
        (await Service.ListAsync(new DocumentListRequest { AlertsOnly = true }, default)).TotalCount.Should().Be(0);
        (await Service.ListAsync(new DocumentListRequest(), default)).Items.Should().ContainSingle(d => d.Id == renewal.Id);
        (await _t.NewContext().OperationalEvents.CountAsync(e => e.Type == OperationalEventType.DocumentRenewed)).Should().Be(1);

        var twice = () => Service.CreateAsync(new DocumentCreateRequest
        {
            DocumentTypeId = insurance.Id, OwnerId = vehicleId, ExpiresOn = Today.AddYears(1), ReplacesDocumentId = old.Id,
        }, default);
        await twice.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAuditsAndRecordsEvent()
    {
        var (vehicleId, insurance, _) = await ArrangeAsync();
        var document = await CreateAsync(insurance.Id, vehicleId, Today.AddDays(90));

        await Service.DeleteAsync(document.Id, default);

        var db = _t.NewContext();
        (await db.Documents.IgnoreQueryFilters().SingleAsync(d => d.Id == document.Id)).DeletedAt.Should().NotBeNull();
        (await db.AuditLogs.AnyAsync(a => a.EntityName == "Document" && a.Action == Fleet.Domain.Auditing.AuditAction.Deleted)).Should().BeTrue();
        (await db.OperationalEvents.AnyAsync(e => e.Type == OperationalEventType.DocumentDeleted && e.VehicleId == vehicleId)).Should().BeTrue();
    }

    [Fact]
    public async Task DocumentType_NewThreshold_ReachesExistingDocuments()
    {
        var (vehicleId, insurance, _) = await ArrangeAsync();
        var document = await CreateAsync(insurance.Id, vehicleId, Today.AddDays(40));
        document.Status.Should().Be(DocumentStatus.Valid);

        await Services.DocumentTypes(_t).UpdateAsync(insurance.Id, new DocumentTypeRequest
        {
            Name = insurance.Name, OwnerType = DocumentOwnerType.Vehicle, HasExpiration = true, AlertDaysBefore = 60,
        }, default);

        (await Service.GetAsync(document.Id, default)).Status.Should().Be(DocumentStatus.ExpiringSoon);
    }

    [Fact]
    public async Task DocumentType_WithDocuments_CannotBeDeleted()
    {
        var (vehicleId, insurance, _) = await ArrangeAsync();
        await CreateAsync(insurance.Id, vehicleId, Today.AddDays(40));

        var act = () => Services.DocumentTypes(_t).DeleteAsync(insurance.Id, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*Inative-o*");
    }

    [Fact]
    public async Task Scanner_EmitsEachStateChangeOnce_ForEveryCompany()
    {
        var (vehicleId, insurance, _) = await ArrangeAsync();
        await CreateAsync(insurance.Id, vehicleId, Today.AddDays(10));
        await CreateAsync(insurance.Id, vehicleId, Today.AddDays(40)); // outside the 30-day window today
        _t.CurrentUser.Anonymous(); // background job: no user, no tenant

        (await Services.DocumentScanner(_t).ScanAsync(default)).Should().Be(1);
        (await Services.DocumentScanner(_t).ScanAsync(default)).Should().Be(0, "the same state is announced only once");

        _t.Clock.UtcNow = _t.Clock.UtcNow.AddDays(11);
        (await Services.DocumentScanner(_t).ScanAsync(default)).Should().Be(2, "one expired, the other entered its window");

        var events = await _t.NewContext().OperationalEvents.IgnoreQueryFilters()
            .Where(e => e.Type == OperationalEventType.DocumentExpiring || e.Type == OperationalEventType.DocumentExpired)
            .ToListAsync();
        events.Select(e => e.Type).Should().BeEquivalentTo(
            [OperationalEventType.DocumentExpiring, OperationalEventType.DocumentExpired, OperationalEventType.DocumentExpiring]);
        events.Should().Contain(e => e.Summary.StartsWith("Seguro do veículo ABC1D23 vence em 29 dias"));
        events.Should().OnlyContain(e => e.CompanyId != Guid.Empty && e.UserId == null && e.VehicleId == vehicleId);
    }

    [Fact]
    public async Task ListAsync_DocumentsOfAnotherCompany_AreInvisible()
    {
        var (vehicleId, insurance, _) = await ArrangeAsync();
        await CreateAsync(insurance.Id, vehicleId, Today.AddDays(90));

        await Scenario.SignedInAsync(_t, cnpj: "12ABC34501DE35");

        (await Service.ListAsync(new DocumentListRequest(), default)).TotalCount.Should().Be(0);
    }
}
