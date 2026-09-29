using REC4.Application.Usuarios.Dtos;

namespace REC4.Application.Usuarios;

public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioResumoDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<UsuarioDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UsuarioDto> CreateAsync(UsuarioCreateDto dto, CancellationToken cancellationToken = default);
    Task<UsuarioDto> UpdateAsync(Guid id, UsuarioUpdateDto dto, CancellationToken cancellationToken = default);
    Task<MeuUsuarioDto> GetMeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UsuarioDto> SetPermissoesAsync(Guid id, UsuarioPermissoesUpdateDto dto, CancellationToken cancellationToken = default);

    Task<UsuarioDto> AddEscritorioAsync(Guid id, UsuarioEscritorioCreateDto dto, CancellationToken cancellationToken = default);
    Task<UsuarioDto> SetEscritorioPrincipalAsync(Guid id, Guid escritorioId, CancellationToken cancellationToken = default);
    Task<UsuarioDto> RemoveEscritorioAsync(Guid id, Guid escritorioId, CancellationToken cancellationToken = default);

    Task<UsuarioDto> AddEmitenteAsync(Guid id, UsuarioEmitenteCreateDto dto, CancellationToken cancellationToken = default);
    Task<UsuarioDto> RemoveEmitenteAsync(Guid id, Guid pessoaId, CancellationToken cancellationToken = default);
}
