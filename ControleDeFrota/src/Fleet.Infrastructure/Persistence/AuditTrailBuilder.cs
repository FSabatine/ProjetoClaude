using System.Text.Json;
using System.Text.Json.Serialization;
using Fleet.Domain.Auditing;
using Fleet.Domain.Common;
using Fleet.Domain.Companies;
using Fleet.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Fleet.Infrastructure.Persistence;

/// <summary>
/// ADR-012: stamps CreatedAt/UpdatedAt and turns the pending changes of IAuditable entities into AuditLog rows
/// (who, what, when, old → new), saved in the same transaction as the change itself.
/// </summary>
internal sealed class AuditTrailBuilder(ChangeTracker changeTracker, DateTime now, Guid? userId, string? traceId)
{
    private const string Masked = "***";

    /// <summary>Bookkeeping or operational noise — never counts as a business change.</summary>
    private static readonly HashSet<string> IgnoredProperties =
    [
        nameof(AuditableEntity.Id),
        nameof(AuditableEntity.CreatedAt), nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.UpdatedAt), nameof(AuditableEntity.UpdatedBy),
        nameof(ISoftDeletable.DeletedBy),
        nameof(User.FailedLoginCount), nameof(User.LockoutEndAt), nameof(User.LastLoginAt),
    ];

    private static readonly HashSet<string> SensitiveProperties = [nameof(User.PasswordHash)];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public List<AuditLog> Build()
    {
        var logs = new List<AuditLog>();
        foreach (var entry in changeTracker.Entries<AuditableEntity>().ToList())
        {
            var changes = CollectChanges(entry);
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = userId;
            }
            else if (changes.Count > 0)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userId;
            }
            else
            {
                continue;
            }

            if (entry.Entity is IAuditable) logs.Add(CreateLog(entry, changes));
        }
        return logs;
    }

    private Dictionary<string, object?> CollectChanges(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();
        AddPropertyChanges(entry, prefix: null, changes);

        // Owned types (Address) are tracked as separate entries, so an address-only edit leaves the owner Unchanged.
        foreach (var reference in entry.References.Where(r => r.TargetEntry?.Metadata.IsOwned() == true))
            AddPropertyChanges(reference.TargetEntry!, reference.Metadata.Name, changes);

        if (entry.Entity is User user) AddRoleChanges(user, changes);
        return changes;
    }

    /// <summary>Role assignment is a security-relevant change even though no column of Users changes.</summary>
    private void AddRoleChanges(User user, Dictionary<string, object?> changes)
    {
        var entries = changeTracker.Entries<UserRole>().Where(e => e.Entity.UserId == user.Id).ToList();
        var before = entries.Where(e => e.State is not EntityState.Added).Select(e => e.Entity.RoleId).Order().ToList();
        var after = entries.Where(e => e.State is not EntityState.Deleted).Select(e => e.Entity.RoleId).Order().ToList();
        if (before.SequenceEqual(after)) return;

        var isAdded = changeTracker.Entries<User>().Any(e => e.Entity == user && e.State == EntityState.Added);
        changes["RoleIds"] = new { old = isAdded ? null : before, @new = after };
    }

    private static void AddPropertyChanges(EntityEntry entry, string? prefix, Dictionary<string, object?> changes)
    {
        var isAdded = entry.State == EntityState.Added;
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (IgnoredProperties.Contains(name) || property.Metadata.IsShadowProperty()) continue;

            var oldValue = isAdded ? null : property.OriginalValue;
            var newValue = property.CurrentValue;
            if (!isAdded && Equals(oldValue, newValue)) continue;
            if (isAdded && newValue is null) continue;

            var sensitive = SensitiveProperties.Contains(name);
            changes[prefix is null ? name : $"{prefix}.{name}"] = new
            {
                old = sensitive && oldValue is not null ? Masked : oldValue,
                @new = sensitive && newValue is not null ? Masked : newValue,
            };
        }
    }

    private AuditLog CreateLog(EntityEntry<AuditableEntity> entry, Dictionary<string, object?> changes)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            _ when entry.Entity is ISoftDeletable { DeletedAt: not null } && changes.ContainsKey(nameof(ISoftDeletable.DeletedAt))
                => AuditAction.Deleted,
            _ => AuditAction.Updated,
        };

        return new AuditLog
        {
            CompanyId = entry.Entity switch
            {
                ITenantScoped tenant => tenant.CompanyId,
                Company company => company.Id,
                User user => user.CompanyId,
                _ => null,
            },
            UserId = userId,
            EntityName = entry.Metadata.ClrType.Name,
            EntityId = entry.Entity.Id.ToString(),
            Action = action,
            Changes = JsonSerializer.Serialize(changes, JsonOptions),
            OccurredAt = now,
            TraceId = traceId,
        };
    }
}
