using Microsoft.EntityFrameworkCore;
using REC4.Domain.Entities;

namespace REC4.Application.Common;

// Unica fonte da regra "de onde vem a permissao efetiva de um usuario": do conjunto
// personalizado (UsuarioPermissao) quando TemPermissaoPersonalizada, senao do Grupo.
// Usado por AuthService (login) e UsuarioService (GetMe/detalhe) para nunca divergir.
public static class PermissaoEfetivaResolver
{
    public static async Task<List<string>> ResolveAsync(IRec4DbContext db, Usuario usuario, CancellationToken cancellationToken = default)
    {
        if (usuario.TemPermissaoPersonalizada)
        {
            return await db.UsuarioPermissoes
                .Where(up => up.UsuarioId == usuario.Id)
                .Select(up => up.Permissao.Chave)
                .ToListAsync(cancellationToken);
        }

        return await db.GrupoPermissoes
            .Where(gp => gp.GrupoId == usuario.GrupoId)
            .Select(gp => gp.Permissao.Chave)
            .ToListAsync(cancellationToken);
    }
}
