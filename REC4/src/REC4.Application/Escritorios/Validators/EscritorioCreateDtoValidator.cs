using FluentValidation;
using REC4.Application.Escritorios.Dtos;

namespace REC4.Application.Escritorios.Validators;

public class EscritorioCreateDtoValidator : AbstractValidator<EscritorioCreateDto>
{
    public EscritorioCreateDtoValidator()
    {
        RuleFor(e => e.Nome).NotEmpty().MaximumLength(150);
    }
}
