using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server;

public enum CentralStatusQueryFailureReason
{
    /// <summary>Nao ha sessao TCP ativa para esse numero de serie (central offline/nunca conectou).</summary>
    CentralOffline,

    /// <summary>A central nao respondeu ao comando de status dentro do prazo.</summary>
    Timeout,

    /// <summary>A resposta chegou mas nao pode ser decodificada (pacote curto demais, etc.).</summary>
    RespostaInvalida,
}

public sealed class CentralStatusQueryResult
{
    public required bool Sucesso { get; init; }
    public CentralStatusResponse? Status { get; init; }
    public CentralStatusQueryFailureReason? Motivo { get; init; }
    public string? Erro { get; init; }

    public static CentralStatusQueryResult Ok(CentralStatusResponse status) => new() { Sucesso = true, Status = status };

    public static CentralStatusQueryResult Falha(CentralStatusQueryFailureReason motivo, string erro) =>
        new() { Sucesso = false, Motivo = motivo, Erro = erro };
}

/// <summary>
/// Consulta o status completo de uma central (comando 0x4D, secao 4.1; resposta no
/// formato da secao 4.10) usando a sessao TCP ja aberta e registrada no
/// <see cref="SessionManager"/> — nunca disca para fora, conforme o modelo de
/// comunicacao da JFL (a central e quem inicia a conexao).
/// </summary>
public sealed class CentralStatusQueryService
{
    private static readonly TimeSpan TimeoutPadrao = TimeSpan.FromSeconds(10);

    private readonly SessionManager _sessionManager;
    private readonly ILogger<CentralStatusQueryService> _logger;

    public CentralStatusQueryService(SessionManager sessionManager, ILogger<CentralStatusQueryService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<CentralStatusQueryResult> ConsultarAsync(string numeroSerie, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        if (!_sessionManager.TryGet(numeroSerie, out var session) || session is null)
        {
            _logger.LogWarning("Consulta de status para {NumeroSerie} falhou: sem sessao ativa (central offline)", numeroSerie);
            return CentralStatusQueryResult.Falha(
                CentralStatusQueryFailureReason.CentralOffline, $"Central {numeroSerie} nao possui sessao ativa (offline).");
        }

        try
        {
            _logger.LogInformation("Enviando comando de status (0x4D) para central {NumeroSerie}", numeroSerie);

            var respostaPacote = await session
                .SendAndWaitAsync((byte)JflCommand.Status, ReadOnlyMemory<byte>.Empty, timeout ?? TimeoutPadrao, cancellationToken)
                .ConfigureAwait(false);

            var status = CentralStatusResponse.Parse(respostaPacote.Dados);

            _logger.LogInformation("Status recebido da central {NumeroSerie}", numeroSerie);
            return CentralStatusQueryResult.Ok(status);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout aguardando resposta de status da central {NumeroSerie}", numeroSerie);
            return CentralStatusQueryResult.Falha(
                CentralStatusQueryFailureReason.Timeout, "A central nao respondeu ao comando de status a tempo.");
        }
        catch (JflProtocolException ex)
        {
            _logger.LogError(ex, "Resposta de status invalida da central {NumeroSerie}", numeroSerie);
            return CentralStatusQueryResult.Falha(CentralStatusQueryFailureReason.RespostaInvalida, ex.Message);
        }
    }
}
