using FluentValidation;
using REC4.Application.Escritorios.Dtos;

namespace REC4.Application.Escritorios.Validators;

public class EscritorioUpdateDtoValidator : AbstractValidator<EscritorioUpdateDto>
{
    public EscritorioUpdateDtoValidator()
    {
        RuleFor(e => e.Nome).NotEmpty().MaximumLength(150);
    }
}
