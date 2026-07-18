using System.Diagnostics;
using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server;

public enum ArmCommandFailureReason
{
    /// <summary>Nao ha sessao TCP ativa para esse numero de serie (central offline/nunca conectou).</summary>
    CentralOffline,

    /// <summary>A central nao respondeu ao comando dentro do prazo.</summary>
    Timeout,

    /// <summary>A resposta chegou mas nao pode ser decodificada, ou nao confirmou o estado esperado da particao/eletrificador.</summary>
    RespostaInvalida,

    /// <summary>Numero de particao fora da faixa documentada (1 a 16, ou 99 para o eletrificador).</summary>
    NumeroInvalido,
}

public sealed class ArmCommandResult
{
    public required bool Sucesso { get; init; }

    /// <summary>Estado real (armada/armado ou desarmada/desarmado) apos o comando, lido de volta da resposta da central (secao 4.10).</summary>
    public bool? EstadoConfirmado { get; init; }

    public ArmCommandFailureReason? Motivo { get; init; }

    public string? Erro { get; init; }

    public static ArmCommandResult Ok(bool estadoConfirmado) => new() { Sucesso = true, EstadoConfirmado = estadoConfirmado };

    public static ArmCommandResult Falha(ArmCommandFailureReason motivo, string erro) =>
        new() { Sucesso = false, Motivo = motivo, Erro = erro };
}

/// <summary>
/// Envia os comandos oficiais de Arme como superusuario (secao 4.2 "Armar" / 0x4E, 4.3
/// "Desarmar" / 0x4F, 4.7 "Armar Stay" / 0x53, 4.8 "Armar Away" / 0x54) usando a sessao TCP
/// ja aberta e registrada no <see cref="SessionManager"/> — nunca disca para fora, mesmo
/// padrao ja homologado por <see cref="PgmCommandService"/>.
/// </summary>
/// <remarks>
/// A resposta de qualquer comando da "tela monitorar" (armar, desarmar, PGM, zonas...) usa o
/// mesmo formato da secao 4.10 — por isso o parser reaproveitado aqui e o mesmo
/// <see cref="CentralStatusResponse"/> usado pela consulta de status (0x4D) e pelo PGM.
/// A particao <c>99</c> e um valor especial documentado que opera o eletrificador como se
/// fosse uma particao (confirmado por captura real: "COMANDO DE ARMAR O ELETRIFICADOR",
/// <c>7B 06 03 4E 63 53</c>) — nao e um bug de validacao, e intencional. O eletrificador nao
/// tem um modo "Stay" documentado, entao <see cref="ArmarStayAsync"/> nao aceita particao 99.
/// </remarks>
public sealed class ArmCommandService
{
    private const int ParticaoMinima = 1;
    private const int ParticaoMaxima = 16;
    private const int ParticaoEletrificador = 99;
    private static readonly TimeSpan TimeoutPadrao = TimeSpan.FromSeconds(10);

    private readonly SessionManager _sessionManager;
    private readonly ILogger<ArmCommandService> _logger;

    public ArmCommandService(SessionManager sessionManager, ILogger<ArmCommandService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>Comando de armar (secao 4.2, CMD 0x4E). Aceita particao 1-16 ou 99 (eletrificador).</summary>
    public Task<ArmCommandResult> ArmarAsync(string numeroSerie, int particao, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        EnviarComandoAsync(numeroSerie, JflCommand.Armar, particao, estadoEsperadoArmado: true, permiteEletrificador: true, cancellationToken, timeout);

    /// <summary>Comando de desarmar (secao 4.3, CMD 0x4F). Aceita particao 1-16 ou 99 (eletrificador).</summary>
    public Task<ArmCommandResult> DesarmarAsync(string numeroSerie, int particao, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        EnviarComandoAsync(numeroSerie, JflCommand.Desarmar, particao, estadoEsperadoArmado: false, permiteEletrificador: true, cancellationToken, timeout);

    /// <summary>Comando de armar Stay (secao 4.7, CMD 0x53). Nao se aplica ao eletrificador (99 nao e aceito).</summary>
    public Task<ArmCommandResult> ArmarStayAsync(string numeroSerie, int particao, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        EnviarComandoAsync(numeroSerie, JflCommand.ArmarStay, particao, estadoEsperadoArmado: true, permiteEletrificador: false, cancellationToken, timeout);

    /// <summary>
    /// Comando de armar Away (secao 4.8, CMD 0x54). Aceita particao 1-16 ou 99 (eletrificador).
    /// No fio, "Armada Away" confirma pelo mesmo <see cref="PartitionState.Armada"/> do arme
    /// normal — o protocolo nao documenta um estado "ArmadaAway" separado (ver
    /// <c>Documentation/Protocol/10_ARM.md</c>).
    /// </summary>
    public Task<ArmCommandResult> ArmarAwayAsync(string numeroSerie, int particao, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        EnviarComandoAsync(numeroSerie, JflCommand.ArmarAway, particao, estadoEsperadoArmado: true, permiteEletrificador: true, cancellationToken, timeout);

    private async Task<ArmCommandResult> EnviarComandoAsync(
        string numeroSerie, JflCommand cmd, int particao, bool estadoEsperadoArmado, bool permiteEletrificador,
        CancellationToken cancellationToken, TimeSpan? timeout)
    {
        var ehEletrificador = particao == ParticaoEletrificador;
        var faixaValida = particao is >= ParticaoMinima and <= ParticaoMaxima;

        if (!faixaValida && !(ehEletrificador && permiteEletrificador))
        {
            var faixaDescricao = permiteEletrificador ? $"{ParticaoMinima} a {ParticaoMaxima}, ou 99 (eletrificador)" : $"{ParticaoMinima} a {ParticaoMaxima}";
            return ArmCommandResult.Falha(
                ArmCommandFailureReason.NumeroInvalido, $"Numero de particao invalido: {particao}. Deve estar entre {faixaDescricao}.");
        }

        if (!_sessionManager.TryGet(numeroSerie, out var session) || session is null)
        {
            _logger.LogWarning("Comando de arme para {NumeroSerie} falhou: sem sessao ativa (central offline)", numeroSerie);
            return ArmCommandResult.Falha(
                ArmCommandFailureReason.CentralOffline, $"Central {numeroSerie} nao possui sessao ativa (offline).");
        }

        var dados = new byte[] { (byte)particao };
        var cronometro = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Enviando comando de arme: Central={NumeroSerie} Cmd=0x{Cmd:X2} Particao={Particao} BytesEnviados={BytesEnviados}",
                numeroSerie, (byte)cmd, particao, dados.Length + 5);

            var pacoteResposta = await session
                .SendAndWaitAsync((byte)cmd, dados, timeout ?? TimeoutPadrao, cancellationToken)
                .ConfigureAwait(false);

            cronometro.Stop();

            var status = CentralStatusResponse.Parse(pacoteResposta.Dados);
            bool confirmado;

            if (ehEletrificador)
            {
                var estadoEletrificador = status.Eletrificador.Estado;
                confirmado = estadoEsperadoArmado
                    ? estadoEletrificador == ElectrifierState.Armado
                    : estadoEletrificador == ElectrifierState.Desarmado;
            }
            else
            {
                var estadoEsperado = cmd switch
                {
                    JflCommand.ArmarStay => PartitionState.ArmadaStay,
                    JflCommand.Desarmar => PartitionState.Desarmada,
                    _ => PartitionState.Armada, // Armar, ArmarAway
                };

                var particaoStatus = status.Particoes.FirstOrDefault(p => p.Numero == particao);
                confirmado = particaoStatus is not null && particaoStatus.Estado == estadoEsperado;
            }

            _logger.LogInformation(
                "Resposta do comando de arme: Central={NumeroSerie} Particao={Particao} Seq=0x{Seq:X2} BytesRecebidos={BytesRecebidos} " +
                "TempoRespostaMs={TempoMs} Resultado={Resultado}",
                numeroSerie, particao, pacoteResposta.Seq, pacoteResposta.Dados.Length + 5, cronometro.ElapsedMilliseconds,
                confirmado ? "Confirmado" : "SemConfirmacao");

            return confirmado
                ? ArmCommandResult.Ok(estadoEsperadoArmado)
                : ArmCommandResult.Falha(
                    ArmCommandFailureReason.RespostaInvalida,
                    $"A central nao confirmou o novo estado da particao {particao} (sem permissao, particao nao configurada, ou nao respondeu como esperado).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            cronometro.Stop();
            _logger.LogWarning(
                "Timeout aguardando resposta do comando de arme na particao {Particao} da central {NumeroSerie} apos {TempoMs}ms",
                particao, numeroSerie, cronometro.ElapsedMilliseconds);
            return ArmCommandResult.Falha(ArmCommandFailureReason.Timeout, "A central nao respondeu ao comando de arme a tempo.");
        }
        catch (JflProtocolException ex)
        {
            cronometro.Stop();
            _logger.LogError(ex, "Resposta invalida ao comando de arme na particao {Particao} da central {NumeroSerie}", particao, numeroSerie);
            return ArmCommandResult.Falha(ArmCommandFailureReason.RespostaInvalida, ex.Message);
        }
    }
}
