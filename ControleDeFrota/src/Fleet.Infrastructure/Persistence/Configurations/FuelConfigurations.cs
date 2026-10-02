using Fleet.Domain.Companies;
using Fleet.Domain.Fuel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

// Phase 4 — fuel. Same shape as Phases 2/3: CompanyId-first indexes chosen from the real queries (DATABASE.md),
// Restrict FKs except the fueling's own child rows (Cascade). Money decimal(14,2), quantity (12,3), unit price (10,4).

internal sealed class FuelTypeConfiguration : IEntityTypeConfiguration<FuelType>
{
    public void Configure(EntityTypeBuilder<FuelType> builder)
    {
        builder.ToTable("FuelTypes");
        builder.Property(t => t.Name).HasMaxLength(FuelType.NameMaxLength).IsRequired();
        builder.Property(t => t.Code).HasMaxLength(FuelType.CodeMaxLength).IsUnicode(false).IsRequired();
        builder.Property(t => t.Category).HasMaxLength(20);
        builder.Property(t => t.Unit).HasMaxLength(20);
        builder.Property(t => t.Description).HasMaxLength(FuelType.DescriptionMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.CompanyId, t.Code }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(t => new { t.CompanyId, t.Name }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
    }
}

internal sealed class FuelStationConfiguration : IEntityTypeConfiguration<FuelStation>
{
    public void Configure(EntityTypeBuilder<FuelStation> builder)
    {
        builder.ToTable("FuelStations");
        builder.Property(s => s.Name).HasMaxLength(FuelStation.NameMaxLength).IsRequired();
        builder.Property(s => s.Cnpj).HasMaxLength(14).IsUnicode(false);
        builder.Property(s => s.Phone).HasMaxLength(11).IsUnicode(false);
        builder.Property(s => s.ContactName).HasMaxLength(FuelStation.ContactMaxLength);
        builder.Property(s => s.Notes).HasMaxLength(FuelStation.NotesMaxLength);
        builder.ConfigureAddress();

        builder.HasOne<Company>().WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.CompanyId, s.Name });
        // The same establishment registered twice would split its price and volume history.
        builder.HasIndex(s => new { s.CompanyId, s.Cnpj }).IsUnique()
            .HasFilter($"{ConfigurationExtensions.NotDeletedFilter} AND [Cnpj] IS NOT NULL");
    }
}

internal sealed class FuelPriceConfiguration : IEntityTypeConfiguration<FuelPrice>
{
    public void Configure(EntityTypeBuilder<FuelPrice> builder)
    {
        builder.ToTable("FuelPrices");
        builder.Property(p => p.Price).HasPrecision(10, 4);
        builder.Property(p => p.Notes).HasMaxLength(FuelPrice.NotesMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.FuelStation).WithMany().HasForeignKey(p => p.FuelStationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.FuelType).WithMany().HasForeignKey(p => p.FuelTypeId).OnDelete(DeleteBehavior.Restrict);
        // "Price in force at a station for a product on a date" = latest EffectiveFrom <= date.
        builder.HasIndex(p => new { p.CompanyId, p.FuelStationId, p.FuelTypeId, p.EffectiveFrom });
    }
}

internal sealed class FuelSettingsConfiguration : IEntityTypeConfiguration<FuelSettings>
{
    public void Configure(EntityTypeBuilder<FuelSettings> builder)
    {
        builder.ToTable("FuelSettings");
        builder.HasOne<Company>().WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.CompanyId).IsUnique();
    }
}

internal sealed class FuelingConfiguration : IEntityTypeConfiguration<Fueling>
{
    public void Configure(EntityTypeBuilder<Fueling> builder)
    {
        builder.ToTable("Fuelings");
        builder.Property(f => f.Quantity).HasPrecision(12, 3);
        builder.Property(f => f.UnitPrice).HasPrecision(10, 4);
        builder.Property(f => f.TotalAmount).HasPrecision(14, 2);
        builder.Property(f => f.PaymentMethod).HasMaxLength(20);
        builder.Property(f => f.Source).HasMaxLength(20);
        builder.Property(f => f.Status).HasMaxLength(20);
        builder.Property(f => f.ReceiptNumber).HasMaxLength(Fueling.ReceiptMaxLength);
        builder.Property(f => f.Notes).HasMaxLength(Fueling.NotesMaxLength);
        builder.Property(f => f.ReviewNotes).HasMaxLength(Fueling.ReasonMaxLength);
        builder.Property(f => f.CancellationReason).HasMaxLength(Fueling.ReasonMaxLength);
        builder.Property(f => f.ConsumptionResult).HasMaxLength(20);
        builder.Property(f => f.SegmentQuantity).HasPrecision(12, 3);
        builder.Property(f => f.SegmentCost).HasPrecision(14, 2);
        builder.Property(f => f.Consumption).HasPrecision(10, 2);
        builder.Property(f => f.ExpectedConsumption).HasPrecision(10, 2);
        builder.Property(f => f.SegmentExpectedQuantity).HasPrecision(12, 3);
        builder.Property(f => f.BaselineSource).HasMaxLength(30);
        builder.Property(f => f.ConsumptionDeviationPercent).HasPrecision(8, 1);

        builder.HasOne<Company>().WithMany().HasForeignKey(f => f.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Vehicle).WithMany().HasForeignKey(f => f.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Driver).WithMany().HasForeignKey(f => f.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.FuelStation).WithMany().HasForeignKey(f => f.FuelStationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.FuelType).WithMany().HasForeignKey(f => f.FuelTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(f => f.Anomalies).WithOne().HasForeignKey(a => a.FuelingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(f => f.Corrections).WithOne().HasForeignKey(c => c.FuelingId).OnDelete(DeleteBehavior.Cascade);

        // Vehicle chain: consumption segments, the vehicle's Fuel tab, the frequency rule.
        builder.HasIndex(f => new { f.CompanyId, f.VehicleId, f.FueledAt });
        // Period lists, dashboard and reports (date range first, then the grouping happens on the range).
        builder.HasIndex(f => new { f.CompanyId, f.FueledOn });
        // Review queue ("requires review") and the dashboard counter.
        builder.HasIndex(f => new { f.CompanyId, f.Status });
        // Reference price of a product over the last days (price rule) and price analysis by product.
        builder.HasIndex(f => new { f.CompanyId, f.FuelTypeId, f.FueledOn });
        // Fueling history of a station and the station report.
        builder.HasIndex(f => new { f.CompanyId, f.FuelStationId, f.FueledOn });
        // Fuelings by driver (filter and cost by driver).
        builder.HasIndex(f => new { f.CompanyId, f.DriverId, f.FueledOn });
    }
}

internal sealed class FuelingAnomalyConfiguration : IEntityTypeConfiguration<FuelingAnomaly>
{
    public void Configure(EntityTypeBuilder<FuelingAnomaly> builder)
    {
        builder.ToTable("FuelingAnomalies");
        builder.Property(a => a.Type).HasMaxLength(30);
        builder.Property(a => a.Message).HasMaxLength(FuelingAnomaly.MessageMaxLength).IsRequired();
        builder.Property(a => a.ExpectedValue).HasPrecision(14, 4);
        builder.Property(a => a.ActualValue).HasPrecision(14, 4);
        builder.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.CompanyId, a.Type });
    }
}

internal sealed class FuelingCorrectionConfiguration : IEntityTypeConfiguration<FuelingCorrection>
{
    public void Configure(EntityTypeBuilder<FuelingCorrection> builder)
    {
        builder.ToTable("FuelingCorrections");
        builder.Property(c => c.Reason).HasMaxLength(Fueling.ReasonMaxLength).IsRequired();
        builder.Property(c => c.Changes).HasMaxLength(FuelingCorrection.ChangesMaxLength).IsRequired();
        builder.HasOne<Company>().WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
