using Fleet.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fleet.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    /// <summary>Unique indexes must ignore soft-deleted rows, so a deleted plate/CPF can be registered again (ADR-011).</summary>
    public const string NotDeletedFilter = "[DeletedAt] IS NULL";

    public static void ConfigureAddress<T>(this EntityTypeBuilder<T> builder) where T : class
    {
        builder.OwnsOne(typeof(Address), "Address", address =>
        {
            address.Property(nameof(Address.Street)).HasColumnName("Street").HasMaxLength(Address.StreetMaxLength);
            address.Property(nameof(Address.Number)).HasColumnName("Number").HasMaxLength(Address.NumberMaxLength);
            address.Property(nameof(Address.Complement)).HasColumnName("Complement").HasMaxLength(Address.ComplementMaxLength);
            address.Property(nameof(Address.Neighborhood)).HasColumnName("Neighborhood").HasMaxLength(Address.NeighborhoodMaxLength);
            address.Property(nameof(Address.City)).HasColumnName("City").HasMaxLength(Address.CityMaxLength);
            address.Property(nameof(Address.State)).HasColumnName("State").HasMaxLength(2).IsFixedLength().IsUnicode(false);
            address.Property(nameof(Address.ZipCode)).HasColumnName("ZipCode").HasMaxLength(8).IsFixedLength().IsUnicode(false);
        });
        builder.Navigation("Address").IsRequired();
    }

    public static PropertyBuilder<string> IsFixedCode(this PropertyBuilder<string> property, int length) =>
        property.HasMaxLength(length).IsFixedLength().IsUnicode(false);
}
