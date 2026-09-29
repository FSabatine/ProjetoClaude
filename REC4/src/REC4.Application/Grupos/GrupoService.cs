using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Exceptions;
using REC4.Application.Grupos.Dtos;
using REC4.Application.Permissoes.Dtos;
using REC4.Domain.Entities;

namespace REC4.Application.Grupos;

public class GrupoService : IGrupoService
{
    private readonly IRec4DbContext _db;

    public GrupoService(IRec4DbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<GrupoDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Grupos
            .OrderBy(g => g.Nome)
            .Select(g => new GrupoDto(g.Id, g.Nome))
            .ToListAsync(cancellationToken);
    }

    public async Task<GrupoDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var grupo = await LoadGrupoAsync(id, cancellationToken);
        return await ToDetailDtoAsync(grupo, cancellationToken);
    }

    public async Task<GrupoDetailDto> CreateAsync(GrupoCreateDto dto, CancellationToken cancellationToken = default)
    {
        var nome = dto.Nome.Trim();
        var jaExiste = await _db.Grupos.AnyAsync(g => g.Nome == nome, cancellationToken);
        if (jaExiste)
            throw new DuplicateGroupNameException(nome);

        var grupo = new Grupo { Nome = nome };
        foreach (var permissaoId in dto.PermissaoIds.Distinct())
            grupo.GrupoPermissoes.Add(new GrupoPermissao { PermissaoId = permissaoId });

        _db.Grupos.Add(grupo);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(grupo.Id, cancellationToken);
    }

    public async Task<GrupoDetailDto> UpdateAsync(int id, GrupoUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var grupo = await LoadGrupoAsync(id, cancellationToken);
        var nome = dto.Nome.Trim();

        var duplicado = await _db.Grupos.AnyAsync(g => g.Id != id && g.Nome == nome, cancellationToken);
        if (duplicado)
            throw new DuplicateGroupNameException(nome);

        grupo.Nome = nome;

        var atuais = await _db.GrupoPermissoes.Where(gp => gp.GrupoId == id).ToListAsync(cancellationToken);
        var desejados = dto.PermissaoIds.Distinct().ToHashSet();

        foreach (var gp in atuais.Where(gp => !desejados.Contains(gp.PermissaoId)))
            _db.GrupoPermissoes.Remove(gp);

        var existentes = atuais.Select(gp => gp.PermissaoId).ToHashSet();
        foreach (var permissaoId in desejados.Where(pid => !existentes.Contains(pid)))
            _db.GrupoPermissoes.Add(new GrupoPermissao { GrupoId = id, PermissaoId = permissaoId });

        await _db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private async Task<Grupo> LoadGrupoAsync(int id, CancellationToken cancellationToken)
    {
        var grupo = await _db.Grupos.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        return grupo ?? throw new NotFoundException(nameof(Grupo), id);
    }

    private async Task<GrupoDetailDto> ToDetailDtoAsync(Grupo grupo, CancellationToken cancellationToken)
    {
        var permissoes = await _db.GrupoPermissoes
            .Where(gp => gp.GrupoId == grupo.Id)
            .OrderBy(gp => gp.Permissao.Chave)
            .Select(gp => new PermissaoDto(gp.Permissao.Id, gp.Permissao.Chave, gp.Permissao.Descricao))
            .ToListAsync(cancellationToken);

        return new GrupoDetailDto(grupo.Id, grupo.Nome, permissoes);
    }
}
