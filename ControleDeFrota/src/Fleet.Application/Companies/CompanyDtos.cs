using Fleet.Application.Common;
using Fleet.Domain.Companies;
using FluentValidation;

namespace Fleet.Application.Companies;

public sealed class CompanyListRequest : ListRequest
{
    public bool? IsActive { get; set; }
}

public sealed record CompanyRequest
{
    public string? LegalName { get; init; }
    public string? TradeName { get; init; }
    public string? Cnpj { get; init; }
    public string? StateRegistration { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public AddressDto Address { get; init; } = new();
    public bool IsActive { get; init; } = true;
}

public sealed record CompanyListItemResponse(
    Guid Id,
    string LegalName,
    string? TradeName,
    string Cnpj,
    string? City,
    string? State,
    bool IsActive,
    int UserCount);

public sealed record CompanyResponse(
    Guid Id,
    string LegalName,
    string? TradeName,
    string Cnpj,
    string? StateRegistration,
    string? Email,
    string? Phone,
    AddressDto Address,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class CompanyRequestValidator : AbstractValidator<CompanyRequest>
{
    public CompanyRequestValidator()
    {
        RuleFor(x => x.LegalName).Required("Razão social").MaxLen(Company.LegalNameMaxLength);
        RuleFor(x => x.TradeName).MaxLen(Company.TradeNameMaxLength);
        RuleFor(x => x.Cnpj).Required("CNPJ").ValidCnpj();
        RuleFor(x => x.StateRegistration).MaxLen(Company.StateRegistrationMaxLength);
        RuleFor(x => x.Email).ValidEmail().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).ValidPhone().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Address).NotNull().SetValidator(new AddressValidator(required: true));
    }
}
