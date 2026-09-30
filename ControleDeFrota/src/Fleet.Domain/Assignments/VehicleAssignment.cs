using Fleet.Domain.Common;
using Fleet.Domain.Drivers;
using Fleet.Domain.Vehicles;

namespace Fleet.Domain.Assignments;

/// <summary>
/// The driver responsible for a vehicle during a period (ADR-020). History is never overwritten:
/// changing the driver ends the current assignment and starts a new one.
/// At most one active assignment per vehicle and per driver (filtered unique indexes).
/// Not soft-deletable: a wrong assignment is ended, and the record stays in the history.
/// </summary>
public class VehicleAssignment : AuditableEntity, ITenantScoped, IAuditable
{
    public const int NotesMaxLength = 500;

    public Guid CompanyId { get; set; }
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;

    public DateTime StartedAt { get; set; }
    /// <summary>Null = active.</summary>
    public DateTime? EndedAt { get; set; }
    public string? Notes { get; set; }
    public string? EndReason { get; set; }

    public bool IsActive => EndedAt is null;
}

/// <summary>Assignment rules that depend only on the entities (the service adds the database checks).</summary>
public static class AssignmentRules
{
    /// <summary>
    /// Why this driver cannot receive a NEW assignment, or null. Existing assignments of a driver
    /// who goes on leave are kept (the vehicle stays reserved for them).
    /// </summary>
    public static string? DriverBlockReason(Driver driver, DateOnly today) => driver switch
    {
        { Status: DriverStatus.Inactive } => $"{driver.FullName} está desligado(a) e não pode receber veículo.",
        { Status: DriverStatus.OnLeave } => $"{driver.FullName} está afastado(a). Altere a situação do motorista para Ativo antes de alocar um veículo.",
        _ when driver.IsLicenseExpired(today) =>
            $"A CNH de {driver.FullName} está vencida. Atualize a validade da CNH no cadastro do motorista antes de alocar um veículo.",
        _ => null,
    };

    public static string? VehicleBlockReason(Vehicle vehicle) => vehicle.Status == VehicleStatus.Inactive
        ? "Este veículo está inativo e não pode receber motorista. Reative o veículo antes de alocá-lo."
        : null;
}
