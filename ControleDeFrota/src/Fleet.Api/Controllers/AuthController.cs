using Fleet.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Fleet.Api.Controllers;

public sealed class RefreshCookieOptions
{
    public const string SectionName = "Auth:RefreshCookie";
    public const string CookieName = "fleet_refresh";
    public const string CookiePath = "/api/v1/auth";

    /// <summary>Always true outside automated tests (test server runs on plain http).</summary>
    public bool Secure { get; set; } = true;
}

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, UserProfileResponse User);

/// <summary>
/// ADR-007: access token in the body (kept in memory by the SPA), refresh token only in an HttpOnly,
/// SameSite=Strict cookie scoped to /api/v1/auth — JavaScript can never read it.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(AuthService auth, IOptions<RefreshCookieOptions> cookieOptions) : ControllerBase
{
    public const string RateLimitPolicy = "auth";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<LoginResponse> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ClientIp, ct);
        return Issue(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<LoginResponse> Refresh(CancellationToken ct)
    {
        try
        {
            var result = await auth.RefreshAsync(Request.Cookies[RefreshCookieOptions.CookieName], ClientIp, ct);
            return Issue(result);
        }
        catch (Fleet.Application.Common.AuthenticationFailedException)
        {
            ClearCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookieOptions.CookieName], ct);
        ClearCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public Task<UserProfileResponse> Me(CancellationToken ct) => auth.GetProfileAsync(ct);

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(request, Request.Cookies[RefreshCookieOptions.CookieName], ct);
        return NoContent();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private LoginResponse Issue(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieOptions.CookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = cookieOptions.Value.Secure,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookieOptions.CookiePath,
            Expires = result.RefreshTokenExpiresAt,
            IsEssential = true,
        });
        return new LoginResponse(result.AccessToken, result.AccessTokenExpiresAt, result.User);
    }

    private void ClearCookie() =>
        Response.Cookies.Delete(RefreshCookieOptions.CookieName, new CookieOptions
        {
            Path = RefreshCookieOptions.CookiePath,
            Secure = cookieOptions.Value.Secure,
            SameSite = SameSiteMode.Strict,
            HttpOnly = true,
        });
}
