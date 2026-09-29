using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using REC4.Application.Common;
using REC4.Domain.Entities;
using REC4.Infrastructure.Security;
using Xunit;

namespace REC4.Application.Tests.Security;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "REC4.Tests",
            Audience = "REC4.Tests",
            SigningKey = "chave-de-teste-com-tamanho-suficiente-para-hmac-sha256",
            ExpiryMinutes = 60
        });
        _sut = new JwtTokenService(options);
    }

    [Fact]
    public void GenerateToken_ContemClaimsEsperadas()
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Fulano de Tal",
            Login = "fulano@rec4.local",
            Grupo = new Grupo { Id = 1, Nome = "Controladoria" }
        };

        var resultado = _sut.GenerateToken(usuario, new[] { "Pessoa.Visualizar", "Usuario.Editar" });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(resultado.Token);

        token.Claims.Should().ContainSingle(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == usuario.Id.ToString());
        token.Claims.Should().ContainSingle(c => c.Type == Rec4Claims.Grupo && c.Value == "Controladoria");
        token.Claims.Where(c => c.Type == Rec4Claims.Permission).Select(c => c.Value)
            .Should().BeEquivalentTo("Pessoa.Visualizar", "Usuario.Editar");
        resultado.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromSeconds(5));
    }
}
