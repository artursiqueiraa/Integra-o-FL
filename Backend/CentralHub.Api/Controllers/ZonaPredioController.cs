using CentralHub.Api.DTOs;
using CentralHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralHub.Api.Controllers;

/// <summary>Gerencia o cadastro de Zonas (nome/tipo) de cada Central, usado pelo painel de Operação.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ZonaPredioController : ControllerBase
{
    private readonly ZonaPredioService _service;
    private readonly ILogger<ZonaPredioController> _logger;

    public ZonaPredioController(ZonaPredioService service, ILogger<ZonaPredioController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Lista as Zonas cadastradas, opcionalmente filtradas por Prédio e/ou Central.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ZonaPredioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ZonaPredioDto>>> GetAll([FromQuery] int? buildingId, [FromQuery] int? centralId)
    {
        return Ok(await _service.ListarAsync(buildingId, centralId));
    }

    /// <summary>Obtém uma Zona cadastrada pelo Id.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ZonaPredioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ZonaPredioDto>> GetById(int id)
    {
        var zona = await _service.ObterPorIdAsync(id);
        if (zona == null)
        {
            return NotFound($"Zona {id} não encontrada.");
        }
        return Ok(zona);
    }

    /// <summary>Cadastra uma nova Zona para uma Central.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ZonaPredioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ZonaPredioDto>> Create(CreateZonaPredioDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza o nome/tipo/status de uma Zona cadastrada.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateZonaPredioDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var atualizado = await _service.AtualizarAsync(id, dto);
        if (!atualizado)
        {
            return NotFound($"Zona {id} não encontrada.");
        }
        return NoContent();
    }

    /// <summary>Remove uma Zona cadastrada (não afeta o estado real da zona na central).</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var excluido = await _service.ExcluirAsync(id);
        if (!excluido)
        {
            return NotFound($"Zona {id} não encontrada.");
        }
        return NoContent();
    }
}
