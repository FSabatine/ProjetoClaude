using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;
using REC4.Domain.Seed;

namespace REC4.Infrastructure.Data.Configurations;

public class GrupoPermissaoConfiguration : IEntityTypeConfiguration<GrupoPermissao>
{
    public void Configure(EntityTypeBuilder<GrupoPermissao> builder)
    {
        builder.ToTable("GrupoPermissoes");
        builder.HasKey(gp => new { gp.GrupoId, gp.PermissaoId });

        builder.HasOne(gp => gp.Grupo)
            .WithMany(g => g.GrupoPermissoes)
            .HasForeignKey(gp => gp.GrupoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gp => gp.Permissao)
            .WithMany(p => p.GrupoPermissoes)
            .HasForeignKey(gp => gp.PermissaoId)
            .OnDelete(DeleteBehavior.Restrict);

        var grupoIds = GruposPadrao.Nomes
            .Select((nome, index) => (nome, id: index + 1))
            .ToDictionary(x => x.nome, x => x.id);

        var permissaoIds = PermissoesPadrao.Catalogo
            .Select((permissao, index) => (permissao.Chave, id: index + 1))
            .ToDictionary(x => x.Chave, x => x.id);

        var seed = GruposPadrao.PermissoesPorGrupo
            .SelectMany(kvp => kvp.Value.Select(chave => new GrupoPermissao
            {
                GrupoId = grupoIds[kvp.Key],
                PermissaoId = permissaoIds[chave]
            }));

        builder.HasData(seed);
    }
}
