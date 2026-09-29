using FluentAssertions;
using Fleet.Application.Auth;
using Fleet.Application.Common;
using Fleet.Application.Tests.TestSupport;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;
using Fleet.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Auth;

public class AuthServiceTests : IDisposable
{
    private const string Password = "SenhaCorreta123";
    private readonly TestDb _t = new();
    private Company _company = null!;
    private User _user = null!;

    public void Dispose() => _t.Dispose();

    private async Task<AuthService> ArrangeAsync(string roleKey = SystemRoles.FleetManager)
    {
        _company = await _t.AddCompanyAsync();
        _user = await _t.AddUserAsync(_company, "gestor@empresa.com", roleKey, Services.Hasher.Hash(Password));
        _t.CurrentUser.Anonymous();
        return Services.Auth(_t);
    }

    private static LoginRequest Login(string password = Password, string email = "Gestor@Empresa.com") =>
        new() { Email = email, Password = password };

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokensAndEffectivePermissions()
    {
        var auth = await ArrangeAsync();

        var result = await auth.LoginAsync(Login(), "127.0.0.1", default);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User.Permissions.Should().BeEquivalentTo(SystemRoles.All.Single(r => r.Key == SystemRoles.FleetManager).Permissions);
        var stored = await _t.NewContext().RefreshTokens.SingleAsync();
        stored.TokenHash.Should().NotBe(result.RefreshToken, "only the hash is persisted");
        (await _t.NewContext().Users.SingleAsync()).LastLoginAt.Should().Be(_t.Clock.UtcNow);
    }

    [Theory]
    [InlineData("gestor@empresa.com", "SenhaErrada123")]
    [InlineData("ninguem@empresa.com", Password)]
    public async Task LoginAsync_WrongEmailOrPassword_ThrowsSameGenericMessage(string email, string password)
    {
        var auth = await ArrangeAsync();

        var act = () => auth.LoginAsync(Login(password, email), null, default);

        (await act.Should().ThrowAsync<AuthenticationFailedException>()).Which.Message.Should().StartWith("E-mail ou senha inválidos");
    }

    [Fact]
    public async Task LoginAsync_EmptyFields_ThrowsValidation()
    {
        var auth = await ArrangeAsync();

        await auth.Invoking(a => a.LoginAsync(new LoginRequest(), null, default)).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task LoginAsync_FiveFailures_LocksAccountEvenForCorrectPassword()
    {
        var auth = await ArrangeAsync();
        for (var i = 0; i < 5; i++)
            await auth.Invoking(a => a.LoginAsync(Login("errada1234"), null, default)).Should().ThrowAsync<AuthenticationFailedException>();

        var act = () => auth.LoginAsync(Login(), null, default);

        (await act.Should().ThrowAsync<AuthenticationFailedException>()).Which.Message.Should().Contain("bloqueado");

        _t.Clock.UtcNow = _t.Clock.UtcNow.AddMinutes(16);
        await auth.Invoking(a => a.LoginAsync(Login(), null, default)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_IsRejected()
    {
        var auth = await ArrangeAsync();
        _user.Status = UserStatus.Inactive;
        await _t.Db.SaveChangesAsync();

        (await auth.Invoking(a => a.LoginAsync(Login(), null, default)).Should().ThrowAsync<AuthenticationFailedException>())
            .Which.Message.Should().Contain("inativo");
    }

    [Fact]
    public async Task LoginAsync_InactiveCompany_IsRejected()
    {
        var auth = await ArrangeAsync();
        _company.IsActive = false;
        await _t.Db.SaveChangesAsync();

        (await auth.Invoking(a => a.LoginAsync(Login(), null, default)).Should().ThrowAsync<AuthenticationFailedException>())
            .Which.Message.Should().Contain("empresa");
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesToken()
    {
        var auth = await ArrangeAsync();
        var login = await auth.LoginAsync(Login(), null, default);

        var refreshed = await auth.RefreshAsync(login.RefreshToken, null, default);

        refreshed.RefreshToken.Should().NotBe(login.RefreshToken);
        await auth.Invoking(a => a.RefreshAsync(refreshed.RefreshToken, null, default)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshAsync_ReusedRotatedToken_RevokesAllSessions()
    {
        var auth = await ArrangeAsync();
        var login = await auth.LoginAsync(Login(), null, default);
        var refreshed = await auth.RefreshAsync(login.RefreshToken, null, default);

        // Attacker replays the old token.
        await auth.Invoking(a => a.RefreshAsync(login.RefreshToken, null, default)).Should().ThrowAsync<AuthenticationFailedException>();

        // The legitimate (newest) session was killed too.
        await auth.Invoking(a => a.RefreshAsync(refreshed.RefreshToken, null, default)).Should().ThrowAsync<AuthenticationFailedException>();
        (await _t.NewContext().RefreshTokens.AllAsync(t => t.RevokedAt != null)).Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_IsRejected()
    {
        var auth = await ArrangeAsync();
        var login = await auth.LoginAsync(Login(), null, default);
        _t.Clock.UtcNow = _t.Clock.UtcNow.AddDays(8);

        await auth.Invoking(a => a.RefreshAsync(login.RefreshToken, null, default)).Should().ThrowAsync<AuthenticationFailedException>();
    }

    [Fact]
    public async Task RefreshAsync_UserDeactivatedAfterLogin_IsRejected()
    {
        var auth = await ArrangeAsync();
        var login = await auth.LoginAsync(Login(), null, default);
        _user.Status = UserStatus.Inactive;
        await _t.Db.SaveChangesAsync();

        await auth.Invoking(a => a.RefreshAsync(login.RefreshToken, null, default)).Should().ThrowAsync<AuthenticationFailedException>();
    }

    [Fact]
    public async Task LogoutAsync_RevokesToken()
    {
        var auth = await ArrangeAsync();
        var login = await auth.LoginAsync(Login(), null, default);

        await auth.LogoutAsync(login.RefreshToken, default);

        await auth.Invoking(a => a.RefreshAsync(login.RefreshToken, null, default)).Should().ThrowAsync<AuthenticationFailedException>();
    }

    [Fact]
    public async Task ChangePasswordAsync_KeepsCurrentSessionAndRevokesOthers()
    {
        var auth = await ArrangeAsync();
        var current = await auth.LoginAsync(Login(), null, default);
        var otherDevice = await auth.LoginAsync(Login(), null, default);
        _t.SignInAs(_company, SystemRoles.FleetManager, _user.Id);

        await auth.ChangePasswordAsync(new ChangePasswordRequest { CurrentPassword = Password, NewPassword = "NovaSenha2026" }, current.RefreshToken, default);

        _t.CurrentUser.Anonymous();
        await auth.Invoking(a => a.RefreshAsync(current.RefreshToken, null, default)).Should().NotThrowAsync();
        await auth.Invoking(a => a.RefreshAsync(otherDevice.RefreshToken, null, default)).Should().ThrowAsync<AuthenticationFailedException>();
        await auth.Invoking(a => a.LoginAsync(Login("NovaSenha2026"), null, default)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsValidationOnField()
    {
        var auth = await ArrangeAsync();
        _t.SignInAs(_company, SystemRoles.FleetManager, _user.Id);

        var act = () => auth.ChangePasswordAsync(new ChangePasswordRequest { CurrentPassword = "errada1234", NewPassword = "NovaSenha2026" }, null, default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "CurrentPassword");
    }
}
