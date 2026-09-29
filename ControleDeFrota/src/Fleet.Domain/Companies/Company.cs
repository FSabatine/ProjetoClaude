using Fleet.Domain.Common;

namespace Fleet.Domain.Companies;

/// <summary>Tenant. Every operational record belongs to exactly one company.</summary>
public class Company : AuditableEntity, ISoftDeletable, IAuditable
{
    public const int LegalNameMaxLength = 200;
    public const int TradeNameMaxLength = 200;
    public const int StateRegistrationMaxLength = 20;

    public string LegalName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    /// <summary>14 characters, normalized (numeric or alphanumeric format).</summary>
    public string Cnpj { get; set; } = string.Empty;
    /// <summary>Inscrição estadual or "ISENTO".</summary>
    public string? StateRegistration { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Address Address { get; set; } = new();
    /// <summary>An inactive company blocks login for all of its users.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
