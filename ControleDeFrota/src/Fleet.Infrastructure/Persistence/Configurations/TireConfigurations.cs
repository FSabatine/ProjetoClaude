using Fleet.Domain.Companies;
using Fleet.Domain.Implements;
using Fleet.Domain.Tires;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

// Phase 5 — tires. CompanyId-first indexes chosen from the real queries (DATABASE.md). Consistency rules that two concurrent
// requests could break are filtered unique indexes (seção 52): one open stint per tire, one per position, one open service
// order per tire. Tread/pressure (5,2), money (14,2), cost/km is never stored.

internal sealed class TireModelConfiguration : IEntityTypeConfiguration<TireModel>
{
    public void Configure(EntityTypeBuilder<TireModel> builder)
    {
        builder.ToTable("TireModels");
        builder.Property(m => m.Brand).HasMaxLength(TireModel.BrandMaxLength).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(TireModel.NameMaxLength).IsRequired();
        builder.Property(m => m.Size).HasMaxLength(TireModel.SizeMaxLength).IsUnicode(false).IsRequired();
        builder.Property(m => m.Application).HasMaxLength(20);
        builder.Property(m => m.Construction).HasMaxLength(20);
        builder.Property(m => m.LoadIndex).HasMaxLength(TireModel.RatingMaxLength);
        builder.Property(m => m.SpeedRating).HasMaxLength(TireModel.RatingMaxLength);
        builder.Property(m => m.OriginalTreadDepthMm).HasPrecision(5, 2);
        builder.Property(m => m.Notes).HasMaxLength(TireModel.NotesMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(m => m.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.CompanyId, m.Brand, m.Name, m.Size }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
    }
}

internal sealed class TireLayoutConfiguration : IEntityTypeConfiguration<TireLayout>
{
    public void Configure(EntityTypeBuilder<TireLayout> builder)
    {
        builder.ToTable("TireLayouts");
        builder.Property(l => l.Name).HasMaxLength(TireLayout.NameMaxLength).IsRequired();
        builder.Property(l => l.Target).HasMaxLength(20);
        builder.Property(l => l.Description).HasMaxLength(TireLayout.DescriptionMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(l => l.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(l => l.Axles).WithOne().HasForeignKey(a => a.TireLayoutId).OnDelete(DeleteBehavior.Cascade);
        // The layout is an attribute of the asset (like the fuel profile, ADR-031) — configured here to keep the registry untouched.
        builder.HasMany<Vehicle>().WithOne().HasForeignKey(v => v.TireLayoutId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<Implement>().WithOne().HasForeignKey(i => i.TireLayoutId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.CompanyId, l.Name }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
    }
}

internal sealed class TireLayoutAxleConfiguration : IEntityTypeConfiguration<TireLayoutAxle>
{
    public void Configure(EntityTypeBuilder<TireLayoutAxle> builder)
    {
        builder.ToTable("TireLayoutAxles");
        builder.Property(a => a.Type).HasMaxLength(20);
        builder.Property(a => a.AllowedSize).HasMaxLength(TireModel.SizeMaxLength).IsUnicode(false);
        builder.Property(a => a.RecommendedPressurePsi).HasPrecision(6, 1);
        builder.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.TireLayoutId, a.Number }).IsUnique();
    }
}

internal sealed class TireConfiguration : IEntityTypeConfiguration<Tire>
{
    public void Configure(EntityTypeBuilder<Tire> builder)
    {
        builder.ToTable("Tires");
        builder.Property(t => t.Code).HasMaxLength(Tire.CodeMaxLength).IsRequired();
        builder.Property(t => t.SerialNumber).HasMaxLength(Tire.SerialMaxLength);
        builder.Property(t => t.Dot).HasMaxLength(Tire.DotMaxLength).IsUnicode(false);
        builder.Property(t => t.PurchasePrice).HasPrecision(14, 2);
        builder.Property(t => t.Supplier).HasMaxLength(Tire.SupplierMaxLength);
        builder.Property(t => t.OriginalTreadDepthMm).HasPrecision(5, 2);
        builder.Property(t => t.StorageLocation).HasMaxLength(Tire.StorageMaxLength);
        builder.Property(t => t.Notes).HasMaxLength(Tire.NotesMaxLength);
        builder.Property(t => t.Status).HasMaxLength(20);
        builder.Property(t => t.CurrentTreadDepthMm).HasPrecision(5, 2);
        builder.Property(t => t.LastWearPattern).HasMaxLength(20);
        builder.Property(t => t.LastPressureCheck).HasMaxLength(20);
        builder.Property(t => t.DisposalReason).HasMaxLength(30);
        builder.Property(t => t.DisposalDestination).HasMaxLength(Tire.DisposalTextMaxLength);
        builder.Property(t => t.DisposalNotes).HasMaxLength(Tire.DisposalTextMaxLength);
        // Seção 52: the second of two concurrent operations on the same tire fails instead of overwriting the first.
        builder.Property(t => t.Version).IsConcurrencyToken();

        builder.HasOne<Company>().WithMany().HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Model).WithMany().HasForeignKey(t => t.TireModelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.CompanyId, t.Code }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(t => new { t.CompanyId, t.Sequence }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        // Inventory by status, dashboard counters (status first: every alert filter is applied on top of it).
        builder.HasIndex(t => new { t.CompanyId, t.Status });
        builder.HasIndex(t => new { t.CompanyId, t.TireModelId });
    }
}

internal sealed class TireInstallationConfiguration : IEntityTypeConfiguration<TireInstallation>
{
    public void Configure(EntityTypeBuilder<TireInstallation> builder)
    {
        builder.ToTable("TireInstallations");
        builder.Property(i => i.PositionCode).HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(i => i.PositionLabel).HasMaxLength(TireInstallation.LabelMaxLength).IsRequired();
        builder.Property(i => i.InstalledHourMeter).HasPrecision(10, 1);
        builder.Property(i => i.RemovedHourMeter).HasPrecision(10, 1);
        builder.Property(i => i.InstallReason).HasMaxLength(20);
        builder.Property(i => i.RemovalReason).HasMaxLength(30);
        builder.Property(i => i.RemovalDestination).HasMaxLength(20);
        builder.Property(i => i.Notes).HasMaxLength(TireInstallation.NotesMaxLength);
        builder.Property(i => i.RemovalNotes).HasMaxLength(TireInstallation.NotesMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(i => i.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Tire).WithMany().HasForeignKey(i => i.TireId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Vehicle).WithMany().HasForeignKey(i => i.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Implement).WithMany().HasForeignKey(i => i.ImplementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TireRotation>().WithMany().HasForeignKey(i => i.RotationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TireRotation>().WithMany().HasForeignKey(i => i.RemovalRotationId).OnDelete(DeleteBehavior.Restrict);

        // Seções 27/52: a tire is on one position at most, a position holds one tire at most — even under concurrent requests.
        builder.HasIndex(i => i.TireId).IsUnique().HasFilter("[RemovedAt] IS NULL").HasDatabaseName("IX_TireInstallations_OpenByTire");
        builder.HasIndex(i => new { i.VehicleId, i.PositionCode }).IsUnique()
            .HasFilter("[RemovedAt] IS NULL AND [VehicleId] IS NOT NULL").HasDatabaseName("IX_TireInstallations_OpenByVehiclePosition");
        builder.HasIndex(i => new { i.ImplementId, i.PositionCode }).IsUnique()
            .HasFilter("[RemovedAt] IS NULL AND [ImplementId] IS NOT NULL").HasDatabaseName("IX_TireInstallations_OpenByImplementPosition");
        // Tire lifecycle (installations tab), asset tire history and the installation-period filter of the lifecycle report.
        builder.HasIndex(i => new { i.CompanyId, i.TireId, i.InstalledAt });
        builder.HasIndex(i => new { i.CompanyId, i.VehicleId, i.InstalledAt });
        builder.HasIndex(i => new { i.CompanyId, i.ImplementId, i.InstalledAt });
        builder.HasIndex(i => new { i.CompanyId, i.InstalledAt });
    }
}

internal sealed class TireRotationConfiguration : IEntityTypeConfiguration<TireRotation>
{
    public void Configure(EntityTypeBuilder<TireRotation> builder)
    {
        builder.ToTable("TireRotations");
        builder.Property(r => r.Reason).HasMaxLength(TireRotation.ReasonMaxLength);
        builder.Property(r => r.Notes).HasMaxLength(TireInstallation.NotesMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Vehicle).WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Implement).WithMany().HasForeignKey(r => r.ImplementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.CompanyId, r.VehicleId, r.PerformedAt });
        builder.HasIndex(r => new { r.CompanyId, r.ImplementId, r.PerformedAt });
    }
}

internal sealed class TireInspectionConfiguration : IEntityTypeConfiguration<TireInspection>
{
    public void Configure(EntityTypeBuilder<TireInspection> builder)
    {
        builder.ToTable("TireInspections");
        builder.Property(i => i.PositionCode).HasMaxLength(10).IsUnicode(false);
        builder.Property(i => i.PositionLabel).HasMaxLength(TireInstallation.LabelMaxLength);
        builder.Property(i => i.Source).HasMaxLength(20);
        builder.Property(i => i.TreadDepthMm).HasPrecision(5, 2);
        builder.Property(i => i.Pressure).HasPrecision(7, 2);
        builder.Property(i => i.PressureUnit).HasMaxLength(10);
        builder.Property(i => i.PressureCheck).HasMaxLength(20);
        builder.Property(i => i.Condition).HasMaxLength(20);
        builder.Property(i => i.WearPattern).HasMaxLength(20);
        builder.Property(i => i.Notes).HasMaxLength(TireInspection.NotesMaxLength);
        builder.Ignore(i => i.RequiresAction);

        builder.HasOne<Company>().WithMany().HasForeignKey(i => i.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Tire).WithMany().HasForeignKey(i => i.TireId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Vehicle).WithMany().HasForeignKey(i => i.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Implement).WithMany().HasForeignKey(i => i.ImplementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TireInstallation>().WithMany().HasForeignKey(i => i.InstallationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Damages).WithOne().HasForeignKey(d => d.InspectionId).OnDelete(DeleteBehavior.Cascade);

        // Tread history of a tire (inspections tab, wear rate), inspection report by period, last inspection per vehicle.
        builder.HasIndex(i => new { i.CompanyId, i.TireId, i.InspectedAt });
        builder.HasIndex(i => new { i.CompanyId, i.InspectedAt });
        builder.HasIndex(i => new { i.CompanyId, i.VehicleId, i.InspectedAt });
    }
}

internal sealed class TireInspectionDamageConfiguration : IEntityTypeConfiguration<TireInspectionDamage>
{
    public void Configure(EntityTypeBuilder<TireInspectionDamage> builder)
    {
        builder.ToTable("TireInspectionDamages");
        builder.Property(d => d.Type).HasMaxLength(20);
        builder.HasOne<Company>().WithMany().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.InspectionId, d.Type }).IsUnique();
        // "Repeated punctures": damages of a type over a period.
        builder.HasIndex(d => new { d.CompanyId, d.Type });
    }
}

internal sealed class TireServiceOrderConfiguration : IEntityTypeConfiguration<TireServiceOrder>
{
    public void Configure(EntityTypeBuilder<TireServiceOrder> builder)
    {
        builder.ToTable("TireServiceOrders");
        builder.Property(o => o.Kind).HasMaxLength(20);
        builder.Property(o => o.Status).HasMaxLength(20);
        builder.Property(o => o.Result).HasMaxLength(20);
        builder.Property(o => o.RepairType).HasMaxLength(20);
        builder.Property(o => o.ProviderName).HasMaxLength(TireServiceOrder.ProviderMaxLength);
        builder.Property(o => o.TreadPattern).HasMaxLength(TireServiceOrder.TreadPatternMaxLength);
        builder.Property(o => o.NewTreadDepthMm).HasPrecision(5, 2);
        builder.Property(o => o.Cost).HasPrecision(14, 2);
        builder.Property(o => o.Description).HasMaxLength(TireServiceOrder.TextMaxLength);
        builder.Property(o => o.ResultNotes).HasMaxLength(TireServiceOrder.TextMaxLength);
        builder.Property(o => o.CancellationReason).HasMaxLength(TireServiceOrder.TextMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(o => o.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Tire).WithMany().HasForeignKey(o => o.TireId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Workshop).WithMany().HasForeignKey(o => o.WorkshopId).OnDelete(DeleteBehavior.Restrict);
        // A tire is at one provider at a time.
        builder.HasIndex(o => o.TireId).IsUnique().HasFilter("[Status] = 'Open'").HasDatabaseName("IX_TireServiceOrders_OpenByTire");
        builder.HasIndex(o => new { o.CompanyId, o.TireId, o.SentAt });
        builder.HasIndex(o => new { o.CompanyId, o.Status });
    }
}

internal sealed class TireCostConfiguration : IEntityTypeConfiguration<TireCost>
{
    public void Configure(EntityTypeBuilder<TireCost> builder)
    {
        builder.ToTable("TireCosts");
        builder.Property(c => c.Type).HasMaxLength(20);
        builder.Property(c => c.Amount).HasPrecision(14, 2);
        builder.Property(c => c.Description).HasMaxLength(TireCost.DescriptionMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tire>().WithMany().HasForeignKey(c => c.TireId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TireServiceOrder>().WithMany().HasForeignKey(c => c.ServiceOrderId).OnDelete(DeleteBehavior.Restrict);
        // Lifecycle cost of a tire (GROUP BY TireId, Type) and costs by period.
        builder.HasIndex(c => new { c.CompanyId, c.TireId, c.Type });
        builder.HasIndex(c => new { c.CompanyId, c.IncurredOn });
    }
}

internal sealed class TireAnomalyConfiguration : IEntityTypeConfiguration<TireAnomaly>
{
    public void Configure(EntityTypeBuilder<TireAnomaly> builder)
    {
        builder.ToTable("TireAnomalies");
        builder.Property(a => a.Type).HasMaxLength(30);
        builder.Property(a => a.Message).HasMaxLength(TireAnomaly.MessageMaxLength).IsRequired();
        builder.Property(a => a.ReviewNotes).HasMaxLength(TireInstallation.NotesMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tire>().WithMany().HasForeignKey(a => a.TireId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.CompanyId, a.TireId });
        // "Requires review" queue (ReviewedAt null) of the dashboard.
        builder.HasIndex(a => new { a.CompanyId, a.ReviewedAt });
    }
}

internal sealed class TireSettingsConfiguration : IEntityTypeConfiguration<TireSettings>
{
    public void Configure(EntityTypeBuilder<TireSettings> builder)
    {
        builder.ToTable("TireSettings");
        builder.Property(s => s.MinTreadDepthMm).HasPrecision(5, 2);
        builder.Property(s => s.TreadWarningDepthMm).HasPrecision(5, 2);
        builder.Property(s => s.RapidWearMmPer1000Km).HasPrecision(5, 2);
        builder.Property(s => s.PressureUnit).HasMaxLength(10);
        builder.HasOne<Company>().WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.CompanyId).IsUnique();
    }
}
