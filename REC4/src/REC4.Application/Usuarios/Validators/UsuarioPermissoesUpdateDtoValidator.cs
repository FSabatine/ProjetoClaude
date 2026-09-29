using FluentValidation;
using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Usuarios.Dtos;

namespace REC4.Application.Usuarios.Validators;

public class UsuarioPermissoesUpdateDtoValidator : AbstractValidator<UsuarioPermissoesUpdateDto>
{
    public UsuarioPermissoesUpdateDtoValidator(IRec4DbContext db)
    {
        RuleForEach(u => u.PermissaoIds)
            .MustAsync((permissaoId, cancellationToken) => db.Permissoes.AnyAsync(p => p.Id == permissaoId, cancellationToken))
            .WithMessage("Permissão informada não existe.");
    }
}
