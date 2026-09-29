using FluentValidation;
using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Grupos.Dtos;

namespace REC4.Application.Grupos.Validators;

public class GrupoCreateDtoValidator : AbstractValidator<GrupoCreateDto>
{
    public GrupoCreateDtoValidator(IRec4DbContext db)
    {
        RuleFor(g => g.Nome).NotEmpty().MaximumLength(100);

        RuleForEach(g => g.PermissaoIds)
            .MustAsync((permissaoId, cancellationToken) => db.Permissoes.AnyAsync(p => p.Id == permissaoId, cancellationToken))
            .WithMessage("Permissão informada não existe.");
    }
}
