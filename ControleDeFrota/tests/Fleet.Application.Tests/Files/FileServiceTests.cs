using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Files;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Files;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Files;

public class FileServiceTests : IDisposable
{
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private readonly TestDb _t = new();

    public void Dispose() => _t.Dispose();

    private FileService Service => Services.Files(_t);

    [Fact]
    public async Task UploadAsync_ValidImage_StoresBytesOutsideTheDatabase()
    {
        await Scenario.SignedInAsync(_t);

        var file = await Service.UploadAsync(new MemoryStream(Png), "../pneu.png", default);

        file.ContentType.Should().Be(FileRules.Png);
        file.FileName.Should().Be("pneu.png");
        file.SizeBytes.Should().Be(Png.Length);
        var stored = await _t.NewContext().StoredFiles.SingleAsync();
        stored.StorageKey.Should().StartWith(_t.CurrentUser.CompanyId!.Value.ToString("N")).And.NotContain("pneu");
        _t.Storage.Files[stored.StorageKey].Should().Equal(Png);
    }

    [Fact]
    public async Task UploadAsync_UnsupportedFormat_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);

        var act = () => Service.UploadAsync(new MemoryStream("MZ fake exe"u8.ToArray()), "nota.pdf", default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Contain("PDF, JPG ou PNG");
    }

    [Fact]
    public async Task UploadAsync_TooLarge_ThrowsValidation()
    {
        await Scenario.SignedInAsync(_t);
        var big = new byte[FileRules.MaxSizeBytes + 1];
        "%PDF-"u8.CopyTo(big);

        var act = () => Service.UploadAsync(new MemoryStream(big), "grande.pdf", default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().ErrorMessage.Should().Contain("MB");
    }

    [Fact]
    public async Task UnattachedFile_IsVisibleOnlyToTheUploader()
    {
        await Scenario.SignedInAsync(_t);
        var file = await Service.UploadAsync(new MemoryStream(Png), "a.png", default);

        (await Service.OpenAsync(file.Id, default)).ContentType.Should().Be(FileRules.Png);

        _t.CurrentUser.UserId = Guid.NewGuid();
        var otherUser = () => Service.OpenAsync(file.Id, default);
        await otherUser.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task AttachAsync_FileOfAnotherUser_IsRefused()
    {
        await Scenario.SignedInAsync(_t);
        var file = await Service.UploadAsync(new MemoryStream(Png), "a.png", default);
        _t.CurrentUser.UserId = Guid.NewGuid();

        var act = () => Service.AttachAsync([file.Id], FileOwnerType.Occurrence, Guid.NewGuid(), "fileIds", default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("fileIds");
    }

    [Fact]
    public async Task DocumentFile_RequiresDocumentsView()
    {
        var company = await Scenario.SignedInAsync(_t);
        var file = await Service.UploadAsync(new MemoryStream(Png), "crlv.png", default);
        await Service.AttachAsync([file.Id], FileOwnerType.Document, Guid.NewGuid(), "fileIds", default);
        await _t.Db.SaveChangesAsync();

        _t.SignInAs(company, SystemRoles.Maintenance); // no documents.view
        var act = () => Service.OpenAsync(file.Id, default);
        await act.Should().ThrowAsync<ForbiddenException>();

        _t.SignInAs(company, SystemRoles.Viewer);
        (await Service.OpenAsync(file.Id, default)).FileName.Should().Be("crlv.png");
    }

    [Fact]
    public async Task OpenAsync_FileOfAnotherCompany_ThrowsNotFound()
    {
        await Scenario.SignedInAsync(_t);
        var file = await Service.UploadAsync(new MemoryStream(Png), "a.png", default);
        await Scenario.SignedInAsync(_t, cnpj: "12ABC34501DE35");

        var act = () => Service.OpenAsync(file.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
