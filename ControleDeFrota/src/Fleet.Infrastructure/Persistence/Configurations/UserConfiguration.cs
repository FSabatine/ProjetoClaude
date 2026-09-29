using Fleet.Domain.Authorization;
using Fleet.Domain.Users;
using Fleet.Domain.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.Property(u => u.Name).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(EmailAddress.MaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.Status).HasMaxLength(20);

        builder.HasOne(u => u.Company).WithMany().HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => u.Email).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
        builder.HasIndex(u => u.CompanyId);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });
        builder.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ur => ur.Role).WithMany().HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Restrict);
        // Matches the User soft-delete filter so role assignments of deleted users are hidden too.
        builder.HasQueryFilter(ur => ur.User.DeletedAt == null);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.Property(t => t.TokenHash).IsFixedCode(64).IsRequired();
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false);
        builder.Property(t => t.CreatedByIp).HasMaxLength(45).IsUnicode(false);
        builder.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
        // Sessions of a deleted user are unusable.
        builder.HasQueryFilter(t => t.User.DeletedAt == null);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Key).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(300);
        builder.HasIndex(r => r.Key).IsUnique();

        builder.HasData(SystemRoles.All.Select(r => new Role
        {
            Id = r.Id, Key = r.Key, Name = r.Name, Description = r.Description, IsSystem = true,
        }));
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Key).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(p => p.Module).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(200);
        builder.HasIndex(p => p.Key).IsUnique();

        builder.HasData(PermissionCatalog.All.Select(p => new Permission
        {
            Id = p.Id, Key = p.Key, Module = p.Module, Description = p.Description,
        }));
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        builder.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
        builder.HasOne(rp => rp.Permission).WithMany().HasForeignKey(rp => rp.PermissionId);

        builder.HasData(SystemRoles.All.SelectMany(role => role.Permissions.Select(key => new RolePermission
        {
            RoleId = role.Id, PermissionId = PermissionCatalog.Get(key).Id,
        })));
    }
}
