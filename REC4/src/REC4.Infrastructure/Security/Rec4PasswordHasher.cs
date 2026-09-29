using Microsoft.AspNetCore.Identity;
using REC4.Application.Common;
using REC4.Domain.Entities;

namespace REC4.Infrastructure.Security;

public class Rec4PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<Usuario> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public bool Verify(string hash, string providedPassword) =>
        _inner.VerifyHashedPassword(null!, hash, providedPassword) != PasswordVerificationResult.Failed;
}
