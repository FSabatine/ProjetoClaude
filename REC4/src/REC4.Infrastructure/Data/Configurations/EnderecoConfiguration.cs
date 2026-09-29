using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data.Configurations;

public class EnderecoConfiguration : IEntityTypeConfiguration<Endereco>
{
    public void Configure(EntityTypeBuilder<Endereco> builder)
    {
        builder.ToTable("Enderecos");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Logradouro).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Numero).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Complemento).HasMaxLength(100);
        builder.Property(e => e.Bairro).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Cidade).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UF).HasMaxLength(2).IsRequired();
        builder.Property(e => e.CEP).HasMaxLength(9).IsRequired();
        builder.Property(e => e.Pais).HasMaxLength(60).IsRequired();

        // Secao 8 do spec: apenas um endereco principal por Pessoa.
        builder.HasIndex(e => e.PessoaId)
            .IsUnique()
            .HasFilter("[IsPrincipal] = 1")
            .HasDatabaseName("IX_Enderecos_PessoaId_Principal");

        // Acompanha o filtro global de Pessoa (soft delete) para evitar vazar enderecos
        // de pessoas inativas em consultas feitas diretamente neste DbSet.
        builder.HasQueryFilter(e => e.Pessoa.IsActive);
    }
}
