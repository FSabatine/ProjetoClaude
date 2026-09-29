using Fleet.Domain.Companies;
using Fleet.Domain.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.Property(c => c.LegalName).HasMaxLength(Company.LegalNameMaxLength).IsRequired();
        builder.Property(c => c.TradeName).HasMaxLength(Company.TradeNameMaxLength);
        builder.Property(c => c.Cnpj).IsFixedCode(Cnpj.Length).IsRequired();
        builder.Property(c => c.StateRegistration).HasMaxLength(Company.StateRegistrationMaxLength);
        builder.Property(c => c.Email).HasMaxLength(EmailAddress.MaxLength);
        builder.Property(c => c.Phone).HasMaxLength(Phone.MaxLength).IsUnicode(false);
        builder.ConfigureAddress();

        builder.HasIndex(c => c.Cnpj).IsUnique().HasFilter(ConfigurationExtensions.NotDeletedFilter);
    }
}
