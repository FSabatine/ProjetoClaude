using Fleet.Domain.Common;

namespace Fleet.Domain.Fuel;

/// <summary>
/// Where vehicles are fueled — an operational record for fuel management, not a supplier registry.
/// <see cref="IsInternal"/> marks the company's own tank/pump (stock control is out of scope, ADR-031).
/// </summary>
public class FuelStation : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NameMaxLength = 150;
    public const int ContactMaxLength = 100;
    public const int NotesMaxLength = 2000;

    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Digits/uppercase only (numeric or alphanumeric CNPJ). Optional: an internal pump has none.</summary>
    public string? Cnpj { get; set; }
    public Address Address { get; set; } = new();
    public string? Phone { get; set; }
    public string? ContactName { get; set; }
    public bool IsInternal { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>
/// Reference price of a product at a station from a date on (posted or negotiated), maintained by hand.
/// Fuelings always keep the price actually paid; this table never rewrites them (ADR-031).
/// </summary>
public class FuelPrice : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int NotesMaxLength = 300;

    public Guid CompanyId { get; set; }
    public Guid FuelStationId { get; set; }
    public FuelStation FuelStation { get; set; } = null!;
    public Guid FuelTypeId { get; set; }
    public FuelType FuelType { get; set; } = null!;
    public decimal Price { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
