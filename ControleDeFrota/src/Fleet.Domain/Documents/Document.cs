using Fleet.Domain.Common;
using Fleet.Domain.Drivers;
using Fleet.Domain.Implements;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Documents;

/// <summary>What a document belongs to. Company-owned documents have no owner FK (the owner is CompanyId).</summary>
public enum DocumentOwnerType
{
    Vehicle,
    Driver,
    Implement,
    Company,
}

/// <summary>
/// Configurable catalog per company (ADR-021): new kinds of document are data, not code.
/// A type applies to a single owner type and defines whether it expires and how early to alert.
/// </summary>
public class DocumentType : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 80;
    public const int DefaultAlertDaysBefore = 30;
    public const int MaxAlertDaysBefore = 365;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DocumentOwnerType OwnerType { get; set; }
    public bool HasExpiration { get; set; } = true;
    public int AlertDaysBefore { get; set; } = DefaultAlertDaysBefore;
    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>
/// A fleet document (CRLV, insurance, medical exam…). The status is never stored: it is computed from the dates by
/// <see cref="DocumentExpiryPolicy"/>, so it is always right on the day it is read.
/// </summary>
public class Document : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NumberMaxLength = 60;
    public const int NotesMaxLength = 1000;

    public Guid CompanyId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    public DocumentOwnerType OwnerType { get; set; }
    public Guid? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }
    public Guid? ImplementId { get; set; }
    public Implement? Implement { get; set; }

    public string? Number { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    /// <summary>ExpiresOn − type.AlertDaysBefore (DocumentExpiryPolicy.AlertStartsOn); kept in sync by the services.</summary>
    public DateOnly? AlertStartsOn { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set when a renewal replaces this document: it leaves the alerts but stays in the history.</summary>
    public DateTime? ReplacedAt { get; set; }
    public Guid? ReplacedByDocumentId { get; set; }

    /// <summary>
    /// Last expiry status an alert event was emitted for (DocumentExpirationScanner), so each state change is
    /// announced once. Bookkeeping only — not part of the audit trail.
    /// </summary>
    public DocumentStatus? LastAlertedStatus { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public Guid? OwnerId => OwnerType switch
    {
        DocumentOwnerType.Vehicle => VehicleId,
        DocumentOwnerType.Driver => DriverId,
        DocumentOwnerType.Implement => ImplementId,
        _ => CompanyId,
    };
}
