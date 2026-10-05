using Fleet.Domain.Companies;
using Fleet.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

// Final phase — tracking foundation (ADR-051). CompanyId-first indexes; positions are high-volume and append-only.

internal sealed class TrackingProviderConfiguration : IEntityTypeConfiguration<TrackingProvider>
{
    public void Configure(EntityTypeBuilder<TrackingProvider> builder)
    {
        builder.ToTable("TrackingProviders");
        builder.Property(p => p.Name).HasMaxLength(TrackingProvider.NameMaxLength);
        builder.Property(p => p.Notes).HasMaxLength(TrackingProvider.NotesMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.CompanyId, p.Name });
    }
}

internal sealed class TrackingDeviceConfiguration : IEntityTypeConfiguration<TrackingDevice>
{
    public void Configure(EntityTypeBuilder<TrackingDevice> builder)
    {
        builder.ToTable("TrackingDevices");
        builder.Property(d => d.Identifier).HasMaxLength(TrackingDevice.IdentifierMaxLength);
        builder.Property(d => d.Model).HasMaxLength(TrackingDevice.ModelMaxLength);
        builder.Property(d => d.ApiKeyHash).HasMaxLength(64);
        builder.Property(d => d.ApiKeyPrefix).HasMaxLength(TrackingDevice.KeyPrefixLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.TrackingProvider).WithMany().HasForeignKey(d => d.TrackingProviderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.CompanyId, d.Identifier }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        // Ingestion looks the device up by key hash, across companies (no user on that endpoint).
        builder.HasIndex(d => d.ApiKeyHash).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
    }
}

internal sealed class VehicleDeviceConfiguration : IEntityTypeConfiguration<VehicleDevice>
{
    public void Configure(EntityTypeBuilder<VehicleDevice> builder)
    {
        builder.ToTable("VehicleDevices");
        builder.HasOne<Company>().WithMany().HasForeignKey(l => l.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.TrackingDevice).WithMany().HasForeignKey(l => l.TrackingDeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.Vehicle).WithMany().HasForeignKey(l => l.VehicleId).OnDelete(DeleteBehavior.Restrict);
        // One vehicle per device and one device per vehicle at a time.
        builder.HasIndex(l => l.TrackingDeviceId).IsUnique().HasFilter("[EndedAt] IS NULL").HasDatabaseName("UX_VehicleDevices_ActiveDevice");
        builder.HasIndex(l => l.VehicleId).IsUnique().HasFilter("[EndedAt] IS NULL").HasDatabaseName("UX_VehicleDevices_ActiveVehicle");
        builder.HasIndex(l => new { l.CompanyId, l.VehicleId, l.StartedAt });
    }
}

internal sealed class VehiclePositionConfiguration : IEntityTypeConfiguration<VehiclePosition>
{
    public void Configure(EntityTypeBuilder<VehiclePosition> builder)
    {
        builder.ToTable("VehiclePositions");
        builder.Property(p => p.Latitude).HasPrecision(9, 6);
        builder.Property(p => p.Longitude).HasPrecision(9, 6);
        builder.Property(p => p.SpeedKmh).HasPrecision(6, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrackingDevice>().WithMany().HasForeignKey(p => p.TrackingDeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Fleet.Domain.Vehicles.Vehicle>().WithMany().HasForeignKey(p => p.VehicleId).OnDelete(DeleteBehavior.Restrict);
        // A device resending the same fix is a duplicate, not a second position.
        builder.HasIndex(p => new { p.TrackingDeviceId, p.RecordedAt }).IsUnique();
        builder.HasIndex(p => new { p.CompanyId, p.VehicleId, p.RecordedAt });
        builder.HasIndex(p => new { p.CompanyId, p.RecordedAt });
    }
}

internal sealed class TrackingDeviceLastPositionConfiguration : IEntityTypeConfiguration<TrackingDeviceLastPosition>
{
    public void Configure(EntityTypeBuilder<TrackingDeviceLastPosition> builder)
    {
        builder.ToTable("TrackingDeviceLastPositions");
        builder.HasKey(p => p.TrackingDeviceId);
        builder.Property(p => p.TrackingDeviceId).ValueGeneratedNever();
        builder.Property(p => p.Latitude).HasPrecision(9, 6);
        builder.Property(p => p.Longitude).HasPrecision(9, 6);
        builder.Property(p => p.SpeedKmh).HasPrecision(6, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrackingDevice>().WithOne().HasForeignKey<TrackingDeviceLastPosition>(p => p.TrackingDeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.CompanyId, p.RecordedAt });
    }
}
