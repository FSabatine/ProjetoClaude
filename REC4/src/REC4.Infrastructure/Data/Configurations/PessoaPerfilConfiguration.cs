using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data.Configurations;

public class PessoaPerfilConfiguration : IEntityTypeConfiguration<PessoaPerfil>
{
    public void Configure(EntityTypeBuilder<PessoaPerfil> builder)
    {
        builder.ToTable("PessoaPerfis");
        builder.HasKey(pp => new { pp.PessoaId, pp.PerfilId });

        builder.HasOne(pp => pp.Pessoa)
            .WithMany(p => p.PessoaPerfis)
            .HasForeignKey(pp => pp.PessoaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pp => pp.Perfil)
            .WithMany(p => p.PessoaPerfis)
            .HasForeignKey(pp => pp.PerfilId)
            .OnDelete(DeleteBehavior.Restrict);

        // Acompanha o filtro global de Pessoa (soft delete) para evitar vazar vinculos
        // de pessoas inativas em consultas feitas diretamente neste DbSet.
        builder.HasQueryFilter(pp => pp.Pessoa.IsActive);
    }
}
