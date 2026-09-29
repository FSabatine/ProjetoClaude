using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;
using REC4.Domain.Seed;

namespace REC4.Infrastructure.Data.Configurations;

public class GrupoConfiguration : IEntityTypeConfiguration<Grupo>
{
    public void Configure(EntityTypeBuilder<Grupo> builder)
    {
        builder.ToTable("Grupos");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Nome).HasMaxLength(100).IsRequired();
        builder.HasIndex(g => g.Nome).IsUnique();

        builder.HasData(GruposPadrao.Nomes
            .Select((nome, index) => new Grupo { Id = index + 1, Nome = nome }));
    }
}
