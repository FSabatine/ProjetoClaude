using Fleet.Domain.Companies;
using Fleet.Domain.Intelligence;
using Fleet.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

// Final phase — alerts and automation (ADR-045). CompanyId-first indexes; FKs Restrict.

internal sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("AutomationRules");
        builder.Property(r => r.Name).HasMaxLength(AutomationRule.NameMaxLength);
        builder.Property(r => r.Description).HasMaxLength(AutomationRule.DescriptionMaxLength);
        builder.Property(r => r.Threshold).HasPrecision(10, 2);
        builder.HasOne<Company>().WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.NotifyUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.CompanyId, r.IsActive });
    }
}

internal sealed class AutomationExecutionConfiguration : IEntityTypeConfiguration<AutomationExecution>
{
    public void Configure(EntityTypeBuilder<AutomationExecution> builder)
    {
        builder.ToTable("AutomationExecutions");
        builder.Property(e => e.Error).HasMaxLength(AutomationExecution.ErrorMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AutomationRule>().WithMany().HasForeignKey(e => e.AutomationRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CompanyId, e.AutomationRuleId, e.StartedAt });
        builder.HasIndex(e => new { e.CompanyId, e.FinishedAt });
    }
}

internal sealed class FleetAlertConfiguration : IEntityTypeConfiguration<FleetAlert>
{
    public const string OpenFilter = "[Status] IN ('New', 'Read', 'InProgress')";

    public void Configure(EntityTypeBuilder<FleetAlert> builder)
    {
        builder.ToTable("FleetAlerts");
        builder.Property(a => a.Title).HasMaxLength(FleetAlert.TitleMaxLength);
        builder.Property(a => a.Explanation).HasMaxLength(FleetAlert.TextMaxLength);
        builder.Property(a => a.Evidence).HasMaxLength(FleetAlert.TextMaxLength);
        builder.Property(a => a.RecommendedAction).HasMaxLength(FleetAlert.ActionMaxLength);
        builder.Property(a => a.EntityType).HasMaxLength(FleetAlert.EntityTypeMaxLength);
        builder.Property(a => a.Tab).HasMaxLength(FleetAlert.TabMaxLength);
        builder.Property(a => a.DedupKey).HasMaxLength(FleetAlert.DedupKeyMaxLength);
        builder.Property(a => a.ClosingNotes).HasMaxLength(FleetAlert.NotesMaxLength);

        builder.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AutomationRule).WithMany().HasForeignKey(a => a.AutomationRuleId).OnDelete(DeleteBehavior.Restrict);

        // One open alert per finding: two overlapping scans cannot duplicate it (ADR-045).
        builder.HasIndex(a => new { a.CompanyId, a.AutomationRuleId, a.DedupKey })
            .IsUnique().HasFilter(OpenFilter).HasDatabaseName("UX_FleetAlerts_OpenFinding");
        builder.HasIndex(a => new { a.CompanyId, a.Status, a.Priority });
        builder.HasIndex(a => new { a.CompanyId, a.VehicleId, a.Status });
        builder.HasIndex(a => new { a.CompanyId, a.AutomationRuleId, a.DedupKey, a.Status });
    }
}

internal sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");
        builder.Property(n => n.Title).HasMaxLength(UserNotification.TitleMaxLength);
        builder.Property(n => n.Message).HasMaxLength(UserNotification.MessageMaxLength);
        builder.Property(n => n.Link).HasMaxLength(UserNotification.LinkMaxLength);
        builder.HasOne<Company>().WithMany().HasForeignKey(n => n.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FleetAlert>().WithMany().HasForeignKey(n => n.FleetAlertId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => new { n.CompanyId, n.UserId, n.ReadAt, n.CreatedAt });
    }
}
