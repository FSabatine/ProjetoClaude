using Fleet.Domain.Companies;
using Fleet.Domain.Implements;
using Fleet.Domain.Maintenance;
using Fleet.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

// Phase 3 — maintenance. Same shape as Phase 2: history/catalog tables, CompanyId-first indexes,
// Restrict FKs everywhere except the aggregate's own child collections (Cascade).

internal sealed class WorkshopConfiguration : IEntityTypeConfiguration<Workshop>
{
    public void Configure(EntityTypeBuilder<Workshop> builder)
    {
        builder.ToTable("Workshops");
        builder.Property(w => w.Name).HasMaxLength(Workshop.NameMaxLength).IsRequired();
        builder.Property(w => w.Document).HasMaxLength(20).IsUnicode(false);
        builder.Property(w => w.Phone).HasMaxLength(11).IsUnicode(false);
        builder.Property(w => w.Email).HasMaxLength(254);
        builder.Property(w => w.Specialties).HasMaxLength(Workshop.SpecialtiesMaxLength);
        builder.Property(w => w.Status).HasMaxLength(20);
        builder.Property(w => w.Notes).HasMaxLength(Workshop.NotesMaxLength);
        builder.ConfigureAddress();

        builder.HasOne<Company>().WithMany().HasForeignKey(w => w.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(w => new { w.CompanyId, w.Name });
        builder.HasIndex(w => new { w.CompanyId, w.Status });
    }
}

internal sealed class MaintenancePlanConfiguration : IEntityTypeConfiguration<MaintenancePlan>
{
    public void Configure(EntityTypeBuilder<MaintenancePlan> builder)
    {
        builder.ToTable("MaintenancePlans");
        builder.Property(p => p.Name).HasMaxLength(MaintenancePlan.NameMaxLength).IsRequired();
        builder.Property(p => p.VehicleType).HasMaxLength(30);

        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Vehicle).WithMany().HasForeignKey(p => p.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.PlanId).OnDelete(DeleteBehavior.Cascade);

        // Resolving the plan for a vehicle (MaintenancePlanResolver) scans active plans by these three shapes.
        builder.HasIndex(p => new { p.CompanyId, p.VehicleId });
        builder.HasIndex(p => new { p.CompanyId, p.VehicleType });
    }
}

internal sealed class MaintenancePlanItemConfiguration : IEntityTypeConfiguration<MaintenancePlanItem>
{
    public void Configure(EntityTypeBuilder<MaintenancePlanItem> builder)
    {
        builder.ToTable("MaintenancePlanItems");
        builder.Property(i => i.ServiceName).HasMaxLength(MaintenancePlanItem.ServiceNameMaxLength).IsRequired();
        builder.Property(i => i.Priority).HasMaxLength(20);
        builder.Property(i => i.Notes).HasMaxLength(MaintenancePlanItem.NotesMaxLength);
        builder.Property(i => i.IntervalHours).HasPrecision(10, 1);
        builder.Property(i => i.GraceHours).HasPrecision(10, 1);
        builder.Property(i => i.EstimatedCost).HasPrecision(18, 2);
        builder.HasIndex(i => i.PlanId);
    }
}

internal sealed class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> builder)
    {
        builder.ToTable("MaintenanceSchedules");
        builder.Property(s => s.LastPerformedHours).HasPrecision(10, 1);
        builder.Property(s => s.NextDueHours).HasPrecision(10, 1);

        builder.HasOne<Company>().WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Vehicle).WithMany().HasForeignKey(s => s.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MaintenancePlanItem>().WithMany().HasForeignKey(s => s.MaintenancePlanItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(s => s.LastWorkOrderId).OnDelete(DeleteBehavior.Restrict);

        // One row per (vehicle, item): recalculated in place when the item is serviced again.
        builder.HasIndex(s => new { s.VehicleId, s.MaintenancePlanItemId }).IsUnique();
        builder.HasIndex(s => new { s.CompanyId, s.NextDueOn });
        builder.HasIndex(s => new { s.CompanyId, s.NextDueKm });
    }
}

internal sealed class HourMeterReadingConfiguration : IEntityTypeConfiguration<HourMeterReading>
{
    public void Configure(EntityTypeBuilder<HourMeterReading> builder)
    {
        builder.ToTable("HourMeterReadings");
        builder.Property(r => r.Hours).HasPrecision(10, 1);
        builder.Property(r => r.Source).HasMaxLength(20);
        builder.Property(r => r.Status).HasMaxLength(20);
        builder.Property(r => r.Anomaly).HasMaxLength(HourMeterReading.AnomalyMaxLength);
        builder.Property(r => r.Notes).HasMaxLength(HourMeterReading.NotesMaxLength);
        builder.Property(r => r.ReviewNotes).HasMaxLength(HourMeterReading.NotesMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Vehicle).WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.VehicleId, r.ReadAt });
        builder.HasIndex(r => new { r.CompanyId, r.Status });
    }
}

internal sealed class MaintenanceRequestConfiguration : IEntityTypeConfiguration<MaintenanceRequest>
{
    public void Configure(EntityTypeBuilder<MaintenanceRequest> builder)
    {
        builder.ToTable("MaintenanceRequests");
        builder.Property(r => r.Source).HasMaxLength(20);
        builder.Property(r => r.MaintenanceType).HasMaxLength(20);
        builder.Property(r => r.Priority).HasMaxLength(20);
        builder.Property(r => r.Description).HasMaxLength(MaintenanceRequest.DescriptionMaxLength).IsRequired();
        builder.Property(r => r.HourMeter).HasPrecision(10, 1);
        builder.Property(r => r.Status).HasMaxLength(20);
        builder.Property(r => r.RejectionReason).HasMaxLength(MaintenanceRequest.RejectionReasonMaxLength);
        builder.Ignore(r => r.IsClosed);

        builder.HasOne<Company>().WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Vehicle).WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Driver).WithMany().HasForeignKey(r => r.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Occurrence).WithMany().HasForeignKey(r => r.OccurrenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(r => r.WorkOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.Status, r.ReportedAt });
        builder.HasIndex(r => new { r.CompanyId, r.VehicleId, r.ReportedAt });
    }
}

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders");
        builder.Property(w => w.Type).HasMaxLength(20);
        builder.Property(w => w.Priority).HasMaxLength(20);
        builder.Property(w => w.Status).HasMaxLength(20);
        builder.Property(w => w.Description).HasMaxLength(WorkOrder.DescriptionMaxLength).IsRequired();
        builder.Property(w => w.Diagnosis).HasMaxLength(WorkOrder.DiagnosisMaxLength);
        builder.Property(w => w.Resolution).HasMaxLength(WorkOrder.ResolutionMaxLength);
        builder.Property(w => w.Notes).HasMaxLength(WorkOrder.NotesMaxLength);
        builder.Property(w => w.HourMeter).HasPrecision(10, 1);
        builder.Property(w => w.PartsCost).HasPrecision(18, 2);
        builder.Property(w => w.LaborCost).HasPrecision(18, 2);
        builder.Property(w => w.OtherCost).HasPrecision(18, 2);
        builder.Property(w => w.TotalCost).HasPrecision(18, 2);
        builder.Ignore(w => w.IsClosed);
        builder.Ignore(w => w.IsActive);
        builder.Ignore(w => w.Number);

        builder.HasOne<Company>().WithMany().HasForeignKey(w => w.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.Vehicle).WithMany().HasForeignKey(w => w.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.Implement).WithMany().HasForeignKey(w => w.ImplementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.MaintenanceRequest).WithMany().HasForeignKey(w => w.MaintenanceRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.Workshop).WithMany().HasForeignKey(w => w.WorkshopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(w => w.Items).WithOne().HasForeignKey(i => i.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(w => w.Parts).WithOne().HasForeignKey(p => p.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(w => w.Labor).WithOne().HasForeignKey(l => l.WorkOrderId).OnDelete(DeleteBehavior.Cascade);

        // Sequence drives the human-readable Number — never deleted (history), so a plain unique index is enough.
        builder.HasIndex(w => new { w.CompanyId, w.Sequence }).IsUnique();
        builder.HasIndex(w => new { w.CompanyId, w.Status, w.Priority });
        builder.HasIndex(w => new { w.CompanyId, w.VehicleId, w.OpenedAt });
        builder.HasIndex(w => new { w.CompanyId, w.OpenedAt });
    }
}

internal sealed class WorkOrderItemConfiguration : IEntityTypeConfiguration<WorkOrderItem>
{
    public void Configure(EntityTypeBuilder<WorkOrderItem> builder)
    {
        builder.ToTable("WorkOrderItems");
        builder.Property(i => i.Description).HasMaxLength(WorkOrderItem.DescriptionMaxLength).IsRequired();
        builder.Property(i => i.Status).HasMaxLength(20);
        builder.Property(i => i.Notes).HasMaxLength(WorkOrderItem.NotesMaxLength);
        builder.HasOne<MaintenancePlanItem>().WithMany().HasForeignKey(i => i.MaintenancePlanItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.WorkOrderId);
    }
}

internal sealed class WorkOrderPartConfiguration : IEntityTypeConfiguration<WorkOrderPart>
{
    public void Configure(EntityTypeBuilder<WorkOrderPart> builder)
    {
        builder.ToTable("WorkOrderParts");
        builder.Property(p => p.PartName).HasMaxLength(WorkOrderPart.PartNameMaxLength).IsRequired();
        builder.Property(p => p.PartNumber).HasMaxLength(WorkOrderPart.PartNumberMaxLength);
        builder.Property(p => p.Supplier).HasMaxLength(WorkOrderPart.SupplierMaxLength);
        builder.Property(p => p.Notes).HasMaxLength(WorkOrderPart.NotesMaxLength);
        builder.Property(p => p.Quantity).HasPrecision(12, 2);
        builder.Property(p => p.UnitCost).HasPrecision(18, 2);
        builder.Ignore(p => p.TotalCost);
        builder.HasIndex(p => p.WorkOrderId);
    }
}

internal sealed class WorkOrderLaborConfiguration : IEntityTypeConfiguration<WorkOrderLabor>
{
    public void Configure(EntityTypeBuilder<WorkOrderLabor> builder)
    {
        builder.ToTable("WorkOrderLabor");
        builder.Property(l => l.TechnicianName).HasMaxLength(WorkOrderLabor.TechnicianNameMaxLength).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(WorkOrderLabor.DescriptionMaxLength);
        builder.Property(l => l.Hours).HasPrecision(10, 2);
        builder.Property(l => l.HourlyRate).HasPrecision(18, 2);
        builder.Ignore(l => l.TotalCost);
        builder.HasIndex(l => l.WorkOrderId);
    }
}
