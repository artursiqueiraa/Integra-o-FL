using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.Api.Models;
using CentralHub.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Controllers;

/// <summary>
/// Envia comandos de PGM para as Centrais e consulta o histórico de operações. Reaproveita
/// exatamente o mesmo <see cref="PgmService"/> usado pela Tela Central — mesma sessão TCP real
/// via <c>SessionManager</c>, mesmo caminho homologado. Não existe mais nenhuma simulação aqui:
/// o antigo <c>OperationService</c> (que sempre retornava sucesso simulado via
/// <c>AdapterFactory</c>/<c>FakeAdapter</c>, sem falar com uma central de verdade) foi removido.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OperationController : ControllerBase
{
    private static readonly string[] ComandosValidos = { "Pulso", "Ligar", "Desligar" };

    private readonly PgmService _pgmService;
    private readonly AppDbContext _context;
    private readonly ILogger<OperationController> _logger;

    public OperationController(PgmService pgmService, AppDbContext context, ILogger<OperationController> logger)
    {
        _pgmService = pgmService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Envia um comando (Pulso, Ligar ou Desligar) para um PGM de uma Central — mesmo fluxo real
    /// da Tela Central (<see cref="PgmService"/> → <c>PgmCommandService</c> do SDK → sessão TCP
    /// já aberta pela central via <c>SessionManager</c>). Nunca disca para fora.
    /// </summary>
    [HttpPost("enviar")]
    [ProducesResponseType(typeof(HistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<HistoryDto>> Enviar(ComandoDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!ComandosValidos.Contains(dto.Comando, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest($"Comando inválido. Valores aceitos: {string.Join(", ", ComandosValidos)}.");
        }

        if (dto.Comando.Equals("Pulso", StringComparison.OrdinalIgnoreCase) && dto.TempoPulsoMs <= 0)
        {
            return BadRequest("Tempo do Pulso deve ser maior que zero.");
        }

        _logger.LogInformation("Recebida requisição de comando {Comando} para Central {CentralId}", dto.Comando, dto.CentralId);

        // Mapeamento direto para o serviço real já homologado — nenhuma lógica de protocolo
        // vive aqui, só a decisão de qual método de PgmService chamar. Se a central estiver
        // offline ou não responder, PgmService lança BusinessException (409/502), tratada pelo
        // ExceptionHandlingMiddleware global — nenhum registro de histórico "de sucesso" chega
        // a ser criado para um comando que falhou de verdade.
        var resultado = dto.Comando.ToLowerInvariant() switch
        {
            "ligar" => await _pgmService.LigarAsync(dto.CentralId, dto.PGM, cancellationToken),
            "desligar" => await _pgmService.DesligarAsync(dto.CentralId, dto.PGM, cancellationToken),
            "pulso" => await _pgmService.PulsoAsync(dto.CentralId, dto.PGM, dto.TempoPulsoMs, cancellationToken),
            _ => throw new InvalidOperationException("Comando inválido (inatingível: já validado acima)."),
        };

        var resultadoTexto = resultado.EstadoConfirmado switch
        {
            true => $"PGM {resultado.Pgm} ligado",
            false => $"PGM {resultado.Pgm} desligado",
            null => "Comando enviado",
        };

        var history = new History
        {
            Data = DateTime.Now,
            CentralId = dto.CentralId,
            PGM = dto.PGM,
            Comando = dto.Comando,
            Resultado = resultadoTexto,
        };

        _context.Histories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Comando registrado no histórico {HistoryId}: {Resultado}", history.Id, history.Resultado);

        return Ok(new HistoryDto
        {
            Id = history.Id,
            Data = history.Data,
            CentralId = history.CentralId,
            PGM = history.PGM,
            Comando = history.Comando,
            Resultado = history.Resultado,
        });
    }

    /// <summary>Lista o histórico de comandos enviados, opcionalmente filtrado por Central.</summary>
    [HttpGet("historico")]
    [ProducesResponseType(typeof(IEnumerable<HistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<HistoryDto>>> Historico([FromQuery] int? centralId, CancellationToken cancellationToken)
    {
        var query = _context.Histories.Include(h => h.Central).OrderByDescending(h => h.Data).AsQueryable();
        if (centralId.HasValue)
        {
            query = query.Where(h => h.CentralId == centralId.Value);
        }

        var historico = await query.ToListAsync(cancellationToken);

        return Ok(historico.Select(h => new HistoryDto
        {
            Id = h.Id,
            Data = h.Data,
            CentralId = h.CentralId,
            CentralNome = h.Central?.Nome,
            PGM = h.PGM,
            Comando = h.Comando,
            Resultado = h.Resultado,
        }));
    }
}
