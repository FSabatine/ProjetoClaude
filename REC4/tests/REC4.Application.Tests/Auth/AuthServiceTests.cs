using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REC4.Application.Auth;
using REC4.Application.Auth.Dtos;
using REC4.Application.Exceptions;
using REC4.Domain.Entities;
using REC4.Domain.Seed;
using REC4.Infrastructure.Data;
using REC4.Infrastructure.Security;
using Xunit;

namespace REC4.Application.Tests.Auth;

public class AuthServiceTests : IDisposable
{
    private readonly Rec4DbContext _db;
    private readonly Rec4PasswordHasher _passwordHasher = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _db = TestDbContextFactory.CreateInMemory();
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "REC4.Tests",
            Audience = "REC4.Tests",
            SigningKey = "chave-de-teste-com-tamanho-suficiente-para-hmac-sha256",
            ExpiryMinutes = 60
        });
        _sut = new AuthService(_db, _passwordHasher, new JwtTokenService(jwtOptions));
    }

    public void Dispose() => _db.Dispose();

    private async Task<Usuario> CriarUsuarioAsync(string senha, bool isActive = true, string grupo = GruposPadrao.Financeiro)
    {
        var grupoId = await _db.Grupos.Where(g => g.Nome == grupo).Select(g => g.Id).SingleAsync();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Usuário de Teste",
            Login = "teste@rec4.local",
            SenhaHash = _passwordHasher.Hash(senha),
            GrupoId = grupoId,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task LoginAsync_ComCredenciaisValidas_RetornaTokenComPermissoesDoGrupo()
    {
        await CriarUsuarioAsync("Rec4!Teste123", grupo: GruposPadrao.Financeiro);

        var resultado = await _sut.LoginAsync(new LoginRequestDto("teste@rec4.local", "Rec4!Teste123"));

        resultado.Token.Should().NotBeNullOrEmpty();
        resultado.Usuario.Grupo.Should().Be(GruposPadrao.Financeiro);
        resultado.Usuario.Permissoes.Should().Contain("Financeiro.Visualizar");
        resultado.Usuario.Permissoes.Should().Contain("Pessoa.Visualizar");
        resultado.Usuario.Permissoes.Should().NotContain("Pessoa.Editar");
    }

    [Fact]
    public async Task LoginAsync_ComSenhaErrada_LancaInvalidCredentialsException()
    {
        await CriarUsuarioAsync("Rec4!Teste123");

        var act = () => _sut.LoginAsync(new LoginRequestDto("teste@rec4.local", "SenhaErrada"));

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task LoginAsync_ComUsuarioInativo_LancaInvalidCredentialsException()
    {
        await CriarUsuarioAsync("Rec4!Teste123", isActive: false);

        var act = () => _sut.LoginAsync(new LoginRequestDto("teste@rec4.local", "Rec4!Teste123"));

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task LoginAsync_ComLoginInexistente_LancaInvalidCredentialsException()
    {
        var act = () => _sut.LoginAsync(new LoginRequestDto("naoexiste@rec4.local", "qualquer"));

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }
}
