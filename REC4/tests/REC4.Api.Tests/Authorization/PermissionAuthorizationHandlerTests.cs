using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using REC4.Api.Authorization;
using REC4.Application.Common;
using Xunit;

namespace REC4.Api.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private readonly PermissionAuthorizationHandler _sut = new();

    [Fact]
    public async Task HandleRequirementAsync_ComClaimDePermissaoCorrespondente_Sucede()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(Rec4Claims.Permission, "Pessoa.Editar") }, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement("Pessoa.Editar");
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await _sut.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_SemAClaimDePermissao_NaoSucede()
    {
        var identity = new ClaimsIdentity(new[] { new Claim(Rec4Claims.Permission, "Pessoa.Visualizar") }, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement("Pessoa.Editar");
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await _sut.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_UsuarioSemClaims_NaoSucede()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var requirement = new PermissionRequirement("Pessoa.Editar");
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await _sut.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
