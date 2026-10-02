using Fleet.Domain.Auditing;
using Fleet.Domain.Drivers;
using Fleet.Domain.Implements;
using Fleet.Domain.Validation;
using Fleet.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");
        builder.Property(d => d.FullName).HasMaxLength(Driver.FullNameMaxLength).IsRequired();
        builder.Property(d => d.Cpf).IsFixedCode(Cpf.Length).IsRequired();
        builder.Property(d => d.Rg).HasMaxLength(Driver.RgMaxLength);
        builder.Property(d => d.Phone).HasMaxLength(Phone.MaxLength).IsUnicode(false);
        builder.Property(d => d.Email).HasMaxLength(EmailAddress.MaxLength);
        builder.Property(d => d.LicenseNumber).IsFixedCode(DriverLicenseNumber.Length).IsRequired();
        builder.Property(d => d.LicenseCategory).HasMaxLength(2);
        builder.Property(d => d.Status).HasMaxLength(20);
        builder.Property(d => d.Notes).HasMaxLength(Driver.NotesMaxLength);
        builder.ConfigureAddress();

        builder.HasOne<Fleet.Domain.Companies.Company>().WithMany().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.CompanyId, d.Cpf }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(d => new { d.CompanyId, d.LicenseNumber }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(d => new { d.CompanyId, d.Status });
        builder.HasIndex(d => new { d.CompanyId, d.LicenseExpiresOn });
    }
}

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.Property(v => v.LicensePlate).IsFixedCode(LicensePlate.Length).IsRequired();
        builder.Property(v => v.Renavam).IsFixedCode(Renavam.Length).IsRequired();
        builder.Property(v => v.Chassis).HasMaxLength(Chassis.Length).IsUnicode(false).IsRequired();
        builder.Property(v => v.Manufacturer).HasMaxLength(Vehicle.ManufacturerMaxLength).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(Vehicle.ModelMaxLength).IsRequired();
        builder.Property(v => v.Color).HasMaxLength(Vehicle.ColorMaxLength);
        builder.Property(v => v.FuelType).HasMaxLength(20);
        builder.Property(v => v.CargoCapacityKg).HasPrecision(12, 2);
        builder.Property(v => v.TareWeightKg).HasPrecision(12, 2);
        builder.Property(v => v.HourMeter).HasPrecision(10, 1);
        builder.Property(v => v.FuelTankCapacity).HasPrecision(10, 2);
        builder.Property(v => v.SecondaryFuelTankCapacity).HasPrecision(10, 2);
        builder.Property(v => v.ExpectedConsumption).HasPrecision(10, 2);
        builder.Property(v => v.AcquisitionValue).HasPrecision(18, 2);
        builder.Property(v => v.Notes).HasMaxLength(Vehicle.NotesMaxLength);

        builder.HasOne<Fleet.Domain.Companies.Company>().WithMany().HasForeignKey(v => v.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => new { v.CompanyId, v.LicensePlate }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(v => new { v.CompanyId, v.Renavam }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(v => new { v.CompanyId, v.Chassis }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(v => new { v.CompanyId, v.Status });
        builder.HasIndex(v => new { v.CompanyId, v.OdometerUpdatedAt });
    }
}

internal sealed class ImplementConfiguration : IEntityTypeConfiguration<Implement>
{
    public void Configure(EntityTypeBuilder<Implement> builder)
    {
        builder.ToTable("Implements");
        builder.Property(i => i.LicensePlate).IsFixedCode(LicensePlate.Length).IsRequired();
        builder.Property(i => i.Renavam).IsFixedCode(Renavam.Length).IsRequired();
        builder.Property(i => i.Chassis).HasMaxLength(Chassis.Length).IsUnicode(false).IsRequired();
        builder.Property(i => i.Manufacturer).HasMaxLength(Vehicle.ManufacturerMaxLength).IsRequired();
        builder.Property(i => i.Model).HasMaxLength(Vehicle.ModelMaxLength).IsRequired();
        builder.Property(i => i.Capacity).HasPrecision(12, 2);
        builder.Property(i => i.CapacityUnit).HasMaxLength(20);
        builder.Property(i => i.TareWeightKg).HasPrecision(12, 2);
        builder.Property(i => i.Status).HasMaxLength(20);
        builder.Property(i => i.Notes).HasMaxLength(Implement.NotesMaxLength);

        builder.HasOne<Fleet.Domain.Companies.Company>().WithMany().HasForeignKey(i => i.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.CompanyId, i.LicensePlate }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(i => new { i.CompanyId, i.Renavam }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(i => new { i.CompanyId, i.Chassis }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(i => new { i.CompanyId, i.Status });
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.Property(a => a.EntityName).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(20);
        builder.Property(a => a.Changes).IsRequired();
        builder.Property(a => a.TraceId).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(a => new { a.EntityName, a.EntityId, a.OccurredAt });
        builder.HasIndex(a => new { a.CompanyId, a.OccurredAt });
    }
}
