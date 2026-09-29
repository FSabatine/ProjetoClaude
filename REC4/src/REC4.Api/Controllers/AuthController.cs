using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using REC4.Application.Auth;
using REC4.Application.Auth.Dtos;

namespace REC4.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<LoginRequestDto> _loginValidator;

    public AuthController(IAuthService authService, IValidator<LoginRequestDto> loginValidator)
    {
        _authService = authService;
        _loginValidator = loginValidator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto dto, CancellationToken cancellationToken)
    {
        await _loginValidator.ValidateAndThrowAsync(dto, cancellationToken);
        return Ok(await _authService.LoginAsync(dto, cancellationToken));
    }
}
