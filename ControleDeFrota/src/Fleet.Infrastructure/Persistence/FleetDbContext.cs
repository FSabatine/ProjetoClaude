using System.Linq.Expressions;
using System.Reflection;
using Fleet.Application.Common;
using Fleet.Domain.Assignments;
using Fleet.Domain.Auditing;
using Fleet.Domain.Checklists;
using Fleet.Domain.Common;
using Fleet.Domain.Companies;
using Fleet.Domain.Documents;
using Fleet.Domain.Drivers;
using Fleet.Domain.Files;
using Fleet.Domain.Fuel;
using Fleet.Domain.Implements;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Operations;
using Fleet.Domain.Tires;
using Fleet.Domain.Users;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fleet.Infrastructure.Persistence;

public sealed class FleetDbContext : DbContext, IFleetDbContext
{
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public FleetDbContext(DbContextOptions<FleetDbContext> options, ICurrentUser currentUser, IClock clock) : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
        // Cascades run inside SaveChanges, after soft delete has turned the Remove() into an update —
        // so soft-deleting a user keeps its role assignments. Orphan removal (e.g. unassigning a role) stays immediate.
        ChangeTracker.CascadeDeleteTiming = Microsoft.EntityFrameworkCore.ChangeTracking.CascadeTiming.OnSaveChanges;
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Implement> Implements => Set<Implement>();
    public DbSet<VehicleAssignment> VehicleAssignments => Set<VehicleAssignment>();
    public DbSet<OdometerReading> OdometerReadings => Set<OdometerReading>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<ChecklistExecution> ChecklistExecutions => Set<ChecklistExecution>();
    public DbSet<Occurrence> Occurrences => Set<Occurrence>();
    public DbSet<OperationalEvent> OperationalEvents => Set<OperationalEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<HourMeterReading> HourMeterReadings => Set<HourMeterReading>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<FuelType> FuelTypes => Set<FuelType>();
    public DbSet<FuelStation> FuelStations => Set<FuelStation>();
    public DbSet<FuelPrice> FuelPrices => Set<FuelPrice>();
    public DbSet<FuelSettings> FuelSettings => Set<FuelSettings>();
    public DbSet<Fueling> Fuelings => Set<Fueling>();
    public DbSet<FuelingAnomaly> FuelingAnomalies => Set<FuelingAnomaly>();

    public DbSet<TireModel> TireModels => Set<TireModel>();
    public DbSet<TireLayout> TireLayouts => Set<TireLayout>();
    public DbSet<Tire> Tires => Set<Tire>();
    public DbSet<TireInstallation> TireInstallations => Set<TireInstallation>();
    public DbSet<TireRotation> TireRotations => Set<TireRotation>();
    public DbSet<TireInspection> TireInspections => Set<TireInspection>();
    public DbSet<TireInspectionDamage> TireInspectionDamages => Set<TireInspectionDamage>();
    public DbSet<TireServiceOrder> TireServiceOrders => Set<TireServiceOrder>();
    public DbSet<TireCost> TireCosts => Set<TireCost>();
    public DbSet<TireAnomaly> TireAnomalies => Set<TireAnomaly>();
    public DbSet<TireSettings> TireSettings => Set<TireSettings>();

    /// <summary>Read by the tenant query filter on every query (EF parameterizes this per context instance).</summary>
    private Guid? CurrentCompanyId => _currentUser.CompanyId;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // ADR-008: enums stored as text.
        foreach (var enumType in typeof(Company).Assembly.GetTypes().Where(t => t.IsEnum))
            builder.Properties(enumType).HaveConversion<string>().HaveMaxLength(30);

        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

        // SQLite (used only by the test suites, ADR-004) has no decimal type and refuses SUM/AVG/MIN/MAX over
        // decimal columns. Fuel analytics aggregate in the database (ADR-033), so on SQLite decimals are stored as
        // REAL. SQL Server keeps exact decimal(p,s) columns — production aggregation stays exact.
        if (Database.ProviderName == SqliteProviderName)
            builder.Properties<decimal>().HaveConversion<double>();
    }

    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FleetDbContext).Assembly);

        var applyFilter = typeof(FleetDbContext).GetMethod(nameof(ApplyGlobalFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => !t.IsOwned()))
        {
            var clr = entityType.ClrType;
            var softDelete = typeof(ISoftDeletable).IsAssignableFrom(clr);
            var tenant = typeof(ITenantScoped).IsAssignableFrom(clr);
            if (softDelete || tenant)
                applyFilter.MakeGenericMethod(clr).Invoke(this, [modelBuilder, softDelete, tenant]);
        }
    }

    /// <summary>ADR-003 + ADR-011: tenant isolation and soft delete applied to every query.</summary>
    private void ApplyGlobalFilter<T>(ModelBuilder modelBuilder, bool softDelete, bool tenant) where T : class
    {
        Expression<Func<T, bool>> filter = (softDelete, tenant) switch
        {
            (true, true) => e => EF.Property<DateTime?>(e, nameof(ISoftDeletable.DeletedAt)) == null &&
                                 EF.Property<Guid>(e, nameof(ITenantScoped.CompanyId)) == CurrentCompanyId,
            (true, false) => e => EF.Property<DateTime?>(e, nameof(ISoftDeletable.DeletedAt)) == null,
            _ => e => EF.Property<Guid>(e, nameof(ITenantScoped.CompanyId)) == CurrentCompanyId,
        };
        modelBuilder.Entity<T>().HasQueryFilter(filter);
    }

    public async Task InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        // A user transaction must run inside the execution strategy when retry-on-failure is enabled (SQL Server).
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(ct);
            await work();
            await transaction.CommitAsync(ct);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var userId = _currentUser.UserId;

        ApplyTenant();
        ApplySoftDelete(now, userId);
        var auditLogs = new AuditTrailBuilder(ChangeTracker, now, userId, _currentUser.TraceId).Build();
        AuditLogs.AddRange(auditLogs);

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>CompanyId is stamped by the server on insert and can never be changed afterwards.</summary>
    private void ApplyTenant()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CompanyId == Guid.Empty)
            {
                entry.Entity.CompanyId = _currentUser.CompanyId
                    ?? throw new InvalidOperationException("Cannot create a tenant-scoped record without a current company.");
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CompanyId).IsModified = false;
            }
        }
    }

    private void ApplySoftDelete(DateTime now, Guid? userId)
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>().Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Unchanged;
            entry.Entity.DeletedAt = now;
            entry.Entity.DeletedBy = userId;
            // Owned address rows share the table; keep them instead of deleting.
            foreach (var owned in entry.References.Where(r => r.TargetEntry?.Metadata.IsOwned() == true))
                owned.TargetEntry!.State = EntityState.Unchanged;
        }
    }
}

/// <summary>Values are stored as UTC; this restores DateTimeKind.Utc when reading.</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
