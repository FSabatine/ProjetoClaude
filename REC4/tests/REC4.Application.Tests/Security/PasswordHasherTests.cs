using FluentAssertions;
using REC4.Infrastructure.Security;
using Xunit;

namespace REC4.Application.Tests.Security;

public class PasswordHasherTests
{
    private readonly Rec4PasswordHasher _sut = new();

    [Fact]
    public void Hash_DepoisVerify_ComSenhaCorreta_RetornaTrue()
    {
        var hash = _sut.Hash("Rec4!Teste123");

        _sut.Verify(hash, "Rec4!Teste123").Should().BeTrue();
    }

    [Fact]
    public void Verify_ComSenhaErrada_RetornaFalse()
    {
        var hash = _sut.Hash("Rec4!Teste123");

        _sut.Verify(hash, "SenhaErrada").Should().BeFalse();
    }

    [Fact]
    public void Hash_MesmaSenhaDuasVezes_GeraHashesDiferentes()
    {
        var hash1 = _sut.Hash("Rec4!Teste123");
        var hash2 = _sut.Hash("Rec4!Teste123");

        hash1.Should().NotBe(hash2);
    }
}
