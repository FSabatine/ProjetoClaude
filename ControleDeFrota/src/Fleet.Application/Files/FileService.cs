using Fleet.Application.Common;
using Fleet.Domain.Authorization;
using Fleet.Domain.Common;
using Fleet.Domain.Files;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Files;

public sealed record FileResponse(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt);

public sealed record FileDownload(string FileName, string ContentType, Stream Content);

/// <summary>
/// Upload → attach → download (ADR-022). Uploaded files have no owner until a document, occurrence or checklist answer
/// claims them; until then only the uploader sees them. Download permission follows the owner record.
/// </summary>
public sealed class FileService(IFleetDbContext db, IFileStorage storage, ICurrentUser currentUser, IClock clock)
{
    public async Task<FileResponse> UploadAsync(Stream content, string? fileName, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        // Bounded copy: never buffer more than the limit, whatever the client declared.
        await CopyAtMostAsync(content, buffer, FileRules.MaxSizeBytes + 1, ct);
        if (buffer.Length == 0) throw ValidationErrors.ForField("file", "Selecione um arquivo para enviar.");
        if (buffer.Length > FileRules.MaxSizeBytes)
            throw ValidationErrors.ForField("file", $"O arquivo é maior que {FileRules.MaxSizeBytes / 1024 / 1024} MB. Reduza o tamanho ou envie outro arquivo.");

        var contentType = FileRules.DetectContentType(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, FileRules.SignatureLength)))
            ?? throw ValidationErrors.ForField("file", "Formato de arquivo não aceito. Envie PDF, JPG ou PNG.");

        var now = clock.UtcNow;
        var file = new StoredFile
        {
            FileName = FileRules.SanitizeFileName(fileName, contentType),
            ContentType = contentType,
            SizeBytes = buffer.Length,
            StorageKey = $"{currentUser.CompanyId:N}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}",
        };
        buffer.Position = 0;
        await storage.SaveAsync(file.StorageKey, buffer, ct);
        db.StoredFiles.Add(file);
        await db.SaveChangesAsync(ct);
        return ToResponse(file);
    }

    /// <summary>
    /// Claims uploaded files for a record, in the caller's unit of work (not saved here). Only files the caller
    /// uploaded and that are still unattached can be claimed — an id alone never grants access to someone else's file.
    /// </summary>
    public async Task AttachAsync(IReadOnlyCollection<Guid>? fileIds, FileOwnerType ownerType, Guid ownerId, string field, CancellationToken ct)
    {
        if (fileIds is null || fileIds.Count == 0) return;
        var ids = fileIds.Distinct().ToList();
        var existing = await db.StoredFiles.CountAsync(f => f.OwnerType == ownerType && f.OwnerId == ownerId, ct);
        if (existing + ids.Count > FileRules.MaxFilesPerOwner)
            throw ValidationErrors.ForField(field, $"Anexe no máximo {FileRules.MaxFilesPerOwner} arquivos por registro.");

        var files = await db.StoredFiles
            .Where(f => ids.Contains(f.Id) && f.OwnerType == null && f.CreatedBy == currentUser.UserId)
            .ToListAsync(ct);
        if (files.Count != ids.Count)
            throw ValidationErrors.ForField(field, "Um dos arquivos não está mais disponível. Envie o arquivo novamente.");

        foreach (var file in files)
        {
            file.OwnerType = ownerType;
            file.OwnerId = ownerId;
        }
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<FileResponse>>> ListByOwnersAsync(
        FileOwnerType ownerType, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct)
    {
        if (ownerIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<FileResponse>>();
        var files = await db.StoredFiles
            .Where(f => f.OwnerType == ownerType && f.OwnerId != null && ownerIds.Contains(f.OwnerId.Value))
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(ct);
        return files.GroupBy(f => f.OwnerId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<FileResponse>)g.Select(ToResponse).ToList());
    }

    public async Task<IReadOnlyList<FileResponse>> ListByOwnerAsync(FileOwnerType ownerType, Guid ownerId, CancellationToken ct) =>
        (await ListByOwnersAsync(ownerType, [ownerId], ct)).GetValueOrDefault(ownerId) ?? [];

    public async Task<FileDownload> OpenAsync(Guid id, CancellationToken ct)
    {
        var file = await LoadAsync(id, ct);
        if (!await CanViewAsync(file, ct))
            throw new ForbiddenException("Você não tem permissão para abrir este arquivo.");
        return new FileDownload(file.FileName, file.ContentType, await storage.OpenReadAsync(file.StorageKey, ct));
    }

    /// <summary>Removes an attachment (soft delete; the bytes stay until a purge job exists).</summary>
    public async Task RemoveAsync(Guid id, CancellationToken ct)
    {
        var file = await LoadAsync(id, ct);
        var allowed = file.OwnerType switch
        {
            null => file.CreatedBy == currentUser.UserId,
            FileOwnerType.Document => currentUser.HasPermission(Permissions.Documents.Manage),
            FileOwnerType.Occurrence => currentUser.HasPermission(Permissions.Occurrences.Manage),
            FileOwnerType.Fueling => currentUser.HasPermission(Permissions.Fuel.Correct),
            FileOwnerType.Tire or FileOwnerType.TireInspection => currentUser.HasPermission(Permissions.Tires.Edit),
            FileOwnerType.TireServiceOrder => currentUser.HasPermission(Permissions.Tires.Repair) || currentUser.HasPermission(Permissions.Tires.Retread),
            // Evidence of a submitted inspection is immutable.
            _ => false,
        };
        if (!allowed)
            throw new ForbiddenException(file.OwnerType == FileOwnerType.ChecklistAnswer
                ? "Fotos de um checklist enviado não podem ser removidas: elas são o registro da inspeção."
                : "Você não tem permissão para remover este arquivo.");
        db.StoredFiles.Remove(file);
        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> CanViewAsync(StoredFile file, CancellationToken ct) => file.OwnerType switch
    {
        FileOwnerType.Fueling => await CanViewFuelingFileAsync(file.OwnerId, ct),
        // Invoices, warranties and service documents of a tire show what was paid: same rule as the tire's money fields.
        FileOwnerType.Tire or FileOwnerType.TireServiceOrder => currentUser.HasPermission(Permissions.Tires.View) &&
            (currentUser.HasPermission(Permissions.Tires.ViewCosts) || file.CreatedBy == currentUser.UserId),
        _ => CanView(file),
    };

    /// <summary>A receipt shows what was paid: same rule as the fueling's money fields (fuel.viewcosts or its author).</summary>
    private async Task<bool> CanViewFuelingFileAsync(Guid? fuelingId, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.Fuel.View)) return false;
        if (currentUser.HasPermission(Permissions.Fuel.ViewCosts)) return true;
        return await db.Fuelings.AnyAsync(f => f.Id == fuelingId && f.CreatedBy == currentUser.UserId, ct);
    }

    private bool CanView(StoredFile file) => file.OwnerType switch
    {
        null => file.CreatedBy == currentUser.UserId,
        FileOwnerType.Document => currentUser.HasPermission(Permissions.Documents.View),
        FileOwnerType.Occurrence => currentUser.HasPermission(Permissions.Occurrences.View),
        // Checklist photos are also the evidence shown on the occurrence the failed item opened.
        FileOwnerType.ChecklistAnswer => currentUser.HasPermission(Permissions.Checklists.View) ||
                                         currentUser.HasPermission(Permissions.Occurrences.View),
        // Inspection photos are the evidence of the tire's condition, visible to whoever sees the tire.
        FileOwnerType.TireInspection => currentUser.HasPermission(Permissions.Tires.View),
        _ => false,
    };

    private async Task<StoredFile> LoadAsync(Guid id, CancellationToken ct) =>
        await db.StoredFiles.SingleOrDefaultAsync(f => f.Id == id, ct)
        ?? throw new NotFoundException("Arquivo não encontrado. Ele pode ter sido removido.");

    private static async Task CopyAtMostAsync(Stream source, Stream destination, long maxBytes, CancellationToken ct)
    {
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while (total < maxBytes && (read = await source.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, maxBytes - total)), ct)) > 0)
        {
            await destination.WriteAsync(chunk.AsMemory(0, read), ct);
            total += read;
        }
    }

    public static FileResponse ToResponse(StoredFile f) => new(f.Id, f.FileName, f.ContentType, f.SizeBytes, f.CreatedAt);
}
