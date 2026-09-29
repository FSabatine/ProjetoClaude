using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data.Configurations;

public class EscritorioConfiguration : IEntityTypeConfiguration<Escritorio>
{
    public void Configure(EntityTypeBuilder<Escritorio> builder)
    {
        builder.ToTable("Escritorios");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Nome).HasMaxLength(150).IsRequired();
    }
}
