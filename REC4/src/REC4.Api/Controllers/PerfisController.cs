using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using REC4.Application.Pessoas;
using REC4.Application.Pessoas.Dtos;

namespace REC4.Api.Controllers;

[ApiController]
[Route("api/perfis")]
[Authorize(Policy = "Pessoa.Visualizar")]
public class PerfisController : ControllerBase
{
    private readonly IPessoaService _pessoaService;

    public PerfisController(IPessoaService pessoaService)
    {
        _pessoaService = pessoaService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PerfilDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _pessoaService.ListPerfisAsync(cancellationToken));
    }
}
