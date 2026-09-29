using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;
using REC4.Domain.Seed;

namespace REC4.Infrastructure.Data.Configurations;

public class PermissaoConfiguration : IEntityTypeConfiguration<Permissao>
{
    public void Configure(EntityTypeBuilder<Permissao> builder)
    {
        builder.ToTable("Permissoes");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Chave).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Descricao).HasMaxLength(300);
        builder.HasIndex(p => p.Chave).IsUnique();

        builder.HasData(PermissoesPadrao.Catalogo
            .Select((permissao, index) => new Permissao { Id = index + 1, Chave = permissao.Chave, Descricao = permissao.Descricao }));
    }
}
