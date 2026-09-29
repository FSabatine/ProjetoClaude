using FluentValidation;
using REC4.Application.Pessoas.Dtos;
using REC4.Application.Validation;
using REC4.Domain.Enums;

namespace REC4.Application.Pessoas.Validators;

public class PessoaCreateDtoValidator : AbstractValidator<PessoaCreateDto>
{
    public PessoaCreateDtoValidator()
    {
        RuleFor(p => p.TipoPessoa).IsInEnum();

        RuleFor(p => p.Nome)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(p => p.CpfCnpj)
            .NotEmpty()
            .Must((dto, cpfCnpj) => DocumentValidator.IsValid(cpfCnpj, dto.TipoPessoa))
            .WithMessage(dto => dto.TipoPessoa == TipoPessoa.Fisica
                ? "CPF inválido."
                : "CNPJ inválido.");

        RuleFor(p => p.UFRG)
            .Length(2)
            .When(p => !string.IsNullOrEmpty(p.UFRG));
    }
}
