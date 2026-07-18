using System.Diagnostics;
using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server;

public enum PgmCommandFailureReason
{
    /// <summary>Nao ha sessao TCP ativa para esse numero de serie (central offline/nunca conectou).</summary>
    CentralOffline,

    /// <summary>A central nao respondeu ao comando dentro do prazo.</summary>
    Timeout,

    /// <summary>A resposta chegou mas nao pode ser decodificada, ou nao confirmou o estado esperado da PGM.</summary>
    RespostaInvalida,

    /// <summary>Numero de PGM fora da faixa documentada (1 a 16).</summary>
    NumeroInvalido,
}

public sealed class PgmCommandResult
{
    public required bool Sucesso { get; init; }

    /// <summary>Estado real da PGM apos o comando, lido de volta da resposta da central (secao 4.10).</summary>
    public bool? EstadoConfirmado { get; init; }

    public PgmCommandFailureReason? Motivo { get; init; }

    public string? Erro { get; init; }

    public static PgmCommandResult Ok(bool estadoConfirmado) => new() { Sucesso = true, EstadoConfirmado = estadoConfirmado };

    public static PgmCommandResult Falha(PgmCommandFailureReason motivo, string erro) =>
        new() { Sucesso = false, Motivo = motivo, Erro = erro };
}

/// <summary>
/// Envia os comandos oficiais de PGM como superusuario (secao 4.4 "Acionar PGM" / 0x50 e
/// secao 4.5 "Desacionar PGM" / 0x51) usando a sessao TCP ja aberta e registrada no
/// <see cref="SessionManager"/> — nunca disca para fora, conforme o modelo de comunicacao
/// da JFL. "Pulso" nao e um comando de fio documentado pela JFL: e implementado aqui como
/// Acionar seguido de Desacionar apos o intervalo pedido, exatamente como um operador faria
/// manualmente com os dois comandos oficiais.
/// </summary>
/// <remarks>
/// A resposta de qualquer comando da "tela monitorar" (armar, desarmar, PGM, zonas...) usa o
/// mesmo formato da secao 4.10 — por isso o parser reaproveitado aqui e o mesmo
/// <see cref="CentralStatusResponse"/> usado pela consulta de status (0x4D). O protocolo nao
/// documenta um codigo de erro especifico para os comandos de superusuario: a forma de
/// confirmar sucesso e comparar o estado da PGM na resposta com o estado esperado.
/// </remarks>
public sealed class PgmCommandService
{
    private const int PgmMinimo = 1;
    private const int PgmMaximo = 16;
    private static readonly TimeSpan TimeoutPadrao = TimeSpan.FromSeconds(10);

    private readonly SessionManager _sessionManager;
    private readonly ILogger<PgmCommandService> _logger;

    public PgmCommandService(SessionManager sessionManager, ILogger<PgmCommandService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>Comando de acionar PGM (secao 4.4, CMD 0x50).</summary>
    public Task<PgmCommandResult> AcionarAsync(string numeroSerie, int pgmNumero, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        EnviarComandoAsync(numeroSerie, JflCommand.AcionarPgm, pgmNumero, estadoEsperado: true, cancellationToken, timeout);

    /// <summary>Comando de desacionar PGM (secao 4.5, CMD 0x51).</summary>
    public Task<PgmCommandResult> DesacionarAsync(string numeroSerie, int pgmNumero, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        EnviarComandoAsync(numeroSerie, JflCommand.DesacionarPgm, pgmNumero, estadoEsperado: false, cancellationToken, timeout);

    /// <summary>
    /// Pulso: aciona a PGM, aguarda <paramref name="duracaoMs"/> e desaciona — dois comandos
    /// oficiais em sequencia na mesma sessao, nao um terceiro comando de protocolo.
    /// </summary>
    public async Task<PgmCommandResult> PulsoAsync(
        string numeroSerie, int pgmNumero, int duracaoMs, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var resultadoAcionar = await AcionarAsync(numeroSerie, pgmNumero, cancellationToken, timeout).ConfigureAwait(false);
        if (!resultadoAcionar.Sucesso)
        {
            return resultadoAcionar;
        }

        await Task.Delay(duracaoMs, cancellationToken).ConfigureAwait(false);

        return await DesacionarAsync(numeroSerie, pgmNumero, cancellationToken, timeout).ConfigureAwait(false);
    }

    private async Task<PgmCommandResult> EnviarComandoAsync(
        string numeroSerie, JflCommand cmd, int pgmNumero, bool estadoEsperado, CancellationToken cancellationToken, TimeSpan? timeout)
    {
        if (pgmNumero is < PgmMinimo or > PgmMaximo)
        {
            return PgmCommandResult.Falha(
                PgmCommandFailureReason.NumeroInvalido, $"Numero de PGM invalido: {pgmNumero}. Deve estar entre {PgmMinimo} e {PgmMaximo}.");
        }

        if (!_sessionManager.TryGet(numeroSerie, out var session) || session is null)
        {
            _logger.LogWarning("Comando PGM para {NumeroSerie} falhou: sem sessao ativa (central offline)", numeroSerie);
            return PgmCommandResult.Falha(
                PgmCommandFailureReason.CentralOffline, $"Central {numeroSerie} nao possui sessao ativa (offline).");
        }

        var dados = new byte[] { (byte)pgmNumero };
        var cronometro = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Enviando comando PGM: Central={NumeroSerie} Cmd=0x{Cmd:X2} PGM={Pgm} BytesEnviados={BytesEnviados}",
                numeroSerie, (byte)cmd, pgmNumero, dados.Length + 5);

            var pacoteResposta = await session
                .SendAndWaitAsync((byte)cmd, dados, timeout ?? TimeoutPadrao, cancellationToken)
                .ConfigureAwait(false);

            cronometro.Stop();

            var status = CentralStatusResponse.Parse(pacoteResposta.Dados);
            var pgm = status.Pgms.FirstOrDefault(p => p.Numero == pgmNumero);
            var confirmado = pgm is not null && pgm.Acionada == estadoEsperado;

            _logger.LogInformation(
                "Resposta do comando PGM: Central={NumeroSerie} PGM={Pgm} Seq=0x{Seq:X2} BytesRecebidos={BytesRecebidos} " +
                "TempoRespostaMs={TempoMs} Resultado={Resultado}",
                numeroSerie, pgmNumero, pacoteResposta.Seq, pacoteResposta.Dados.Length + 5, cronometro.ElapsedMilliseconds,
                confirmado ? "Confirmado" : "SemConfirmacao");

            return confirmado
                ? PgmCommandResult.Ok(pgm!.Acionada)
                : PgmCommandResult.Falha(
                    PgmCommandFailureReason.RespostaInvalida,
                    $"A central nao confirmou o novo estado da PGM {pgmNumero} (sem permissao, PGM nao configurada, ou nao respondeu como esperado).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            cronometro.Stop();
            _logger.LogWarning(
                "Timeout aguardando resposta do comando PGM {Pgm} na central {NumeroSerie} apos {TempoMs}ms",
                pgmNumero, numeroSerie, cronometro.ElapsedMilliseconds);
            return PgmCommandResult.Falha(PgmCommandFailureReason.Timeout, "A central nao respondeu ao comando de PGM a tempo.");
        }
        catch (JflProtocolException ex)
        {
            cronometro.Stop();
            _logger.LogError(ex, "Resposta invalida ao comando PGM {Pgm} da central {NumeroSerie}", pgmNumero, numeroSerie);
            return PgmCommandResult.Falha(PgmCommandFailureReason.RespostaInvalida, ex.Message);
        }
    }
}
