using Fleet.Domain.Common;

namespace Fleet.Domain.Drivers;

public enum DriverStatus
{
    Active,
    /// <summary>Afastado: vacation, medical leave etc.</summary>
    OnLeave,
    /// <summary>Desligado.</summary>
    Inactive,
}

public enum LicenseCategory
{
    A,
    B,
    C,
    D,
    E,
    AB,
    AC,
    AD,
    AE,
}

/// <summary>
/// Driver registry only. Trips, fines, accidents, medical exams and vehicle allocation
/// are future modules referencing DriverId — do not add them here.
/// </summary>
public class Driver : AuditableEntity, ITenantScoped, ISoftDeletable, IAuditable
{
    public const int FullNameMaxLength = 150;
    public const int RgMaxLength = 20;
    public const int NotesMaxLength = 2000;
    public const int MinimumAge = 18;
    public const int MaximumAge = 100;
    public const int LicenseExpiryAlertDays = 30;

    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? Rg { get; set; }
    public DateOnly BirthDate { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Address Address { get; set; } = new();

    public string LicenseNumber { get; set; } = string.Empty;
    public LicenseCategory LicenseCategory { get; set; }
    public DateOnly LicenseExpiresOn { get; set; }
    /// <summary>EAR — "Exerce Atividade Remunerada" annotation on the CNH.</summary>
    public bool PerformsPaidActivity { get; set; }

    public DriverStatus Status { get; set; } = DriverStatus.Active;
    public string? Notes { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public bool IsLicenseExpired(DateOnly today) => LicenseExpiresOn < today;

    public bool IsLicenseExpiringSoon(DateOnly today) =>
        !IsLicenseExpired(today) && LicenseExpiresOn <= today.AddDays(LicenseExpiryAlertDays);
}
