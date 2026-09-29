using System.IdentityModel.Tokens.Jwt;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using REC4.Application.Usuarios;
using REC4.Application.Usuarios.Dtos;

namespace REC4.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;
    private readonly IValidator<UsuarioCreateDto> _createValidator;
    private readonly IValidator<UsuarioUpdateDto> _updateValidator;
    private readonly IValidator<UsuarioPermissoesUpdateDto> _permissoesValidator;
    private readonly IValidator<UsuarioEscritorioCreateDto> _escritorioValidator;
    private readonly IValidator<UsuarioEmitenteCreateDto> _emitenteValidator;

    public UsuariosController(
        IUsuarioService usuarioService,
        IValidator<UsuarioCreateDto> createValidator,
        IValidator<UsuarioUpdateDto> updateValidator,
        IValidator<UsuarioPermissoesUpdateDto> permissoesValidator,
        IValidator<UsuarioEscritorioCreateDto> escritorioValidator,
        IValidator<UsuarioEmitenteCreateDto> emitenteValidator)
    {
        _usuarioService = usuarioService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _permissoesValidator = permissoesValidator;
        _escritorioValidator = escritorioValidator;
        _emitenteValidator = emitenteValidator;
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeuUsuarioDto>> Me(CancellationToken cancellationToken)
    {
        var subClaim = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        return Ok(await _usuarioService.GetMeAsync(Guid.Parse(subClaim), cancellationToken));
    }

    [HttpGet]
    [Authorize(Policy = "Usuario.Visualizar")]
    public async Task<ActionResult<IReadOnlyList<UsuarioResumoDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _usuarioService.ListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Usuario.Visualizar")]
    public async Task<ActionResult<UsuarioDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _usuarioService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "Usuario.Criar")]
    public async Task<ActionResult<UsuarioDto>> Create(UsuarioCreateDto dto, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);
        var usuario = await _usuarioService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, usuario);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Usuario.Editar")]
    public async Task<ActionResult<UsuarioDto>> Update(Guid id, UsuarioUpdateDto dto, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);
        return Ok(await _usuarioService.UpdateAsync(id, dto, cancellationToken));
    }

    [HttpPut("{id:guid}/permissoes")]
    [Authorize(Policy = "Usuario.Editar")]
    public async Task<ActionResult<UsuarioDto>> SetPermissoes(Guid id, UsuarioPermissoesUpdateDto dto, CancellationToken cancellationToken)
    {
        await _permissoesValidator.ValidateAndThrowAsync(dto, cancellationToken);
        return Ok(await _usuarioService.SetPermissoesAsync(id, dto, cancellationToken));
    }

    [HttpPost("{id:guid}/escritorios")]
    [Authorize(Policy = "Usuario.Editar")]
    public async Task<ActionResult<UsuarioDto>> AddEscritorio(Guid id, UsuarioEscritorioCreateDto dto, CancellationToken cancellationToken)
    {
        await _escritorioValidator.ValidateAndThrowAsync(dto, cancellationToken);
        return Ok(await _usuarioService.AddEscritorioAsync(id, dto, cancellationToken));
    }

    [HttpPut("{id:guid}/escritorios/{escritorioId:guid}/principal")]
    [Authorize(Policy = "Usuario.Editar")]
    public async Task<ActionResult<UsuarioDto>> SetEscritorioPrincipal(Guid id, Guid escritorioId, CancellationToken cancellationToken)
    {
        return Ok(await _usuarioService.SetEscritorioPrincipalAsync(id, escritorioId, cancellationToken));
    }

    [HttpDelete("{id:guid}/escritorios/{escritorioId:guid}")]
    [Authorize(Policy = "Usuario.Editar")]
    public async Task<ActionResult<UsuarioDto>> RemoveEscritorio(Guid id, Guid escritorioId, CancellationToken cancellationToken)
    {
        return Ok(await _usuarioService.RemoveEscritorioAsync(id, escritorioId, cancellationToken));
    }

    [HttpPost("{id:guid}/emitentes")]
    [Authorize(Policy = "Usuario.Editar")]
    public async Task<ActionResult<UsuarioDto>> AddEmitente(Guid id, UsuarioEmitenteCreateDto dto, CancellationToken cancellationToken)
    {
        await _emitenteValidator.ValidateAndThrowAsync(dto, cancellationToken);
        return Ok(await _usuarioService.AddEmitenteAsync(id, dto, cancellationToken));
    }

    [HttpDelete("{id:guid}/emitentes/{pessoaId:guid}")]
    [Authorize(Policy = "Usuario.Editar")]
    public async Task<ActionResult<UsuarioDto>> RemoveEmitente(Guid id, Guid pessoaId, CancellationToken cancellationToken)
    {
        return Ok(await _usuarioService.RemoveEmitenteAsync(id, pessoaId, cancellationToken));
    }
}
