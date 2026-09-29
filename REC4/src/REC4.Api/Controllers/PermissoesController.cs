using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using REC4.Application.Permissoes;
using REC4.Application.Permissoes.Dtos;

namespace REC4.Api.Controllers;

[ApiController]
[Route("api/permissoes")]
[Authorize(Policy = "Usuario.Visualizar")]
public class PermissoesController : ControllerBase
{
    private readonly IPermissaoService _permissaoService;

    public PermissoesController(IPermissaoService permissaoService)
    {
        _permissaoService = permissaoService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PermissaoDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _permissaoService.ListAsync(cancellationToken));
    }
}
