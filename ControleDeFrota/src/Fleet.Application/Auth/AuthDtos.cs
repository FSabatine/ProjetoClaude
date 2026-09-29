using FluentValidation;

namespace Fleet.Application.Auth;

public sealed record LoginRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }
}

public sealed record ChangePasswordRequest
{
    public string? CurrentPassword { get; init; }
    public string? NewPassword { get; init; }
}

public sealed record UserProfileResponse(
    Guid Id,
    string Name,
    string Email,
    Guid CompanyId,
    string CompanyName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>AccessToken goes in the response body; RefreshToken is set by the API as an HttpOnly cookie.</summary>
public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserProfileResponse User);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Informe o e-mail.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Informe a senha.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword).WithMessage("A nova senha deve ser diferente da atual.");
    }
}
