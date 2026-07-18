using CentralHub.Api.Data;
using CentralHub.Api.DTOs;
using CentralHub.SDK.Jfl.Server;
using Microsoft.EntityFrameworkCore;

namespace CentralHub.Api.Services;

/// <summary>
/// Envia comandos de Arme (Armar, Desarmar, Armar Stay, Armar Away) para uma Central cadastrada:
/// resolve o NumeroSerie no banco e delega ao <see cref="ArmCommandService"/> do SDK, que usa a
/// sessao TCP ja aberta pela central (via SessionManager) — nunca disca para fora. Espelha
/// exatamente <see cref="PgmService"/>.
/// </summary>
public class ArmService
{
    private readonly AppDbContext _context;
    private readonly ArmCommandService _armCommandService;
    private readonly ILogger<ArmService> _logger;

    public ArmService(AppDbContext context, ArmCommandService armCommandService, ILogger<ArmService> logger)
    {
        _context = context;
        _armCommandService = armCommandService;
        _logger = logger;
    }

    public Task<ArmCommandResultDto> ArmarAsync(int centralId, int particao, CancellationToken cancellationToken) =>
        ExecutarAsync(centralId, particao, (numeroSerie, ct) => _armCommandService.ArmarAsync(numeroSerie, particao, ct), cancellationToken);

    public Task<ArmCommandResultDto> DesarmarAsync(int centralId, int particao, CancellationToken cancellationToken) =>
        ExecutarAsync(centralId, particao, (numeroSerie, ct) => _armCommandService.DesarmarAsync(numeroSerie, particao, ct), cancellationToken);

    public Task<ArmCommandResultDto> ArmarStayAsync(int centralId, int particao, CancellationToken cancellationToken) =>
        ExecutarAsync(centralId, particao, (numeroSerie, ct) => _armCommandService.ArmarStayAsync(numeroSerie, particao, ct), cancellationToken);

    public Task<ArmCommandResultDto> ArmarAwayAsync(int centralId, int particao, CancellationToken cancellationToken) =>
        ExecutarAsync(centralId, particao, (numeroSerie, ct) => _armCommandService.ArmarAwayAsync(numeroSerie, particao, ct), cancellationToken);

    private async Task<ArmCommandResultDto> ExecutarAsync(
        int centralId, int particao, Func<string, CancellationToken, Task<ArmCommandResult>> executar, CancellationToken cancellationToken)
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

        _logger.LogInformation("Requisicao de comando de arme: CentralId={CentralId} NumeroSerie={NumeroSerie} Particao={Particao}", centralId, central.NumeroSerie, particao);

        var resultado = await executar(central.NumeroSerie, cancellationToken);

        if (!resultado.Sucesso)
        {
            _logger.LogWarning(
                "Comando de arme falhou: CentralId={CentralId} Particao={Particao} Motivo={Motivo} Erro={Erro}",
                centralId, particao, resultado.Motivo, resultado.Erro);

            var statusCode = resultado.Motivo switch
            {
                ArmCommandFailureReason.CentralOffline => 409,
                ArmCommandFailureReason.NumeroInvalido => 400,
                _ => 502,
            };

            throw new BusinessException(resultado.Erro ?? "Falha ao enviar comando de arme.", statusCode);
        }

        _logger.LogInformation(
            "Comando de arme concluido: CentralId={CentralId} Particao={Particao} EstadoConfirmado={EstadoConfirmado}",
            centralId, particao, resultado.EstadoConfirmado);

        return new ArmCommandResultDto
        {
            Particao = particao,
            Sucesso = true,
            EstadoConfirmado = resultado.EstadoConfirmado,
        };
    }
}
