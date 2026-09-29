using Microsoft.EntityFrameworkCore;
using REC4.Application.Auth.Dtos;
using REC4.Application.Common;
using REC4.Application.Exceptions;

namespace REC4.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IRec4DbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(IRec4DbContext db, IPasswordHasher passwordHasher, ITokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        var login = dto.Login.Trim();

        var usuario = await _db.Usuarios
            .Include(u => u.Grupo)
            .FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

        if (usuario is null || !usuario.IsActive || !_passwordHasher.Verify(usuario.SenhaHash, dto.Senha))
            throw new InvalidCredentialsException();

        var permissoes = await PermissaoEfetivaResolver.ResolveAsync(_db, usuario, cancellationToken);

        var token = _tokenService.GenerateToken(usuario, permissoes);

        var usuarioDto = new UsuarioAutenticadoDto(usuario.Id, usuario.Nome, usuario.Login, usuario.Grupo.Nome, permissoes);
        return new LoginResponseDto(token.Token, token.ExpiresAtUtc, usuarioDto);
    }
}
