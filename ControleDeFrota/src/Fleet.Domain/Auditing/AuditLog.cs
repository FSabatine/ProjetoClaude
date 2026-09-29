namespace Fleet.Domain.Auditing;

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
}

/// <summary>
/// Who changed what, when, from which value to which value. No foreign keys on purpose,
/// so history survives even if the referenced rows are eventually purged.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? UserId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    /// <summary>JSON: { "Field": { "old": x, "new": y } } with changed fields only.</summary>
    public string Changes { get; set; } = "{}";
    public DateTime OccurredAt { get; set; }
    public string? TraceId { get; set; }
}
