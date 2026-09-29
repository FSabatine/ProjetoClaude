using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data.Configurations;

public class UsuarioPermissaoConfiguration : IEntityTypeConfiguration<UsuarioPermissao>
{
    public void Configure(EntityTypeBuilder<UsuarioPermissao> builder)
    {
        builder.ToTable("UsuarioPermissoes");
        builder.HasKey(up => new { up.UsuarioId, up.PermissaoId });

        builder.HasOne(up => up.Usuario)
            .WithMany(u => u.UsuarioPermissoes)
            .HasForeignKey(up => up.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(up => up.Permissao)
            .WithMany()
            .HasForeignKey(up => up.PermissaoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
