using REC4.Application.Grupos.Dtos;

namespace REC4.Application.Grupos;

public interface IGrupoService
{
    Task<IReadOnlyList<GrupoDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<GrupoDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<GrupoDetailDto> CreateAsync(GrupoCreateDto dto, CancellationToken cancellationToken = default);
    Task<GrupoDetailDto> UpdateAsync(int id, GrupoUpdateDto dto, CancellationToken cancellationToken = default);
}
