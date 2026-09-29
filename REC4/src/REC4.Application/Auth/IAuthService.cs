using REC4.Application.Auth.Dtos;

namespace REC4.Application.Auth;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
}
