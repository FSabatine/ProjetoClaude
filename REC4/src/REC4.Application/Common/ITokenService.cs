using REC4.Domain.Entities;

namespace REC4.Application.Common;

public record TokenResult(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    TokenResult GenerateToken(Usuario usuario, IReadOnlyList<string> permissoes);
}
