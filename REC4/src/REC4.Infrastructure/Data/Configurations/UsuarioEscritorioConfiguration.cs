using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data.Configurations;

public class UsuarioEscritorioConfiguration : IEntityTypeConfiguration<UsuarioEscritorio>
{
    public void Configure(EntityTypeBuilder<UsuarioEscritorio> builder)
    {
        builder.ToTable("UsuarioEscritorios");
        builder.HasKey(ue => new { ue.UsuarioId, ue.EscritorioId });

        builder.HasOne(ue => ue.Usuario)
            .WithMany(u => u.UsuarioEscritorios)
            .HasForeignKey(ue => ue.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ue => ue.Escritorio)
            .WithMany(e => e.UsuarioEscritorios)
            .HasForeignKey(ue => ue.EscritorioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Secao "Vinculos" do cadastro de usuario: apenas um escritorio principal por usuario.
        builder.HasIndex(ue => ue.UsuarioId)
            .IsUnique()
            .HasFilter("[IsPrincipal] = 1")
            .HasDatabaseName("IX_UsuarioEscritorios_UsuarioId_Principal");
    }
}
