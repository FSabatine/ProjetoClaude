using Fleet.Domain.Common;

namespace Fleet.Domain.Maintenance;

public enum WorkshopStatus
{
    Active,
    Inactive,
}

/// <summary>An internal or external workshop that performs maintenance work orders.</summary>
public class Workshop : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 150;
    public const int SpecialtiesMaxLength = 300;
    public const int NotesMaxLength = 2000;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>CPF or CNPJ, digits only.</summary>
    public string? Document { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Address Address { get; set; } = new();
    /// <summary>Free text (engine, electrical, brakes…) — not worth a tags table at this scale.</summary>
    public string? Specialties { get; set; }
    public WorkshopStatus Status { get; set; } = WorkshopStatus.Active;
    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
