using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using REC4.Application.Pessoas;
using REC4.Application.Pessoas.Dtos;

namespace REC4.Api.Controllers;

[ApiController]
[Route("api/pessoas")]
[Authorize(Policy = "Pessoa.Visualizar")]
public class PessoasController : ControllerBase
{
    private readonly IPessoaService _pessoaService;
    private readonly IValidator<PessoaCreateDto> _createValidator;
    private readonly IValidator<PessoaUpdateDto> _updateValidator;

    public PessoasController(
        IPessoaService pessoaService,
        IValidator<PessoaCreateDto> createValidator,
        IValidator<PessoaUpdateDto> updateValidator)
    {
        _pessoaService = pessoaService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpPost]
    [Authorize(Policy = "Pessoa.Criar")]
    public async Task<ActionResult<PessoaDto>> Create(PessoaCreateDto dto, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);
        var pessoa = await _pessoaService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pessoa.Id }, pessoa);
    }

    [HttpGet]
    public async Task<ActionResult<object>> List(int page = 1, int pageSize = 20, int? perfilId = null, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _pessoaService.ListAsync(page, pageSize, perfilId, cancellationToken);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PessoaDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _pessoaService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Pessoa.Editar")]
    public async Task<ActionResult<PessoaDto>> Update(Guid id, PessoaUpdateDto dto, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);
        return Ok(await _pessoaService.UpdateAsync(id, dto, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Pessoa.Editar")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _pessoaService.SoftDeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/perfis")]
    [Authorize(Policy = "Pessoa.Editar")]
    public async Task<ActionResult<PessoaDto>> SetPerfis(Guid id, [FromBody] List<int> perfilIds, CancellationToken cancellationToken)
    {
        return Ok(await _pessoaService.SetPerfisAsync(id, perfilIds, cancellationToken));
    }

    [HttpPost("{id:guid}/enderecos")]
    [Authorize(Policy = "Pessoa.Editar")]
    public async Task<ActionResult<EnderecoDto>> AddEndereco(Guid id, EnderecoCreateDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _pessoaService.AddEnderecoAsync(id, dto, cancellationToken));
    }

    [HttpPut("{id:guid}/enderecos/{enderecoId:guid}/principal")]
    [Authorize(Policy = "Pessoa.Editar")]
    public async Task<IActionResult> SetEnderecoPrincipal(Guid id, Guid enderecoId, CancellationToken cancellationToken)
    {
        await _pessoaService.SetEnderecoPrincipalAsync(id, enderecoId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/contas-bancarias")]
    [Authorize(Policy = "Pessoa.Editar")]
    public async Task<ActionResult<ContaBancariaDto>> AddContaBancaria(Guid id, ContaBancariaCreateDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _pessoaService.AddContaBancariaAsync(id, dto, cancellationToken));
    }

    [HttpPut("{id:guid}/contas-bancarias/{contaId:guid}/principal")]
    [Authorize(Policy = "Pessoa.Editar")]
    public async Task<IActionResult> SetContaPrincipal(Guid id, Guid contaId, CancellationToken cancellationToken)
    {
        await _pessoaService.SetContaPrincipalAsync(id, contaId, cancellationToken);
        return NoContent();
    }
}
