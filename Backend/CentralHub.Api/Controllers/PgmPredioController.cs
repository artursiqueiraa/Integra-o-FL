using CentralHub.Api.DTOs;
using CentralHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralHub.Api.Controllers;

/// <summary>Gerencia o cadastro de PGMs (nome/tipo/ícone) de cada Central, usado pelo painel de Operação.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PgmPredioController : ControllerBase
{
    private readonly PgmPredioService _service;
    private readonly ILogger<PgmPredioController> _logger;

    public PgmPredioController(PgmPredioService service, ILogger<PgmPredioController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Lista as PGMs cadastradas, opcionalmente filtradas por Prédio e/ou Central.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PgmPredioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PgmPredioDto>>> GetAll([FromQuery] int? buildingId, [FromQuery] int? centralId)
    {
        return Ok(await _service.ListarAsync(buildingId, centralId));
    }

    /// <summary>Obtém uma PGM cadastrada pelo Id.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PgmPredioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PgmPredioDto>> GetById(int id)
    {
        var pgm = await _service.ObterPorIdAsync(id);
        if (pgm == null)
        {
            return NotFound($"PGM {id} não encontrada.");
        }
        return Ok(pgm);
    }

    /// <summary>Cadastra uma nova PGM para uma Central.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PgmPredioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PgmPredioDto>> Create(CreatePgmPredioDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza o nome/tipo/ícone/status de uma PGM cadastrada.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdatePgmPredioDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var atualizado = await _service.AtualizarAsync(id, dto);
        if (!atualizado)
        {
            return NotFound($"PGM {id} não encontrada.");
        }
        return NoContent();
    }

    /// <summary>Remove uma PGM cadastrada (não afeta o estado real da PGM na central).</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var excluido = await _service.ExcluirAsync(id);
        if (!excluido)
        {
            return NotFound($"PGM {id} não encontrada.");
        }
        return NoContent();
    }
}
