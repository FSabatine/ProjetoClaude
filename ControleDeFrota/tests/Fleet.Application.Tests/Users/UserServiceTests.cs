using System.Text.Json;
using FluentAssertions;
using Fleet.Application.Common;
using Fleet.Application.Tests.TestSupport;
using Fleet.Application.Users;
using Fleet.Domain.Authorization;
using Fleet.Domain.Companies;
using Fleet.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Application.Tests.Users;

public class UserServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    private Company _company = null!;

    public void Dispose() => _t.Dispose();

    private static int RoleId(string key) => SystemRoles.All.Single(r => r.Key == key).Id;

    private static UserCreateRequest NewUser(string email = "novo@empresa.com", params string[] roles) => new()
    {
        Name = "Novo Usuário",
        Email = email,
        Password = "SenhaSegura123",
        RoleIds = (roles.Length == 0 ? [SystemRoles.Viewer] : roles).Select(RoleId).ToList(),
    };

    private async Task<(UserService Service, User Admin)> AsAdministratorAsync()
    {
        _company = await _t.AddCompanyAsync();
        var admin = await _t.AddUserAsync(_company, "admin@empresa.com", SystemRoles.Administrator);
        _t.SignInAs(_company, SystemRoles.Administrator, admin.Id);
        return (Services.Users(_t), admin);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_HashesPasswordAndNormalizesEmail()
    {
        var (service, _) = await AsAdministratorAsync();

        var created = await service.CreateAsync(NewUser(" Novo@Empresa.COM "), default);

        created.Email.Should().Be("novo@empresa.com");
        created.CompanyId.Should().Be(_company.Id);
        var stored = await _t.NewContext().Users.SingleAsync(u => u.Id == created.Id);
        stored.PasswordHash.Should().NotBe("SenhaSegura123");
        Services.Hasher.Verify(stored.PasswordHash, "SenhaSegura123").Should().Be(PasswordCheck.Success);
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmailDifferentCase_ThrowsConflict()
    {
        var (service, _) = await AsAdministratorAsync();

        var act = () => service.CreateAsync(NewUser("ADMIN@empresa.com"), default);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Field.Should().Be("email");
    }

    [Theory]
    [InlineData("curta1")]          // too short
    [InlineData("somenteletras")]   // no digits
    [InlineData("novo12345678")]    // contains e-mail local part
    public async Task CreateAsync_WeakPassword_ThrowsValidation(string password)
    {
        var (service, _) = await AsAdministratorAsync();

        var act = () => service.CreateAsync(NewUser() with { Password = password }, default);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task CreateAsync_RoleWithPermissionsCallerLacks_ThrowsForbidden()
    {
        var (service, _) = await AsAdministratorAsync();

        var act = () => service.CreateAsync(NewUser(roles: SystemRoles.PlatformAdministrator), default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_InAnotherCompanyWithoutPlatformPermission_ThrowsForbidden()
    {
        var (service, _) = await AsAdministratorAsync();
        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");

        var act = () => service.CreateAsync(NewUser() with { CompanyId = other.Id }, default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateAsync_InAnotherCompanyAsPlatformAdmin_Succeeds()
    {
        _company = await _t.AddCompanyAsync();
        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        _t.SignInAs(_company, SystemRoles.PlatformAdministrator);

        var created = await Services.Users(_t).CreateAsync(NewUser(roles: SystemRoles.Administrator) with { CompanyId = other.Id }, default);

        created.CompanyId.Should().Be(other.Id);
    }

    [Fact]
    public async Task UpdateAsync_DeactivatingSelf_ThrowsBusinessRule()
    {
        var (service, admin) = await AsAdministratorAsync();
        var request = new UserUpdateRequest
        {
            Name = admin.Name, Email = admin.Email, Status = UserStatus.Inactive, RoleIds = [RoleId(SystemRoles.Administrator)],
        };

        await service.Invoking(s => s.UpdateAsync(admin.Id, request, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UpdateAsync_ChangingOwnRoles_ThrowsBusinessRule()
    {
        var (service, admin) = await AsAdministratorAsync();
        var request = new UserUpdateRequest { Name = admin.Name, Email = admin.Email, RoleIds = [RoleId(SystemRoles.Viewer)] };

        await service.Invoking(s => s.UpdateAsync(admin.Id, request, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UpdateAsync_TargetHasMorePermissionsThanCaller_ThrowsForbidden()
    {
        var (service, _) = await AsAdministratorAsync();
        var platformAdmin = await _t.AddUserAsync(_company, "root@empresa.com", SystemRoles.PlatformAdministrator);
        var request = new UserUpdateRequest
        {
            Name = "x", Email = platformAdmin.Email, Status = UserStatus.Inactive, RoleIds = [RoleId(SystemRoles.PlatformAdministrator)],
        };

        await service.Invoking(s => s.UpdateAsync(platformAdmin.Id, request, default)).Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAsync_RoleChange_IsAuditedAndDeactivationRevokesSessions()
    {
        var (service, _) = await AsAdministratorAsync();
        var created = await service.CreateAsync(NewUser(), default);
        _t.Db.RefreshTokens.Add(new RefreshToken { UserId = created.Id, TokenHash = new string('A', 64), CreatedAt = _t.Clock.UtcNow, ExpiresAt = _t.Clock.UtcNow.AddDays(1) });
        await _t.Db.SaveChangesAsync();

        await service.UpdateAsync(created.Id, new UserUpdateRequest
        {
            Name = created.Name, Email = created.Email, Status = UserStatus.Inactive,
            RoleIds = [RoleId(SystemRoles.Operations)],
        }, default);

        var ctx = _t.NewContext();
        (await ctx.RefreshTokens.SingleAsync()).RevokedAt.Should().NotBeNull();
        var log = await ctx.AuditLogs.Where(a => a.EntityId == created.Id.ToString()).OrderBy(a => a.Id).LastAsync();
        var changes = JsonDocument.Parse(log.Changes).RootElement;
        changes.GetProperty("RoleIds").GetProperty("new")[0].GetInt32().Should().Be(RoleId(SystemRoles.Operations));
        changes.GetProperty("Status").GetProperty("new").GetString().Should().Be("Inactive");
    }

    [Fact]
    public async Task ListAsync_OnlyReturnsUsersOfOwnCompany()
    {
        var (service, _) = await AsAdministratorAsync();
        var other = await _t.AddCompanyAsync("12ABC34501DE35", "Outra");
        var foreign = await _t.AddUserAsync(other, "user@outra.com", SystemRoles.Viewer);

        var result = await service.ListAsync(new UserListRequest(), default);

        result.Items.Should().ContainSingle().Which.Email.Should().Be("admin@empresa.com");
        await service.Invoking(s => s.GetAsync(foreign.Id, default)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_Self_ThrowsBusinessRule()
    {
        var (service, admin) = await AsAdministratorAsync();

        await service.Invoking(s => s.DeleteAsync(admin.Id, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesKeepsRolesAndFreesEmail()
    {
        var (service, _) = await AsAdministratorAsync();
        var created = await service.CreateAsync(NewUser(), default);

        await service.DeleteAsync(created.Id, default);

        var ctx = _t.NewContext();
        (await ctx.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == created.Id)).DeletedAt.Should().NotBeNull();
        (await ctx.UserRoles.IgnoreQueryFilters().CountAsync(ur => ur.UserId == created.Id)).Should().Be(1);
        await service.Invoking(s => s.CreateAsync(NewUser(), default)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_UnlocksAccountAndAuditsWithoutRevealingHash()
    {
        var (service, _) = await AsAdministratorAsync();
        var created = await service.CreateAsync(NewUser(), default);
        var user = await _t.Db.Users.SingleAsync(u => u.Id == created.Id);
        user.LockoutEndAt = _t.Clock.UtcNow.AddMinutes(10);
        await _t.Db.SaveChangesAsync();

        await service.ResetPasswordAsync(created.Id, new ResetPasswordRequest { NewPassword = "OutraSenha456" }, default);

        var ctx = _t.NewContext();
        (await ctx.Users.SingleAsync(u => u.Id == created.Id)).LockoutEndAt.Should().BeNull();
        var log = await ctx.AuditLogs.Where(a => a.EntityId == created.Id.ToString()).OrderBy(a => a.Id).LastAsync();
        log.Changes.Should().Contain("\"PasswordHash\":{\"old\":\"***\",\"new\":\"***\"}");
    }
}
