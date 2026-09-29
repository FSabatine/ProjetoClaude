using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using REC4.Application.Escritorios;
using REC4.Application.Escritorios.Dtos;

namespace REC4.Api.Controllers;

[ApiController]
[Route("api/escritorios")]
[Authorize(Policy = "Escritorio.Visualizar")]
public class EscritoriosController : ControllerBase
{
    private readonly IEscritorioService _escritorioService;
    private readonly IValidator<EscritorioCreateDto> _createValidator;
    private readonly IValidator<EscritorioUpdateDto> _updateValidator;

    public EscritoriosController(
        IEscritorioService escritorioService,
        IValidator<EscritorioCreateDto> createValidator,
        IValidator<EscritorioUpdateDto> updateValidator)
    {
        _escritorioService = escritorioService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EscritorioDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _escritorioService.ListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EscritorioDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _escritorioService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "Escritorio.Criar")]
    public async Task<ActionResult<EscritorioDto>> Create(EscritorioCreateDto dto, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);
        var escritorio = await _escritorioService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = escritorio.Id }, escritorio);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Escritorio.Editar")]
    public async Task<ActionResult<EscritorioDto>> Update(Guid id, EscritorioUpdateDto dto, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);
        return Ok(await _escritorioService.UpdateAsync(id, dto, cancellationToken));
    }
}
