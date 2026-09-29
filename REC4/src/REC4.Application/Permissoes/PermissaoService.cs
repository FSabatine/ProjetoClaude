using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Permissoes.Dtos;

namespace REC4.Application.Permissoes;

public class PermissaoService : IPermissaoService
{
    private readonly IRec4DbContext _db;

    public PermissaoService(IRec4DbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PermissaoDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Permissoes
            .OrderBy(p => p.Chave)
            .Select(p => new PermissaoDto(p.Id, p.Chave, p.Descricao))
            .ToListAsync(cancellationToken);
    }
}
