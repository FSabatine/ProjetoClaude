using FluentValidation;
using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Usuarios.Dtos;

namespace REC4.Application.Usuarios.Validators;

public class UsuarioCreateDtoValidator : AbstractValidator<UsuarioCreateDto>
{
    // TODO — REGRA DE NEGÓCIO A CONFIRMAR: política de complexidade de senha ainda não foi definida
    // pelo cliente; usando apenas um tamanho mínimo como placeholder reversível.
    public UsuarioCreateDtoValidator(IRec4DbContext db)
    {
        RuleFor(u => u.Nome).NotEmpty().MaximumLength(200);
        RuleFor(u => u.Login).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(u => u.Senha).NotEmpty().MinimumLength(8);

        RuleFor(u => u.GrupoId)
            .MustAsync((grupoId, cancellationToken) => db.Grupos.AnyAsync(g => g.Id == grupoId, cancellationToken))
            .WithMessage("Grupo informado não existe.");

        RuleFor(u => u.TipoUsuario).IsInEnum();

        RuleFor(u => u.PessoaId)
            .MustAsync((pessoaId, cancellationToken) => db.Pessoas.AnyAsync(p => p.Id == pessoaId, cancellationToken))
            .When(u => u.PessoaId.HasValue)
            .WithMessage("Pessoa informada não existe.");

        RuleFor(u => u.LimiteDiasEdicaoFinanceiro)
            .GreaterThanOrEqualTo(0)
            .When(u => u.LimiteDiasEdicaoFinanceiro.HasValue);
    }
}
