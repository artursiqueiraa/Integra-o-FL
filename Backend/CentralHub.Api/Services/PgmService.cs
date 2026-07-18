using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.SDK.Jfl.Server;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Envia comandos de PGM (Ligar, Desligar, Pulso) para uma Central cadastrada: resolve o
/// NumeroSerie no banco e delega ao <see cref="PgmCommandService"/> do SDK, que usa a sessao
/// TCP ja aberta pela central (via SessionManager) — nunca disca para fora.
/// </summary>
public class PgmService
{
    private readonly AppDbContext _context;
    private readonly PgmCommandService _pgmCommandService;
    private readonly ILogger<PgmService> _logger;

    public PgmService(AppDbContext context, PgmCommandService pgmCommandService, ILogger<PgmService> logger)
    {
        _context = context;
        _pgmCommandService = pgmCommandService;
        _logger = logger;
    }

    public Task<PgmCommandResultDto> LigarAsync(int centralId, int pgm, CancellationToken cancellationToken) =>
        ExecutarAsync(centralId, pgm, (numeroSerie, ct) => _pgmCommandService.AcionarAsync(numeroSerie, pgm, ct), cancellationToken);

    public Task<PgmCommandResultDto> DesligarAsync(int centralId, int pgm, CancellationToken cancellationToken) =>
        ExecutarAsync(centralId, pgm, (numeroSerie, ct) => _pgmCommandService.DesacionarAsync(numeroSerie, pgm, ct), cancellationToken);

    public Task<PgmCommandResultDto> PulsoAsync(int centralId, int pgm, int duracaoMs, CancellationToken cancellationToken) =>
        ExecutarAsync(centralId, pgm, (numeroSerie, ct) => _pgmCommandService.PulsoAsync(numeroSerie, pgm, duracaoMs, ct), cancellationToken);

    private async Task<PgmCommandResultDto> ExecutarAsync(
        int centralId, int pgm, Func<string, CancellationToken, Task<PgmCommandResult>> executar, CancellationToken cancellationToken)
    {
        var central = await _context.Centrals.FirstOrDefaultAsync(c => c.Id == centralId, cancellationToken);
        if (central is null)
        {
            throw new BusinessException($"Central {centralId} não encontrada.", statusCode: 404);
        }

        if (string.IsNullOrEmpty(central.NumeroSerie))
        {
            throw new BusinessException(
                $"Central {centralId} não possui Número de Série cadastrado; não é possível localizar a sessão da central.",
                statusCode: 409);
        }

        _logger.LogInformation("Requisicao de comando PGM: CentralId={CentralId} NumeroSerie={NumeroSerie} PGM={Pgm}", centralId, central.NumeroSerie, pgm);

        var resultado = await executar(central.NumeroSerie, cancellationToken);

        if (!resultado.Sucesso)
        {
            _logger.LogWarning(
                "Comando PGM falhou: CentralId={CentralId} PGM={Pgm} Motivo={Motivo} Erro={Erro}",
                centralId, pgm, resultado.Motivo, resultado.Erro);

            // 409 quando a central esta offline ou o pedido em si e invalido (conflito de
            // estado/entrada); 502 quando a sessao existe mas a comunicacao falhou (timeout
            // ou resposta que a central enviou nao pode ser interpretada/confirmada).
            var statusCode = resultado.Motivo switch
            {
                PgmCommandFailureReason.CentralOffline => 409,
                PgmCommandFailureReason.NumeroInvalido => 400,
                _ => 502,
            };

            throw new BusinessException(resultado.Erro ?? "Falha ao enviar comando de PGM.", statusCode);
        }

        _logger.LogInformation(
            "Comando PGM concluido: CentralId={CentralId} PGM={Pgm} EstadoConfirmado={EstadoConfirmado}",
            centralId, pgm, resultado.EstadoConfirmado);

        return new PgmCommandResultDto
        {
            Pgm = pgm,
            Sucesso = true,
            EstadoConfirmado = resultado.EstadoConfirmado,
        };
    }
}
