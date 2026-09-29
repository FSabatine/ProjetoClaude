namespace Fleet.Domain.Common;

/// <summary>
/// Base for every business entity. Timestamps and authorship are filled by the
/// persistence layer (FleetDbContext.SaveChangesAsync) — never set them by hand.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>Removal becomes an update of DeletedAt (ADR-011).</summary>
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

/// <summary>Belongs to a single company; queries are filtered by the current user's company (ADR-003).</summary>
public interface ITenantScoped
{
    Guid CompanyId { get; set; }
}

/// <summary>Changes are written to AuditLogs with old/new values (ADR-012).</summary>
public interface IAuditable;
