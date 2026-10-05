using Fleet.Domain.Assignments;
using Fleet.Domain.Auditing;
using Fleet.Domain.Checklists;
using Fleet.Domain.Common;
using Fleet.Domain.Companies;
using Fleet.Domain.Documents;
using Fleet.Domain.Drivers;
using Fleet.Domain.Files;
using Fleet.Domain.Finance;
using Fleet.Domain.Fuel;
using Fleet.Domain.Implements;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using Fleet.Domain.Tracking;
using Fleet.Domain.Users;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Common;

/// <summary>
/// Persistence boundary used directly by services (ADR-004). Tenant and soft-delete filters are
/// applied by the implementation, so queries here never need to filter by CompanyId or DeletedAt.
/// </summary>
public interface IFleetDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Driver> Drivers { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<Implement> Implements { get; }
    DbSet<VehicleAssignment> VehicleAssignments { get; }
    DbSet<OdometerReading> OdometerReadings { get; }
    DbSet<DocumentType> DocumentTypes { get; }
    DbSet<Document> Documents { get; }
    DbSet<StoredFile> StoredFiles { get; }
    DbSet<ChecklistTemplate> ChecklistTemplates { get; }
    DbSet<ChecklistExecution> ChecklistExecutions { get; }
    DbSet<Occurrence> Occurrences { get; }
    DbSet<OperationalEvent> OperationalEvents { get; }
    DbSet<AuditLog> AuditLogs { get; }

    // Phase 3 — maintenance
    DbSet<Workshop> Workshops { get; }
    DbSet<MaintenancePlan> MaintenancePlans { get; }
    DbSet<MaintenanceSchedule> MaintenanceSchedules { get; }
    DbSet<HourMeterReading> HourMeterReadings { get; }
    DbSet<MaintenanceRequest> MaintenanceRequests { get; }
    DbSet<WorkOrder> WorkOrders { get; }

    // Phase 4 — fuel
    DbSet<FuelType> FuelTypes { get; }
    DbSet<FuelStation> FuelStations { get; }
    DbSet<FuelPrice> FuelPrices { get; }
    DbSet<FuelSettings> FuelSettings { get; }
    DbSet<Fueling> Fuelings { get; }
    DbSet<FuelingAnomaly> FuelingAnomalies { get; }

    // Phase 5 — tires
    DbSet<TireModel> TireModels { get; }
    DbSet<TireLayout> TireLayouts { get; }
    DbSet<Tire> Tires { get; }
    DbSet<TireInstallation> TireInstallations { get; }
    DbSet<TireRotation> TireRotations { get; }
    DbSet<TireInspection> TireInspections { get; }
    DbSet<TireInspectionDamage> TireInspectionDamages { get; }
    DbSet<TireServiceOrder> TireServiceOrders { get; }
    DbSet<TireCost> TireCosts { get; }
    DbSet<TireAnomaly> TireAnomalies { get; }
    DbSet<TireSettings> TireSettings { get; }

    // Phase 6 — finance
    DbSet<CostCenter> CostCenters { get; }
    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<RecurringExpense> RecurringExpenses { get; }
    DbSet<Budget> Budgets { get; }

    // Final phase — intelligence and automation
    DbSet<AutomationRule> AutomationRules { get; }
    DbSet<AutomationExecution> AutomationExecutions { get; }
    DbSet<FleetAlert> FleetAlerts { get; }
    DbSet<UserNotification> UserNotifications { get; }
    DbSet<TrackingProvider> TrackingProviders { get; }
    DbSet<TrackingDevice> TrackingDevices { get; }
    DbSet<VehicleDevice> VehicleDevices { get; }
    DbSet<VehiclePosition> VehiclePositions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs several SaveChanges atomically. Needed when one step frees a unique value another step takes
    /// (ending the current assignment before starting the next): EF does not order commands by filtered indexes.
    /// </summary>
    Task InTransactionAsync(Func<Task> work, CancellationToken ct);
}

/// <summary>The authenticated caller. Never trust company/user ids sent by the client — use this.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? CompanyId { get; }
    IReadOnlySet<string> Permissions { get; }
    string? TraceId { get; }

    bool HasPermission(string permission) => Permissions.Contains(permission);
}

/// <summary>
/// Lets a background job act for one company (ADR-045): while active, ICurrentUser reports that company and every
/// permission, so tenant filters and permission-gated services work unchanged. Only job code activates it — never a
/// request. What the job produces is filtered again per user when read (alert audiences).
/// </summary>
public sealed class SystemExecutionContext
{
    public Guid? CompanyId { get; private set; }
    public bool IsActive => CompanyId.HasValue;

    public void ActAsSystemFor(Guid companyId)
    {
        if (IsActive && CompanyId != companyId)
            throw new InvalidOperationException("A system scope serves a single company; create a new scope per company.");
        CompanyId = companyId;
    }
}

public interface IClock
{
    DateTime UtcNow { get; }
    /// <summary>Business date in Brazil (America/Sao_Paulo) — used for license expiry, ages etc.</summary>
    DateOnly Today { get; }
    /// <summary>Wall-clock time in Brazil of a UTC instant (for messages; storage stays UTC).</summary>
    DateTime ToBusinessDateTime(DateTime utc);
    /// <summary>Business date of a UTC instant.</summary>
    DateOnly ToBusinessDate(DateTime utc);
    /// <summary>UTC instant at which a business date starts — for "per day/month" queries over UTC columns.</summary>
    DateTime StartOfBusinessDayUtc(DateOnly date);
}

public static class ClockExtensions
{
    /// <summary>"dd/mm/aaaa hh:mm" in Brazilian time, for user-facing messages built on the server.</summary>
    public static string FormatDateTime(this IClock clock, DateTime utc) => BrazilianFormat.DateTime(clock.ToBusinessDateTime(utc));
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordCheck Verify(string hash, string password);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user, IReadOnlyCollection<string> permissions);
    /// <summary>Cryptographically random opaque token (sent to the browser in an HttpOnly cookie).</summary>
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
    TimeSpan RefreshTokenLifetime { get; }
}

/// <summary>
/// Where file bytes live (ADR-022). Local disk today; object/cloud storage later without touching the modules.
/// Keys are generated by the server — implementations may assume they are safe relative paths.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
