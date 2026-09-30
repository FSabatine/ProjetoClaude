using Fleet.Domain.Common;

namespace Fleet.Domain.Files;

/// <summary>Which record an attachment belongs to. Decides who may download it.</summary>
public enum FileOwnerType
{
    Document,
    Occurrence,
    ChecklistAnswer,
}

/// <summary>
/// Metadata of an uploaded file (ADR-022). The bytes live in IFileStorage under <see cref="StorageKey"/> —
/// never in the database. A file is uploaded first (no owner, visible only to the uploader) and then attached
/// to the record it documents.
/// </summary>
public class StoredFile : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int FileNameMaxLength = 200;
    public const int StorageKeyMaxLength = 200;

    public Guid CompanyId { get; set; }
    /// <summary>Original name, sanitized — display only, never used as a path.</summary>
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    /// <summary>Server-generated key ("{companyId}/{yyyy}/{MM}/{guid}"): user input never reaches the storage path.</summary>
    public string StorageKey { get; set; } = string.Empty;

    public FileOwnerType? OwnerType { get; set; }
    public Guid? OwnerId { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>What may be uploaded. The content is checked by its signature (magic bytes), not by name or header.</summary>
public static class FileRules
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    public const int MaxFilesPerOwner = 10;

    public const string Pdf = "application/pdf";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";

    private static readonly (string ContentType, byte[] Signature)[] Signatures =
    [
        (Pdf, "%PDF-"u8.ToArray()),
        (Jpeg, [0xFF, 0xD8, 0xFF]),
        (Png, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
    ];

    public const int SignatureLength = 8;

    /// <summary>Content type recognized from the first bytes, or null when the file is not an accepted format.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        foreach (var (contentType, signature) in Signatures)
            if (header.StartsWith(signature)) return contentType;
        return null;
    }

    public static bool IsImage(string contentType) => contentType is Jpeg or Png;

    /// <summary>Keeps only the base name with safe characters (no path, no control or reserved characters).</summary>
    public static string SanitizeFileName(string? name, string contentType)
    {
        var baseName = Path.GetFileName((name ?? string.Empty).Replace('\\', '/'));
        var safe = new string(baseName.Where(c => !char.IsControl(c) && c is not ('/' or ':' or '*' or '?' or '"' or '<' or '>' or '|')).ToArray()).Trim();
        if (safe.Length == 0 || safe.Trim('.').Length == 0) safe = "arquivo" + DefaultExtension(contentType);
        return safe.Length <= StoredFile.FileNameMaxLength ? safe : safe[^StoredFile.FileNameMaxLength..];
    }

    private static string DefaultExtension(string contentType) => contentType switch
    {
        Pdf => ".pdf",
        Jpeg => ".jpg",
        _ => ".png",
    };
}
