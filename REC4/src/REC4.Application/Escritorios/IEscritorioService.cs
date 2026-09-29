using REC4.Application.Escritorios.Dtos;

namespace REC4.Application.Escritorios;

public interface IEscritorioService
{
    Task<IReadOnlyList<EscritorioDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<EscritorioDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EscritorioDto> CreateAsync(EscritorioCreateDto dto, CancellationToken cancellationToken = default);
    Task<EscritorioDto> UpdateAsync(Guid id, EscritorioUpdateDto dto, CancellationToken cancellationToken = default);
}
