using FluentValidation;
using REC4.Application.Auth.Dtos;

namespace REC4.Application.Auth.Validators;

public class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(l => l.Login).NotEmpty();
        RuleFor(l => l.Senha).NotEmpty();
    }
}
