using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Escritorios.Dtos;
using REC4.Application.Exceptions;
using REC4.Domain.Entities;

namespace REC4.Application.Escritorios;

public class EscritorioService : IEscritorioService
{
    private readonly IRec4DbContext _db;

    public EscritorioService(IRec4DbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EscritorioDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Escritorios
            .OrderBy(e => e.Nome)
            .Select(e => new EscritorioDto(e.Id, e.Nome, e.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<EscritorioDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var escritorio = await LoadAsync(id, cancellationToken);
        return escritorio.ToDto();
    }

    public async Task<EscritorioDto> CreateAsync(EscritorioCreateDto dto, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var escritorio = new Escritorio
        {
            Id = Guid.NewGuid(),
            Nome = dto.Nome,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Escritorios.Add(escritorio);
        await _db.SaveChangesAsync(cancellationToken);
        return escritorio.ToDto();
    }

    public async Task<EscritorioDto> UpdateAsync(Guid id, EscritorioUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var escritorio = await LoadAsync(id, cancellationToken);
        escritorio.Nome = dto.Nome;
        escritorio.IsActive = dto.IsActive;
        escritorio.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return escritorio.ToDto();
    }

    private async Task<Escritorio> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var escritorio = await _db.Escritorios.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return escritorio ?? throw new NotFoundException(nameof(Escritorio), id);
    }
}

internal static class EscritorioMappings
{
    public static EscritorioDto ToDto(this Escritorio escritorio) => new(escritorio.Id, escritorio.Nome, escritorio.IsActive);
}
