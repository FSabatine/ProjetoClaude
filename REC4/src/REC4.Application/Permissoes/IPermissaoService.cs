using REC4.Application.Permissoes.Dtos;

namespace REC4.Application.Permissoes;

public interface IPermissaoService
{
    Task<IReadOnlyList<PermissaoDto>> ListAsync(CancellationToken cancellationToken = default);
}
