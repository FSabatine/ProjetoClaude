using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Nome).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Login).HasMaxLength(200).IsRequired();
        builder.Property(u => u.SenhaHash).IsRequired();
        builder.Property(u => u.TipoUsuario).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(u => u.Login).IsUnique();

        builder.HasOne(u => u.Grupo)
            .WithMany(g => g.Usuarios)
            .HasForeignKey(u => u.GrupoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Pessoa)
            .WithMany()
            .HasForeignKey(u => u.PessoaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
