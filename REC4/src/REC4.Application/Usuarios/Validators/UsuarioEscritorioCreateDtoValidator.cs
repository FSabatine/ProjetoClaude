using FluentValidation;
using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Usuarios.Dtos;

namespace REC4.Application.Usuarios.Validators;

public class UsuarioEscritorioCreateDtoValidator : AbstractValidator<UsuarioEscritorioCreateDto>
{
    public UsuarioEscritorioCreateDtoValidator(IRec4DbContext db)
    {
        RuleFor(u => u.EscritorioId)
            .MustAsync((escritorioId, cancellationToken) => db.Escritorios.AnyAsync(e => e.Id == escritorioId, cancellationToken))
            .WithMessage("Escritório informado não existe.");
    }
}
