using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data.Configurations;

public class UsuarioEmitenteConfiguration : IEntityTypeConfiguration<UsuarioEmitente>
{
    public void Configure(EntityTypeBuilder<UsuarioEmitente> builder)
    {
        builder.ToTable("UsuarioEmitentes");
        builder.HasKey(ue => new { ue.UsuarioId, ue.PessoaId });

        builder.HasOne(ue => ue.Usuario)
            .WithMany(u => u.UsuarioEmitentes)
            .HasForeignKey(ue => ue.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ue => ue.Pessoa)
            .WithMany()
            .HasForeignKey(ue => ue.PessoaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Acompanha o filtro global de Pessoa (soft delete), mesmo padrao de ContaBancaria/Endereco/PessoaPerfil.
        builder.HasQueryFilter(ue => ue.Pessoa.IsActive);
    }
}
