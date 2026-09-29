using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using REC4.Application.Grupos;
using REC4.Application.Grupos.Dtos;

namespace REC4.Api.Controllers;

[ApiController]
[Route("api/grupos")]
[Authorize(Policy = "Usuario.Visualizar")]
public class GruposController : ControllerBase
{
    private readonly IGrupoService _grupoService;
    private readonly IValidator<GrupoCreateDto> _createValidator;
    private readonly IValidator<GrupoUpdateDto> _updateValidator;

    public GruposController(
        IGrupoService grupoService,
        IValidator<GrupoCreateDto> createValidator,
        IValidator<GrupoUpdateDto> updateValidator)
    {
        _grupoService = grupoService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GrupoDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _grupoService.ListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GrupoDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _grupoService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "Usuario.Criar")]
    public async Task<ActionResult<GrupoDetailDto>> Create(GrupoCreateDto dto, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);
        var grupo = await _grupoService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = grupo.Id }, grupo);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "Usuario.Editar")]
    public async Task<ActionResult<GrupoDetailDto>> Update(int id, GrupoUpdateDto dto, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);
        return Ok(await _grupoService.UpdateAsync(id, dto, cancellationToken));
    }
}
