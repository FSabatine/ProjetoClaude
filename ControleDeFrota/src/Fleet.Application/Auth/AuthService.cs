using Fleet.Application.Common;
using Fleet.Domain.Users;
using Fleet.Domain.Validation;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fleet.Application.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}

public sealed class AuthService(
    IFleetDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IClock clock,
    ICurrentUser currentUser,
    IOptions<AuthOptions> options,
    IValidator<LoginRequest> loginValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    ILogger<AuthService> logger)
{
    private const string InvalidCredentials = "E-mail ou senha inválidos. Confira os dados e tente novamente.";
    private const string SessionExpired = "Sua sessão expirou. Entre novamente para continuar.";

    // Verified when the e-mail doesn't exist, so response time doesn't reveal registered e-mails.
    private readonly Lazy<string> _dummyHash = new(() => passwordHasher.Hash("dummy-password-for-timing-1"));

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);
        var email = EmailAddress.Normalize(request.Email);
        var now = clock.UtcNow;

        var user = await db.Users.Include(u => u.Company).SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            passwordHasher.Verify(_dummyHash.Value, request.Password!);
            logger.LogWarning("Login failed: unknown e-mail");
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        if (user.IsLockedOut(now))
        {
            logger.LogWarning("Login rejected: user {UserId} is locked out", user.Id);
            throw new AuthenticationFailedException(
                $"Acesso bloqueado temporariamente após várias tentativas sem sucesso. Tente novamente em até {options.Value.LockoutMinutes} minutos.");
        }

        var check = passwordHasher.Verify(user.PasswordHash, request.Password!);
        if (check == PasswordCheck.Failed)
        {
            await RegisterFailedAttemptAsync(user, now, ct);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        // Status is only revealed after the correct password, so it can't be used to probe accounts.
        EnsureCanSignIn(user);

        if (check == PasswordCheck.SuccessRehashNeeded) user.PasswordHash = passwordHasher.Hash(request.Password!);
        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        user.LastLoginAt = now;

        var result = await IssueTokensAsync(user, ipAddress, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} signed in", user.Id);
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, string? ipAddress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) throw new AuthenticationFailedException(SessionExpired);

        var hash = tokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw new AuthenticationFailedException(SessionExpired);
        var now = clock.UtcNow;

        if (stored.RevokedAt is not null)
        {
            if (stored.ReplacedByTokenHash is not null)
            {
                // A rotated token was presented again: it was probably stolen. Kill every session of the user.
                logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking all sessions", stored.UserId);
                await RevokeAllSessionsAsync(stored.UserId, exceptTokenHash: null, ct);
                await db.SaveChangesAsync(ct);
            }
            throw new AuthenticationFailedException(SessionExpired);
        }

        if (!stored.IsActive(now)) throw new AuthenticationFailedException(SessionExpired);

        var user = await db.Users.Include(u => u.Company).SingleOrDefaultAsync(u => u.Id == stored.UserId, ct)
            ?? throw new AuthenticationFailedException(SessionExpired);
        EnsureCanSignIn(user);

        var result = await IssueTokensAsync(user, ipAddress, ct);
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = tokenService.HashRefreshToken(result.RefreshToken);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = tokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt is not null) return;
        stored.RevokedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserProfileResponse> GetProfileAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserEntityAsync(ct);
        return await BuildProfileAsync(user, ct);
    }

    /// <summary>Changes the caller's password and ends all their other sessions.</summary>
    public async Task ChangePasswordAsync(ChangePasswordRequest request, string? currentRefreshToken, CancellationToken ct)
    {
        await changePasswordValidator.ValidateAndThrowAsync(request, ct);
        var user = await GetCurrentUserEntityAsync(ct);

        if (passwordHasher.Verify(user.PasswordHash, request.CurrentPassword!) == PasswordCheck.Failed)
            throw ValidationErrors.ForField(nameof(ChangePasswordRequest.CurrentPassword), "Senha atual incorreta.");

        var violation = PasswordPolicy.GetViolation(request.NewPassword, user.Email);
        if (violation is not null) throw ValidationErrors.ForField(nameof(ChangePasswordRequest.NewPassword), violation);

        user.PasswordHash = passwordHasher.Hash(request.NewPassword!);
        user.PasswordChangedAt = clock.UtcNow;
        var keep = string.IsNullOrWhiteSpace(currentRefreshToken) ? null : tokenService.HashRefreshToken(currentRefreshToken);
        await RevokeAllSessionsAsync(user.Id, keep, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} changed their password", user.Id);
    }

    /// <summary>Used when a user is deactivated, deleted or has the password reset by an administrator.</summary>
    public static async Task RevokeAllSessionsAsync(IFleetDbContext db, DateTime now, Guid userId, string? exceptTokenHash, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.TokenHash != exceptTokenHash)
            .ToListAsync(ct);
        foreach (var token in active) token.RevokedAt = now;
    }

    private Task RevokeAllSessionsAsync(Guid userId, string? exceptTokenHash, CancellationToken ct) =>
        RevokeAllSessionsAsync(db, clock.UtcNow, userId, exceptTokenHash, ct);

    private async Task RegisterFailedAttemptAsync(User user, DateTime now, CancellationToken ct)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= options.Value.MaxFailedAttempts)
        {
            user.LockoutEndAt = now.AddMinutes(options.Value.LockoutMinutes);
            user.FailedLoginCount = 0;
            logger.LogWarning("User {UserId} locked out after repeated failed logins", user.Id);
        }
        else
        {
            logger.LogWarning("Login failed for user {UserId} (attempt {Attempt})", user.Id, user.FailedLoginCount);
        }
        await db.SaveChangesAsync(ct);
    }

    private static void EnsureCanSignIn(User user)
    {
        if (user.Status != UserStatus.Active)
            throw new AuthenticationFailedException("Seu usuário está inativo. Procure o administrador do sistema.");
        if (!user.Company.IsActive || user.Company.DeletedAt is not null)
            throw new AuthenticationFailedException("A empresa vinculada ao seu usuário está inativa. Procure o administrador do sistema.");
    }

    private async Task<AuthResult> IssueTokensAsync(User user, string? ipAddress, CancellationToken ct)
    {
        var profile = await BuildProfileAsync(user, ct);
        var access = tokenService.CreateAccessToken(user, profile.Permissions);
        var rawRefresh = tokenService.GenerateRefreshToken();
        var now = clock.UtcNow;
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(rawRefresh),
            CreatedAt = now,
            ExpiresAt = now.Add(tokenService.RefreshTokenLifetime),
            CreatedByIp = ipAddress,
        };
        db.RefreshTokens.Add(refresh);
        return new AuthResult(access.Token, access.ExpiresAt, rawRefresh, refresh.ExpiresAt, profile);
    }

    private async Task<UserProfileResponse> BuildProfileAsync(User user, CancellationToken ct)
    {
        var permissions = await PermissionResolver.GetForUserAsync(db, user.Id, ct);
        var roles = await db.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.Role.Name).OrderBy(n => n).ToListAsync(ct);
        return new UserProfileResponse(
            user.Id, user.Name, user.Email, user.CompanyId,
            user.Company.TradeName ?? user.Company.LegalName, roles, permissions);
    }

    private async Task<User> GetCurrentUserEntityAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException(SessionExpired);
        return await db.Users.Include(u => u.Company).SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new AuthenticationFailedException(SessionExpired);
    }
}
