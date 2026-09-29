using Fleet.Domain.Common;
using Fleet.Domain.Validation;
using FluentValidation;

namespace Fleet.Application.Common;

public sealed record AddressDto
{
    public string? Street { get; init; }
    public string? Number { get; init; }
    public string? Complement { get; init; }
    public string? Neighborhood { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? ZipCode { get; init; }

    public static AddressDto From(Address a) => new()
    {
        Street = a.Street,
        Number = a.Number,
        Complement = a.Complement,
        Neighborhood = a.Neighborhood,
        City = a.City,
        State = a.State,
        ZipCode = a.ZipCode,
    };

    public void ApplyTo(Address a)
    {
        a.Street = Street.TrimToNull();
        a.Number = Number.TrimToNull();
        a.Complement = Complement.TrimToNull();
        a.Neighborhood = Neighborhood.TrimToNull();
        a.City = City.TrimToNull();
        a.State = string.IsNullOrWhiteSpace(State) ? null : BrazilianStates.Normalize(State);
        a.ZipCode = string.IsNullOrWhiteSpace(ZipCode) ? null : Fleet.Domain.Validation.ZipCode.Normalize(ZipCode);
    }
}

/// <summary>Child validator composed via SetValidator — internal so assembly scanning doesn't register it.</summary>
internal sealed class AddressValidator : AbstractValidator<AddressDto>
{

    /// <param name="required">Company addresses are mandatory; driver addresses are optional but must be valid when given.</param>
    public AddressValidator(bool required)
    {
        if (required)
        {
            RuleFor(x => x.Street).Required("Logradouro");
            RuleFor(x => x.Number).Required("Número");
            RuleFor(x => x.Neighborhood).Required("Bairro");
            RuleFor(x => x.City).Required("Cidade");
            RuleFor(x => x.State).Required("UF");
            RuleFor(x => x.ZipCode).Required("CEP");
        }

        RuleFor(x => x.Street).MaxLen(Address.StreetMaxLength);
        RuleFor(x => x.Number).MaxLen(Address.NumberMaxLength);
        RuleFor(x => x.Complement).MaxLen(Address.ComplementMaxLength);
        RuleFor(x => x.Neighborhood).MaxLen(Address.NeighborhoodMaxLength);
        RuleFor(x => x.City).MaxLen(Address.CityMaxLength);
        RuleFor(x => x.State).ValidState().When(x => !string.IsNullOrWhiteSpace(x.State));
        RuleFor(x => x.ZipCode).ValidZipCode().When(x => !string.IsNullOrWhiteSpace(x.ZipCode));
    }
}
