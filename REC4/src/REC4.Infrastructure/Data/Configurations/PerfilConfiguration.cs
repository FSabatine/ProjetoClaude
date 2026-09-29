using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;
using REC4.Domain.Seed;

namespace REC4.Infrastructure.Data.Configurations;

public class PerfilConfiguration : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> builder)
    {
        builder.ToTable("Perfis");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nome).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => p.Nome).IsUnique();

        builder.HasData(PerfisPadrao.Nomes
            .Select((nome, index) => new Perfil { Id = index + 1, Nome = nome }));
    }
}
