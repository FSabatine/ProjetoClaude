using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data.Configurations;

public class PessoaConfiguration : IEntityTypeConfiguration<Pessoa>
{
    public void Configure(EntityTypeBuilder<Pessoa> builder)
    {
        builder.ToTable("Pessoas");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.CpfCnpj).HasMaxLength(14).IsRequired();
        builder.Property(p => p.Nome).HasMaxLength(200).IsRequired();
        builder.Property(p => p.TipoPessoa).HasConversion<string>().HasMaxLength(20);

        // "Impedir cadastro de nova Pessoa com mesmo CPF/CNPJ de outra Pessoa ativa" (spec secao 3):
        // unicidade avaliada apenas entre registros ativos, para permitir reativacao futura do documento.
        builder.HasIndex(p => p.CpfCnpj)
            .IsUnique()
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_Pessoas_CpfCnpj_Ativas");

        builder.HasQueryFilter(p => p.IsActive);

        builder.HasMany(p => p.Enderecos)
            .WithOne(e => e.Pessoa)
            .HasForeignKey(e => e.PessoaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ContasBancarias)
            .WithOne(c => c.Pessoa)
            .HasForeignKey(c => c.PessoaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
