using FluentValidation;
using REC4.Application.Pessoas.Dtos;

namespace REC4.Application.Pessoas.Validators;

public class PessoaUpdateDtoValidator : AbstractValidator<PessoaUpdateDto>
{
    public PessoaUpdateDtoValidator()
    {
        RuleFor(p => p.Nome)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(p => p.UFRG)
            .Length(2)
            .When(p => !string.IsNullOrEmpty(p.UFRG));
    }
}
