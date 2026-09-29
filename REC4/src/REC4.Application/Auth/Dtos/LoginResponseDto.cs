namespace REC4.Application.Auth.Dtos;

public record UsuarioAutenticadoDto(Guid Id, string Nome, string Login, string Grupo, IReadOnlyList<string> Permissoes);

public record LoginResponseDto(string Token, DateTime ExpiresAtUtc, UsuarioAutenticadoDto Usuario);
