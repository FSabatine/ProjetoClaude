using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Data.Configurations;

public class ContaBancariaConfiguration : IEntityTypeConfiguration<ContaBancaria>
{
    public void Configure(EntityTypeBuilder<ContaBancaria> builder)
    {
        builder.ToTable("ContasBancarias");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Banco).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Agencia).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Conta).HasMaxLength(30).IsRequired();
        builder.Property(c => c.DigitoConta).HasMaxLength(5);
        builder.Property(c => c.TipoConta).HasMaxLength(30).IsRequired();

        // Secao 7 do spec: apenas uma conta principal por Pessoa.
        builder.HasIndex(c => c.PessoaId)
            .IsUnique()
            .HasFilter("[IsPrincipal] = 1")
            .HasDatabaseName("IX_ContasBancarias_PessoaId_Principal");

        // Acompanha o filtro global de Pessoa (soft delete) para evitar vazar contas
        // de pessoas inativas em consultas feitas diretamente neste DbSet.
        builder.HasQueryFilter(c => c.Pessoa.IsActive);
    }
}
