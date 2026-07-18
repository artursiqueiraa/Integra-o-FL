using System.ComponentModel.DataAnnotations;
using CentralHub.Api.DTOs;
using CentralHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CentralHub.Api.Controllers;

/// <summary>
/// Gerencia o cadastro de Centrais de alarme, o monitoramento da sessão TCP real (via
/// SessionManager) e a operação de PGMs. Nunca abre conexão de saída — toda informação de
/// sessão vem da conexão que a própria central já mantém aberta com o servidor.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CentralController : ControllerBase
{
    private readonly CentralService _service;
    private readonly CentralStatusService _statusService;
    private readonly PgmService _pgmService;
    private readonly ArmService _armService;
    private readonly ZoneInhibitService _zoneInhibitService;
    private readonly CentralSessionService _sessionService;
    private readonly ILogger<CentralController> _logger;

    public CentralController(
        CentralService service,
        CentralStatusService statusService,
        PgmService pgmService,
        ArmService armService,
        ZoneInhibitService zoneInhibitService,
        CentralSessionService sessionService,
        ILogger<CentralController> logger)
    {
        _service = service;
        _statusService = statusService;
        _pgmService = pgmService;
        _armService = armService;
        _zoneInhibitService = zoneInhibitService;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>Lista todas as centrais cadastradas (sem retornar a senha).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CentralDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CentralDto>>> GetAll()
    {
        return Ok(await _service.ListarAsync());
    }

    /// <summary>Obtém uma central pelo Id (sem retornar a senha).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CentralDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CentralDto>> GetById(int id)
    {
        var central = await _service.ObterPorIdAsync(id);
        if (central == null)
        {
            _logger.LogWarning("Central {CentralId} não encontrada", id);
            return NotFound($"Central {id} não encontrada.");
        }
        return Ok(central);
    }

    /// <summary>Cadastra uma nova central, vinculada a um Prédio existente.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CentralDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CentralDto>> Create(CreateCentralDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza uma central existente.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpdateCentralDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var atualizado = await _service.AtualizarAsync(id, dto);
        if (!atualizado)
        {
            return NotFound($"Central {id} não encontrada.");
        }
        return NoContent();
    }

    /// <summary>Remove uma central.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var excluido = await _service.ExcluirAsync(id);
        if (!excluido)
        {
            return NotFound($"Central {id} não encontrada.");
        }
        return NoContent();
    }

    /// <summary>
    /// Consulta o status completo da central (partições, zonas, PGMs, eletrificador,
    /// bateria, alimentação AC e problemas) ao vivo, via comando 0x4D enviado na
    /// sessão TCP que a central mantém aberta com o servidor JFL.
    /// </summary>
    /// <remarks>
    /// Rota exposta como <c>/api/centrais/{id}/status</c> (fora do prefixo
    /// <c>api/Central</c> do restante deste controller) para casar exatamente com o
    /// endpoint solicitado. Retorna 409 quando a central não possui sessão ativa
    /// (offline) ou não possui Número de Série cadastrado.
    /// </remarks>
    [HttpGet("~/api/centrais/{id}/status")]
    [ProducesResponseType(typeof(CentralStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CentralStatusDto>> ConsultarStatus(int id, CancellationToken cancellationToken)
    {
        var status = await _statusService.ConsultarStatusAsync(id, cancellationToken);
        return Ok(status);
    }

    /// <summary>Aciona (liga) uma PGM (1 a 16) na sessão TCP ativa da central (comando oficial 0x50).</summary>
    [HttpPost("~/api/centrais/{id}/pgm/{pgm}/ligar")]
    [ProducesResponseType(typeof(PgmCommandResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PgmCommandResultDto>> LigarPgm(int id, [Range(1, 16)] int pgm, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _pgmService.LigarAsync(id, pgm, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>Desaciona (desliga) uma PGM (1 a 16) na sessão TCP ativa da central (comando oficial 0x51).</summary>
    [HttpPost("~/api/centrais/{id}/pgm/{pgm}/desligar")]
    [ProducesResponseType(typeof(PgmCommandResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PgmCommandResultDto>> DesligarPgm(int id, [Range(1, 16)] int pgm, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _pgmService.DesligarAsync(id, pgm, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>
    /// Pulso: aciona a PGM, aguarda a duração informada e desaciona — dois comandos oficiais
    /// (0x50 seguido de 0x51) em sequência na mesma sessão; não existe um terceiro comando de
    /// "pulso" no protocolo JFL.
    /// </summary>
    [HttpPost("~/api/centrais/{id}/pgm/{pgm}/pulso")]
    [ProducesResponseType(typeof(PgmCommandResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PgmCommandResultDto>> PulsoPgm(int id, [Range(1, 16)] int pgm, PulsoPgmDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _pgmService.PulsoAsync(id, pgm, dto.DuracaoMs, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>Arma uma partição (1 a 16, ou 99 para o eletrificador) na sessão TCP ativa da central (comando oficial 0x4E).</summary>
    [HttpPost("~/api/centrais/{id}/particoes/{particao}/armar")]
    [ProducesResponseType(typeof(ArmCommandResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ArmCommandResultDto>> Armar(int id, [Range(1, 99)] int particao, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _armService.ArmarAsync(id, particao, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>Desarma uma partição (1 a 16, ou 99 para o eletrificador) na sessão TCP ativa da central (comando oficial 0x4F).</summary>
    [HttpPost("~/api/centrais/{id}/particoes/{particao}/desarmar")]
    [ProducesResponseType(typeof(ArmCommandResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ArmCommandResultDto>> Desarmar(int id, [Range(1, 99)] int particao, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _armService.DesarmarAsync(id, particao, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>Arma uma partição no modo Stay (comando oficial 0x53). Não se aplica ao eletrificador.</summary>
    [HttpPost("~/api/centrais/{id}/particoes/{particao}/armar-stay")]
    [ProducesResponseType(typeof(ArmCommandResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ArmCommandResultDto>> ArmarStay(int id, [Range(1, 99)] int particao, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _armService.ArmarStayAsync(id, particao, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>Arma uma partição no modo Away (comando oficial 0x54).</summary>
    [HttpPost("~/api/centrais/{id}/particoes/{particao}/armar-away")]
    [ProducesResponseType(typeof(ArmCommandResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ArmCommandResultDto>> ArmarAway(int id, [Range(1, 99)] int particao, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _armService.ArmarAwayAsync(id, particao, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>
    /// Inibe uma zona (1 a 99) na sessão TCP ativa da central (comando oficial 0x52). O comando
    /// real substitui o conjunto inteiro de zonas inibidas — este endpoint consulta o estado
    /// atual antes de enviar, então o efeito observado é "inibir só esta zona, sem desinibir as
    /// demais".
    /// </summary>
    [HttpPost("~/api/centrais/{id}/zonas/{zona}/inibir")]
    [ProducesResponseType(typeof(ZoneInhibitResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ZoneInhibitResultDto>> InibirZona(int id, [Range(1, 99)] int zona, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _zoneInhibitService.InibirAsync(id, zona, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>Desinibe uma zona (1 a 99) na sessão TCP ativa da central (comando oficial 0x52, mesmo mecanismo de substituição do conjunto completo).</summary>
    [HttpPost("~/api/centrais/{id}/zonas/{zona}/desinibir")]
    [ProducesResponseType(typeof(ZoneInhibitResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ZoneInhibitResultDto>> DesinibirZona(int id, [Range(1, 99)] int zona, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var resultado = await _zoneInhibitService.DesinibirAsync(id, zona, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>Lista os números das zonas atualmente inibidas, consultando o status ao vivo da central (comando 0x4D).</summary>
    [HttpGet("~/api/centrais/{id}/zonas/inibidas")]
    [ProducesResponseType(typeof(IEnumerable<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IEnumerable<int>>> ZonasInibidas(int id, CancellationToken cancellationToken)
    {
        var zonas = await _zoneInhibitService.ObterInibidasAsync(id, cancellationToken);
        return Ok(zonas);
    }

    /// <summary>
    /// Snapshot completo da sessão TCP da central — Status da Conexão, painel Sessão TCP e
    /// modal Detalhes da Sessão usam este mesmo endpoint. Nunca abre conexão nenhuma: consulta
    /// só o SessionManager (sessão ao vivo) e o histórico já persistido no banco.
    /// </summary>
    [HttpGet("~/api/centrais/{id}/sessao")]
    [ProducesResponseType(typeof(SessaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessaoDto>> ObterSessao(int id, CancellationToken cancellationToken)
    {
        var sessao = await _sessionService.ObterSessaoAsync(id, cancellationToken);
        return Ok(sessao);
    }

    /// <summary>Últimas entradas do log de atividade capturado da sessão desta central (mais recente primeiro).</summary>
    [HttpGet("~/api/centrais/{id}/log")]
    [ProducesResponseType(typeof(IEnumerable<AtividadeLogEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<AtividadeLogEntryDto>>> ObterLog(int id, CancellationToken cancellationToken, [FromQuery] int max = 100)
    {
        var log = await _sessionService.ObterLogAsync(id, max, cancellationToken);
        return Ok(log);
    }

    /// <summary>
    /// "Reconectar": NÃO abre conexão nenhuma. Só limpa a sessão registrada no SessionManager
    /// (mesmo efeito de a central cair sozinha) — a central deve reconectar por conta própria.
    /// </summary>
    [HttpPost("~/api/centrais/{id}/reconectar")]
    [ProducesResponseType(typeof(ReconectarResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReconectarResultDto>> Reconectar(int id, CancellationToken cancellationToken)
    {
        var resultado = await _sessionService.ReconectarAsync(id, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>Checklist de diagnóstico da central (sessão, handshake, keep-alive, cadastro, vínculo com Prédio).</summary>
    [HttpGet("~/api/centrais/{id}/diagnostico")]
    [ProducesResponseType(typeof(DiagnosticoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiagnosticoDto>> ObterDiagnostico(int id, CancellationToken cancellationToken)
    {
        var diagnostico = await _sessionService.ObterDiagnosticoAsync(id, cancellationToken);
        return Ok(diagnostico);
    }
}
