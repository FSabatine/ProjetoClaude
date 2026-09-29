using Fleet.Application.Common;
using Fleet.Domain.Users;
using Microsoft.AspNetCore.Identity;
using IdentityResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace Fleet.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 (ASP.NET Core Identity v3 format, 100k iterations, per-password salt)
/// without pulling the whole Identity stack.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();
    private static readonly User HashUser = new();

    public string Hash(string password) => _inner.HashPassword(HashUser, password);

    public PasswordCheck Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash)) return PasswordCheck.Failed;
        try
        {
            return _inner.VerifyHashedPassword(HashUser, hash, password) switch
            {
                IdentityResult.Success => PasswordCheck.Success,
                IdentityResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
                _ => PasswordCheck.Failed,
            };
        }
        catch (FormatException)
        {
            // A corrupted hash must never grant access.
            return PasswordCheck.Failed;
        }
    }
}
