using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using REC4.Application.Escritorios;
using REC4.Application.Escritorios.Dtos;
using REC4.Application.Usuarios;
using REC4.Application.Usuarios.Dtos;
using REC4.Domain.Enums;
using REC4.Domain.Seed;
using REC4.Infrastructure.Data;
using REC4.Infrastructure.Security;
using Xunit;

namespace REC4.Application.Tests.Usuarios;

public class UsuarioEscritorioLinkTests : IDisposable
{
    private readonly Rec4DbContext _db;
    private readonly UsuarioService _usuarioService;
    private readonly EscritorioService _escritorioService;

    public UsuarioEscritorioLinkTests()
    {
        _db = TestDbContextFactory.CreateInMemory();
        _usuarioService = new UsuarioService(_db, new Rec4PasswordHasher());
        _escritorioService = new EscritorioService(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> CriarUsuarioAsync()
    {
        var grupoId = await _db.Grupos.Where(g => g.Nome == GruposPadrao.Financeiro).Select(g => g.Id).SingleAsync();
        var usuario = await _usuarioService.CreateAsync(
            new UsuarioCreateDto("Fulano", "fulano@rec4.local", "Rec4!Teste123", grupoId, null, TipoUsuario.Operador, null));
        return usuario.Id;
    }

    [Fact]
    public async Task AddEscritorioAsync_PrimeiroEscritorio_TornaSePrincipalAutomaticamente()
    {
        var usuarioId = await CriarUsuarioAsync();
        var escritorio = await _escritorioService.CreateAsync(new EscritorioCreateDto("Matriz"));

        var usuario = await _usuarioService.AddEscritorioAsync(usuarioId, new UsuarioEscritorioCreateDto(escritorio.Id, false));

        usuario.Escritorios.Single().IsPrincipal.Should().BeTrue();
    }

    [Fact]
    public async Task AddEscritorioAsync_SegundoComoPrincipal_DesmarcaOAnterior()
    {
        var usuarioId = await CriarUsuarioAsync();
        var matriz = await _escritorioService.CreateAsync(new EscritorioCreateDto("Matriz"));
        var filial = await _escritorioService.CreateAsync(new EscritorioCreateDto("Filial"));

        await _usuarioService.AddEscritorioAsync(usuarioId, new UsuarioEscritorioCreateDto(matriz.Id, true));
        var usuario = await _usuarioService.AddEscritorioAsync(usuarioId, new UsuarioEscritorioCreateDto(filial.Id, true));

        usuario.Escritorios.Single(e => e.Id == matriz.Id).IsPrincipal.Should().BeFalse();
        usuario.Escritorios.Single(e => e.Id == filial.Id).IsPrincipal.Should().BeTrue();
    }

    [Fact]
    public async Task SetEscritorioPrincipalAsync_TrocaOPrincipalDeFormaExclusiva()
    {
        var usuarioId = await CriarUsuarioAsync();
        var matriz = await _escritorioService.CreateAsync(new EscritorioCreateDto("Matriz"));
        var filial = await _escritorioService.CreateAsync(new EscritorioCreateDto("Filial"));
        await _usuarioService.AddEscritorioAsync(usuarioId, new UsuarioEscritorioCreateDto(matriz.Id, true));
        await _usuarioService.AddEscritorioAsync(usuarioId, new UsuarioEscritorioCreateDto(filial.Id, false));

        var usuario = await _usuarioService.SetEscritorioPrincipalAsync(usuarioId, filial.Id);

        usuario.Escritorios.Single(e => e.Id == matriz.Id).IsPrincipal.Should().BeFalse();
        usuario.Escritorios.Single(e => e.Id == filial.Id).IsPrincipal.Should().BeTrue();
    }
}
