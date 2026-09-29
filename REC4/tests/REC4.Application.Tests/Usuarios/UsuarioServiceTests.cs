using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using REC4.Application.Exceptions;
using REC4.Application.Usuarios;
using REC4.Application.Usuarios.Dtos;
using REC4.Domain.Enums;
using REC4.Domain.Seed;
using REC4.Infrastructure.Data;
using REC4.Infrastructure.Security;
using Xunit;

namespace REC4.Application.Tests.Usuarios;

public class UsuarioServiceTests : IDisposable
{
    private readonly Rec4DbContext _db;
    private readonly Rec4PasswordHasher _passwordHasher = new();
    private readonly UsuarioService _sut;

    public UsuarioServiceTests()
    {
        _db = TestDbContextFactory.CreateInMemory();
        _sut = new UsuarioService(_db, _passwordHasher);
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> GrupoIdAsync(string nome) =>
        await _db.Grupos.Where(g => g.Nome == nome).Select(g => g.Id).SingleAsync();

    private static UsuarioCreateDto NovoUsuario(string login, int grupoId) =>
        new("Fulano", login, "Rec4!Teste123", grupoId, PessoaId: null, TipoUsuario.Operador, LimiteDiasEdicaoFinanceiro: null);

    [Fact]
    public async Task CreateAsync_NuncaArmazenaSenhaEmTextoPuro()
    {
        var grupoId = await GrupoIdAsync(GruposPadrao.Financeiro);

        var usuario = await _sut.CreateAsync(NovoUsuario("fulano@rec4.local", grupoId));

        var senhaHashPersistida = await _db.Usuarios.Where(u => u.Id == usuario.Id).Select(u => u.SenhaHash).SingleAsync();
        senhaHashPersistida.Should().NotBe("Rec4!Teste123");
        _passwordHasher.Verify(senhaHashPersistida, "Rec4!Teste123").Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ComLoginDuplicado_LancaDuplicateLoginException()
    {
        var grupoId = await GrupoIdAsync(GruposPadrao.Financeiro);
        await _sut.CreateAsync(NovoUsuario("fulano@rec4.local", grupoId));

        var act = () => _sut.CreateAsync(NovoUsuario("fulano@rec4.local", grupoId));

        await act.Should().ThrowAsync<DuplicateLoginException>();
    }

    [Fact]
    public async Task UpdateAsync_AlteraGrupoEStatus()
    {
        var grupoFinanceiroId = await GrupoIdAsync(GruposPadrao.Financeiro);
        var grupoLogisticaId = await GrupoIdAsync(GruposPadrao.Logistica);
        var usuario = await _sut.CreateAsync(NovoUsuario("fulano@rec4.local", grupoFinanceiroId));

        var atualizado = await _sut.UpdateAsync(usuario.Id,
            new UsuarioUpdateDto("Fulano Editado", grupoLogisticaId, false, PessoaId: null, TipoUsuario.Gestor, LimiteDiasEdicaoFinanceiro: 5));

        atualizado.Nome.Should().Be("Fulano Editado");
        atualizado.GrupoId.Should().Be(grupoLogisticaId);
        atualizado.IsActive.Should().BeFalse();
        atualizado.TipoUsuario.Should().Be(TipoUsuario.Gestor);
        atualizado.LimiteDiasEdicaoFinanceiro.Should().Be(5);
    }

    [Fact]
    public async Task SetPermissoesAsync_Personalizado_SubstituiPermissoesDoGrupo()
    {
        var grupoId = await GrupoIdAsync(GruposPadrao.Financeiro);
        var usuario = await _sut.CreateAsync(NovoUsuario("fulano@rec4.local", grupoId));
        usuario.PermissoesEfetivas.Should().Contain("Financeiro.Visualizar"); // vem do grupo por padrao

        var pessoaVisualizarId = await _db.Permissoes.Where(p => p.Chave == "Pessoa.Visualizar").Select(p => p.Id).SingleAsync();
        var personalizado = await _sut.SetPermissoesAsync(usuario.Id, new UsuarioPermissoesUpdateDto(true, new[] { pessoaVisualizarId }));

        personalizado.TemPermissaoPersonalizada.Should().BeTrue();
        personalizado.PermissoesEfetivas.Should().BeEquivalentTo(new[] { "Pessoa.Visualizar" });
    }

    [Fact]
    public async Task SetPermissoesAsync_Restaurar_VoltaParaPermissoesDoGrupo()
    {
        var grupoId = await GrupoIdAsync(GruposPadrao.Financeiro);
        var usuario = await _sut.CreateAsync(NovoUsuario("fulano@rec4.local", grupoId));
        var pessoaVisualizarId = await _db.Permissoes.Where(p => p.Chave == "Pessoa.Visualizar").Select(p => p.Id).SingleAsync();
        await _sut.SetPermissoesAsync(usuario.Id, new UsuarioPermissoesUpdateDto(true, new[] { pessoaVisualizarId }));

        var restaurado = await _sut.SetPermissoesAsync(usuario.Id, new UsuarioPermissoesUpdateDto(false, Array.Empty<int>()));

        restaurado.TemPermissaoPersonalizada.Should().BeFalse();
        restaurado.PermissoesEfetivas.Should().Contain("Financeiro.Visualizar");
        restaurado.PermissoesEfetivas.Should().Contain("Pessoa.Visualizar"); // grupo Financeiro ja tem essa tambem
        restaurado.PermissoesEfetivas.Should().Contain("Financeiro.Criar");
    }
}
