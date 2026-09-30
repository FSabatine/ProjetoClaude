using Fleet.Domain.Assignments;
using Fleet.Domain.Checklists;
using Fleet.Domain.Companies;
using Fleet.Domain.Documents;
using Fleet.Domain.Files;
using Fleet.Domain.Mileage;
using Fleet.Domain.Occurrences;
using Fleet.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

// Phase 2 — operational control. History tables are append-mostly and queried by (CompanyId, owner, date):
// every index starts with CompanyId (tenant) and ends with the date the timelines sort by.

internal sealed class VehicleAssignmentConfiguration : IEntityTypeConfiguration<VehicleAssignment>
{
    /// <summary>"Active" = not ended. The filtered unique indexes are the last line of defense against two concurrent assignments.</summary>
    private const string ActiveFilter = "[EndedAt] IS NULL";

    public void Configure(EntityTypeBuilder<VehicleAssignment> builder)
    {
        builder.ToTable("VehicleAssignments");
        builder.Property(a => a.Notes).HasMaxLength(VehicleAssignment.NotesMaxLength);
        builder.Property(a => a.EndReason).HasMaxLength(VehicleAssignment.NotesMaxLength);
        builder.Ignore(a => a.IsActive);

        builder.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Vehicle).WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Driver).WithMany().HasForeignKey(a => a.DriverId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.VehicleId).IsUnique().HasFilter(ActiveFilter).HasDatabaseName("UX_VehicleAssignments_ActiveVehicle");
        builder.HasIndex(a => a.DriverId).IsUnique().HasFilter(ActiveFilter).HasDatabaseName("UX_VehicleAssignments_ActiveDriver");
        builder.HasIndex(a => new { a.CompanyId, a.VehicleId, a.StartedAt });
        builder.HasIndex(a => new { a.CompanyId, a.DriverId, a.StartedAt });
    }
}

internal sealed class OdometerReadingConfiguration : IEntityTypeConfiguration<OdometerReading>
{
    public void Configure(EntityTypeBuilder<OdometerReading> builder)
    {
        builder.ToTable("OdometerReadings");
        builder.Property(r => r.Source).HasMaxLength(20);
        builder.Property(r => r.Status).HasMaxLength(20);
        builder.Property(r => r.Anomaly).HasMaxLength(OdometerReading.AnomalyMaxLength);
        builder.Property(r => r.Notes).HasMaxLength(OdometerReading.NotesMaxLength);
        builder.Property(r => r.ReviewNotes).HasMaxLength(OdometerReading.NotesMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Vehicle).WithMany().HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChecklistExecution>().WithMany().HasForeignKey(r => r.ChecklistExecutionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.VehicleId, r.ReadAt });
        builder.HasIndex(r => new { r.CompanyId, r.Status });
    }
}

internal sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("DocumentTypes");
        builder.Property(t => t.Name).HasMaxLength(DocumentType.NameMaxLength).IsRequired();
        builder.Property(t => t.OwnerType).HasMaxLength(20);
        builder.HasOne<Company>().WithMany().HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.CompanyId, t.OwnerType, t.Name }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
    }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents", t => t.HasCheckConstraint("CK_Documents_Owner",
            // Exactly the owner FK that matches OwnerType (company documents have none).
            "([OwnerType] = 'Vehicle' AND [VehicleId] IS NOT NULL AND [DriverId] IS NULL AND [ImplementId] IS NULL) OR " +
            "([OwnerType] = 'Driver' AND [DriverId] IS NOT NULL AND [VehicleId] IS NULL AND [ImplementId] IS NULL) OR " +
            "([OwnerType] = 'Implement' AND [ImplementId] IS NOT NULL AND [VehicleId] IS NULL AND [DriverId] IS NULL) OR " +
            "([OwnerType] = 'Company' AND [VehicleId] IS NULL AND [DriverId] IS NULL AND [ImplementId] IS NULL)"));
        builder.Property(d => d.OwnerType).HasMaxLength(20);
        builder.Property(d => d.Number).HasMaxLength(Document.NumberMaxLength);
        builder.Property(d => d.Notes).HasMaxLength(Document.NotesMaxLength);
        builder.Property(d => d.LastAlertedStatus).HasMaxLength(20);
        builder.Ignore(d => d.OwnerId);

        builder.HasOne<Company>().WithMany().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.DocumentType).WithMany().HasForeignKey(d => d.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Vehicle).WithMany().HasForeignKey(d => d.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Driver).WithMany().HasForeignKey(d => d.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Implement).WithMany().HasForeignKey(d => d.ImplementId).OnDelete(DeleteBehavior.Restrict);

        // Alerts and status filters: "AlertStartsOn <= today" (dashboard, scanner) and due-date sorting.
        builder.HasIndex(d => new { d.CompanyId, d.AlertStartsOn });
        builder.HasIndex(d => new { d.CompanyId, d.ExpiresOn });
        builder.HasIndex(d => new { d.CompanyId, d.VehicleId });
        builder.HasIndex(d => new { d.CompanyId, d.DriverId });
        builder.HasIndex(d => new { d.CompanyId, d.ImplementId });
        builder.HasIndex(d => new { d.CompanyId, d.DocumentTypeId });
    }
}

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("StoredFiles");
        builder.Property(f => f.FileName).HasMaxLength(StoredFile.FileNameMaxLength).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(f => f.StorageKey).HasMaxLength(StoredFile.StorageKeyMaxLength).IsUnicode(false).IsRequired();
        builder.Property(f => f.OwnerType).HasMaxLength(30);
        builder.HasOne<Company>().WithMany().HasForeignKey(f => f.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(f => f.StorageKey).IsUnique();
        builder.HasIndex(f => new { f.CompanyId, f.OwnerType, f.OwnerId });
    }
}

internal sealed class ChecklistTemplateConfiguration : IEntityTypeConfiguration<ChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplate> builder)
    {
        builder.ToTable("ChecklistTemplates");
        builder.Property(t => t.Name).HasMaxLength(ChecklistTemplate.NameMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(ChecklistTemplate.DescriptionMaxLength);
        builder.Property(t => t.Frequency).HasMaxLength(20);
        builder.HasOne<Company>().WithMany().HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(t => t.Items).WithOne().HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => new { t.CompanyId, t.Name }).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
    }
}

internal sealed class ChecklistTemplateItemConfiguration : IEntityTypeConfiguration<ChecklistTemplateItem>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplateItem> builder)
    {
        builder.ToTable("ChecklistTemplateItems");
        builder.Property(i => i.Section).HasMaxLength(ChecklistTemplateItem.SectionMaxLength);
        builder.Property(i => i.Label).HasMaxLength(ChecklistTemplateItem.LabelMaxLength).IsRequired();
        builder.Property(i => i.Unit).HasMaxLength(ChecklistTemplateItem.UnitMaxLength);
        builder.Property(i => i.ResponseType).HasMaxLength(20);
        builder.Property(i => i.FailureSeverity).HasMaxLength(20);
        builder.HasIndex(i => new { i.TemplateId, i.Position });
    }
}

internal sealed class ChecklistExecutionConfiguration : IEntityTypeConfiguration<ChecklistExecution>
{
    public void Configure(EntityTypeBuilder<ChecklistExecution> builder)
    {
        builder.ToTable("ChecklistExecutions");
        builder.Property(e => e.TemplateName).HasMaxLength(ChecklistTemplate.NameMaxLength).IsRequired();
        builder.Property(e => e.Frequency).HasMaxLength(20);
        builder.Property(e => e.Result).HasMaxLength(20);
        builder.Property(e => e.Location).HasMaxLength(ChecklistExecution.LocationMaxLength);
        builder.Property(e => e.Notes).HasMaxLength(ChecklistExecution.NotesMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Vehicle).WithMany().HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Driver).WithMany().HasForeignKey(e => e.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChecklistTemplate>().WithMany().HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Answers).WithOne().HasForeignKey(a => a.ExecutionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.CompanyId, e.VehicleId, e.PerformedAt });
        builder.HasIndex(e => new { e.CompanyId, e.DriverId, e.PerformedAt });
        // "Pending checklists": executions of a template in the current day/week.
        builder.HasIndex(e => new { e.CompanyId, e.TemplateId, e.PerformedOn });
        builder.HasIndex(e => new { e.CompanyId, e.PerformedAt });
    }
}

internal sealed class ChecklistAnswerConfiguration : IEntityTypeConfiguration<ChecklistAnswer>
{
    public void Configure(EntityTypeBuilder<ChecklistAnswer> builder)
    {
        builder.ToTable("ChecklistAnswers");
        builder.Property(a => a.Section).HasMaxLength(ChecklistTemplateItem.SectionMaxLength);
        builder.Property(a => a.Label).HasMaxLength(ChecklistTemplateItem.LabelMaxLength).IsRequired();
        builder.Property(a => a.Unit).HasMaxLength(ChecklistTemplateItem.UnitMaxLength);
        builder.Property(a => a.ResponseType).HasMaxLength(20);
        builder.Property(a => a.Choice).HasMaxLength(20);
        builder.Property(a => a.Severity).HasMaxLength(20);
        builder.Property(a => a.NumberValue).HasPrecision(12, 2);
        builder.Property(a => a.TextValue).HasMaxLength(ChecklistAnswer.TextMaxLength);
        builder.Property(a => a.Comment).HasMaxLength(ChecklistAnswer.CommentMaxLength);
        builder.Ignore(a => a.IsFailure);
        builder.Ignore(a => a.IsAnswered);
        builder.HasOne<Occurrence>().WithMany().HasForeignKey(a => a.OccurrenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.ExecutionId, a.Position });
    }
}

internal sealed class OccurrenceConfiguration : IEntityTypeConfiguration<Occurrence>
{
    public void Configure(EntityTypeBuilder<Occurrence> builder)
    {
        builder.ToTable("Occurrences");
        builder.Property(o => o.Severity).HasMaxLength(20);
        builder.Property(o => o.Status).HasMaxLength(20);
        builder.Property(o => o.Source).HasMaxLength(20);
        builder.Property(o => o.Description).HasMaxLength(Occurrence.DescriptionMaxLength).IsRequired();
        builder.Property(o => o.Location).HasMaxLength(Occurrence.LocationMaxLength);
        builder.Property(o => o.Resolution).HasMaxLength(Occurrence.ResolutionMaxLength);
        builder.Ignore(o => o.IsClosed);

        builder.HasOne<Company>().WithMany().HasForeignKey(o => o.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Vehicle).WithMany().HasForeignKey(o => o.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Driver).WithMany().HasForeignKey(o => o.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Implement).WithMany().HasForeignKey(o => o.ImplementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChecklistExecution>().WithMany().HasForeignKey(o => o.ChecklistExecutionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.CompanyId, o.Status, o.OccurredAt });
        builder.HasIndex(o => new { o.CompanyId, o.VehicleId, o.OccurredAt });
        builder.HasIndex(o => new { o.CompanyId, o.DriverId, o.OccurredAt });
        builder.HasIndex(o => new { o.CompanyId, o.OccurredAt });
    }
}

internal sealed class OperationalEventConfiguration : IEntityTypeConfiguration<OperationalEvent>
{
    public void Configure(EntityTypeBuilder<OperationalEvent> builder)
    {
        builder.ToTable("OperationalEvents");
        builder.Property(e => e.Type).HasMaxLength(40);
        builder.Property(e => e.SubjectType).HasMaxLength(OperationalEvent.SubjectTypeMaxLength).IsUnicode(false).IsRequired();
        builder.Property(e => e.Summary).HasMaxLength(OperationalEvent.SummaryMaxLength).IsRequired();
        builder.Property(e => e.Data).IsRequired();
        // No foreign keys on purpose (like AuditLogs): the history must survive whatever happens to the records.
        builder.HasIndex(e => new { e.CompanyId, e.VehicleId, e.OccurredAt });
        builder.HasIndex(e => new { e.CompanyId, e.DriverId, e.OccurredAt });
        builder.HasIndex(e => new { e.CompanyId, e.Type, e.OccurredAt });
        // Outbox scan of future notification dispatchers.
        builder.HasIndex(e => e.Id).HasFilter("[PublishedAt] IS NULL").HasDatabaseName("IX_OperationalEvents_Unpublished");
    }
}
