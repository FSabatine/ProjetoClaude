using REC4.Application.Usuarios.Dtos;
using REC4.Domain.Entities;

namespace REC4.Application.Usuarios;

internal static class UsuarioMappings
{
    public static UsuarioResumoDto ToResumoDto(this Usuario usuario) =>
        new(usuario.Id, usuario.Nome, usuario.Login, usuario.Grupo.Nome, usuario.IsActive);
}
