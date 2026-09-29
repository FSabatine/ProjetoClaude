using FluentValidation;
using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Usuarios.Dtos;

namespace REC4.Application.Usuarios.Validators;

public class UsuarioEmitenteCreateDtoValidator : AbstractValidator<UsuarioEmitenteCreateDto>
{
    public UsuarioEmitenteCreateDtoValidator(IRec4DbContext db)
    {
        RuleFor(u => u.PessoaId)
            .MustAsync((pessoaId, cancellationToken) => db.Pessoas.AnyAsync(p => p.Id == pessoaId, cancellationToken))
            .WithMessage("Pessoa informada não existe.");
    }
}
