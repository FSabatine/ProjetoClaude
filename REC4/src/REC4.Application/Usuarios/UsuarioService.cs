using Microsoft.EntityFrameworkCore;
using REC4.Application.Common;
using REC4.Application.Exceptions;
using REC4.Application.Usuarios.Dtos;
using REC4.Domain.Entities;

namespace REC4.Application.Usuarios;

public class UsuarioService : IUsuarioService
{
    private readonly IRec4DbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public UsuarioService(IRec4DbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<UsuarioResumoDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var usuarios = await _db.Usuarios
            .Include(u => u.Grupo)
            .OrderBy(u => u.Nome)
            .ToListAsync(cancellationToken);

        return usuarios.Select(u => u.ToResumoDto()).ToList();
    }

    public async Task<UsuarioDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usuario = await LoadUsuarioAsync(id, cancellationToken);
        return await ToDetailDtoAsync(usuario, cancellationToken);
    }

    public async Task<UsuarioDto> CreateAsync(UsuarioCreateDto dto, CancellationToken cancellationToken = default)
    {
        var login = dto.Login.Trim();

        var jaExiste = await _db.Usuarios.AnyAsync(u => u.Login == login, cancellationToken);
        if (jaExiste)
            throw new DuplicateLoginException(login);

        var now = DateTime.UtcNow;
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = dto.Nome,
            Login = login,
            SenhaHash = _passwordHasher.Hash(dto.Senha),
            GrupoId = dto.GrupoId,
            PessoaId = dto.PessoaId,
            TipoUsuario = dto.TipoUsuario,
            LimiteDiasEdicaoFinanceiro = dto.LimiteDiasEdicaoFinanceiro,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(usuario.Id, cancellationToken);
    }

    public async Task<UsuarioDto> UpdateAsync(Guid id, UsuarioUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var usuario = await LoadUsuarioAsync(id, cancellationToken);

        usuario.Nome = dto.Nome;
        usuario.GrupoId = dto.GrupoId;
        usuario.IsActive = dto.IsActive;
        usuario.PessoaId = dto.PessoaId;
        usuario.TipoUsuario = dto.TipoUsuario;
        usuario.LimiteDiasEdicaoFinanceiro = dto.LimiteDiasEdicaoFinanceiro;
        usuario.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(usuario.Id, cancellationToken);
    }

    public async Task<MeuUsuarioDto> GetMeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usuario = await LoadUsuarioAsync(id, cancellationToken);
        var permissoes = await PermissaoEfetivaResolver.ResolveAsync(_db, usuario, cancellationToken);

        return new MeuUsuarioDto(usuario.Id, usuario.Nome, usuario.Login, usuario.Grupo.Nome, usuario.IsActive, permissoes);
    }

    public async Task<UsuarioDto> SetPermissoesAsync(Guid id, UsuarioPermissoesUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var usuario = await LoadUsuarioAsync(id, cancellationToken);

        var atuais = await _db.UsuarioPermissoes.Where(up => up.UsuarioId == id).ToListAsync(cancellationToken);
        foreach (var up in atuais)
            _db.UsuarioPermissoes.Remove(up);

        if (dto.Personalizado)
        {
            foreach (var permissaoId in dto.PermissaoIds.Distinct())
                _db.UsuarioPermissoes.Add(new UsuarioPermissao { UsuarioId = id, PermissaoId = permissaoId });
        }

        usuario.TemPermissaoPersonalizada = dto.Personalizado;
        usuario.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UsuarioDto> AddEscritorioAsync(Guid id, UsuarioEscritorioCreateDto dto, CancellationToken cancellationToken = default)
    {
        var usuario = await LoadUsuarioWithVinculosAsync(id, cancellationToken);
        var tornaPrincipal = dto.IsPrincipal || usuario.UsuarioEscritorios.Count == 0;
        var existentesPrincipais = usuario.UsuarioEscritorios.Where(ue => ue.IsPrincipal).ToList();

        var vinculo = new UsuarioEscritorio { UsuarioId = id, EscritorioId = dto.EscritorioId, IsPrincipal = tornaPrincipal };

        await PrincipalSwapHelper.SaveAsync(
            _db,
            needsSwap: tornaPrincipal && existentesPrincipais.Count > 0,
            clearOld: () => existentesPrincipais.ForEach(ue => ue.IsPrincipal = false),
            applyNew: () => usuario.UsuarioEscritorios.Add(vinculo),
            cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UsuarioDto> SetEscritorioPrincipalAsync(Guid id, Guid escritorioId, CancellationToken cancellationToken = default)
    {
        var usuario = await LoadUsuarioWithVinculosAsync(id, cancellationToken);
        var alvo = usuario.UsuarioEscritorios.FirstOrDefault(ue => ue.EscritorioId == escritorioId)
            ?? throw new NotFoundException(nameof(UsuarioEscritorio), escritorioId);
        var outrasPrincipais = usuario.UsuarioEscritorios.Where(ue => ue.EscritorioId != escritorioId && ue.IsPrincipal).ToList();

        await PrincipalSwapHelper.SaveAsync(
            _db,
            needsSwap: outrasPrincipais.Count > 0,
            clearOld: () => outrasPrincipais.ForEach(ue => ue.IsPrincipal = false),
            applyNew: () => alvo.IsPrincipal = true,
            cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UsuarioDto> RemoveEscritorioAsync(Guid id, Guid escritorioId, CancellationToken cancellationToken = default)
    {
        var usuario = await LoadUsuarioWithVinculosAsync(id, cancellationToken);
        var alvo = usuario.UsuarioEscritorios.FirstOrDefault(ue => ue.EscritorioId == escritorioId)
            ?? throw new NotFoundException(nameof(UsuarioEscritorio), escritorioId);

        _db.UsuarioEscritorios.Remove(alvo);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UsuarioDto> AddEmitenteAsync(Guid id, UsuarioEmitenteCreateDto dto, CancellationToken cancellationToken = default)
    {
        var usuario = await LoadUsuarioWithVinculosAsync(id, cancellationToken);
        var jaVinculado = usuario.UsuarioEmitentes.Any(ue => ue.PessoaId == dto.PessoaId);
        if (!jaVinculado)
        {
            usuario.UsuarioEmitentes.Add(new UsuarioEmitente { UsuarioId = id, PessoaId = dto.PessoaId });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<UsuarioDto> RemoveEmitenteAsync(Guid id, Guid pessoaId, CancellationToken cancellationToken = default)
    {
        var usuario = await LoadUsuarioWithVinculosAsync(id, cancellationToken);
        var alvo = usuario.UsuarioEmitentes.FirstOrDefault(ue => ue.PessoaId == pessoaId)
            ?? throw new NotFoundException(nameof(UsuarioEmitente), pessoaId);

        _db.UsuarioEmitentes.Remove(alvo);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private async Task<Usuario> LoadUsuarioAsync(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Grupo)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return usuario ?? throw new NotFoundException(nameof(Usuario), id);
    }

    private async Task<Usuario> LoadUsuarioWithVinculosAsync(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Grupo)
            .Include(u => u.UsuarioEscritorios).ThenInclude(ue => ue.Escritorio)
            .Include(u => u.UsuarioEmitentes).ThenInclude(ue => ue.Pessoa)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return usuario ?? throw new NotFoundException(nameof(Usuario), id);
    }

    private async Task<UsuarioDto> ToDetailDtoAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        var pessoaNome = usuario.PessoaId is null
            ? null
            : await _db.Pessoas.Where(p => p.Id == usuario.PessoaId).Select(p => p.Nome).FirstOrDefaultAsync(cancellationToken);

        var permissoesEfetivas = await PermissaoEfetivaResolver.ResolveAsync(_db, usuario, cancellationToken);

        var escritorios = await _db.UsuarioEscritorios
            .Where(ue => ue.UsuarioId == usuario.Id)
            .OrderBy(ue => ue.Escritorio.Nome)
            .Select(ue => new UsuarioEscritorioDto(ue.EscritorioId, ue.Escritorio.Nome, ue.IsPrincipal))
            .ToListAsync(cancellationToken);

        var emitentes = await _db.UsuarioEmitentes
            .Where(ue => ue.UsuarioId == usuario.Id)
            .OrderBy(ue => ue.Pessoa.Nome)
            .Select(ue => new UsuarioEmitenteDto(ue.PessoaId, ue.Pessoa.Nome))
            .ToListAsync(cancellationToken);

        return new UsuarioDto(
            usuario.Id, usuario.Nome, usuario.Login, usuario.GrupoId, usuario.Grupo.Nome, usuario.IsActive,
            usuario.PessoaId, pessoaNome, usuario.TipoUsuario, usuario.LimiteDiasEdicaoFinanceiro,
            usuario.TemPermissaoPersonalizada, permissoesEfetivas, escritorios, emitentes,
            usuario.CreatedAt, usuario.UpdatedAt);
    }
}
