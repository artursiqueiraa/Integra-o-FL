using System.Diagnostics;
using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server;

public enum ZoneInhibitFailureReason
{
    /// <summary>Nao ha sessao TCP ativa para esse numero de serie (central offline/nunca conectou).</summary>
    CentralOffline,

    /// <summary>A central nao respondeu ao comando dentro do prazo.</summary>
    Timeout,

    /// <summary>A resposta chegou mas nao pode ser decodificada.</summary>
    RespostaInvalida,

    /// <summary>Alguma zona do conjunto pedido esta fora da faixa documentada (1 a 99).</summary>
    NumeroInvalido,
}

public sealed class ZoneInhibitResult
{
    public required bool Sucesso { get; init; }

    /// <summary>Estado de todas as 99 zonas apos o comando, lido de volta da resposta da central (secao 4.10) — permite ao chamador conferir qualquer zona especifica.</summary>
    public IReadOnlyList<ZoneStatus>? ZonasResultantes { get; init; }

    public ZoneInhibitFailureReason? Motivo { get; init; }

    public string? Erro { get; init; }

    public static ZoneInhibitResult Ok(IReadOnlyList<ZoneStatus> zonasResultantes) => new() { Sucesso = true, ZonasResultantes = zonasResultantes };

    public static ZoneInhibitResult Falha(ZoneInhibitFailureReason motivo, string erro) =>
        new() { Sucesso = false, Motivo = motivo, Erro = erro };
}

/// <summary>
/// Envia o comando oficial de Inibir Zonas como superusuario (secao 4.6, CMD 0x52) usando a
/// sessao TCP ja aberta e registrada no <see cref="SessionManager"/> — nunca disca para fora,
/// mesmo padrao ja homologado por <see cref="PgmCommandService"/>/<see cref="ArmCommandService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Semantica de substituicao, nao soma:</b> cada pacote 0x52 carrega o conjunto COMPLETO de
/// zonas que devem ficar inibidas depois dele (confirmado por sequencia de exemplos do manual
/// que redefinem o estado inteiro a cada envio, nunca acumulam). Por isso o unico metodo aqui e
/// <see cref="InibirZonasAsync"/>, que recebe o conjunto final desejado — a logica de "pegar o
/// estado atual e somar/tirar uma zona" fica no Backend (que ja tem acesso a
/// <c>CentralStatusQueryService</c> para consultar o estado atual antes de montar o novo
/// conjunto).
/// </para>
/// <para>
/// <b>Convencao de bits — ATENCAO, e diferente do campo P-INIB da resposta de status:</b> o
/// payload deste comando usa bit mais significativo (bit 7) = zona menor do byte; bit menos
/// significativo (bit 0) = zona maior. Confirmado com 3 exemplos reais do manual:
/// <c>80 00...00</c> = inibir zona 1; <c>F0 00...00</c> = inibir zonas 1-4; <c>FF 80 00...</c> =
/// inibir zonas 1-9. Isso e o OPOSTO da convencao do campo P-INIB (permissao de inibir) da
/// resposta 4.10, que e LSB-first — reaproveitar aquela logica de bit aqui inibiria a zona
/// errada silenciosamente (ver <c>Documentation/Protocol/11_ZONES.md</c>).
/// </para>
/// </remarks>
public sealed class ZoneInhibitCommandService
{
    private const int ZonaMinima = 1;
    private const int ZonaMaxima = CentralStatusResponse.QuantidadeZonas;
    private const int TamanhoBitmap = 13;
    private static readonly TimeSpan TimeoutPadrao = TimeSpan.FromSeconds(10);

    private readonly SessionManager _sessionManager;
    private readonly ILogger<ZoneInhibitCommandService> _logger;

    public ZoneInhibitCommandService(SessionManager sessionManager, ILogger<ZoneInhibitCommandService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Envia o comando 0x52 com o bitmap das zonas que devem ficar inibidas — este conjunto
    /// SUBSTITUI o estado de inibicao anterior por completo (ver remarks da classe).
    /// </summary>
    public async Task<ZoneInhibitResult> InibirZonasAsync(
        string numeroSerie, IReadOnlySet<int> zonasQueDevemFicarInibidas, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        foreach (var zona in zonasQueDevemFicarInibidas)
        {
            if (zona is < ZonaMinima or > ZonaMaxima)
            {
                return ZoneInhibitResult.Falha(
                    ZoneInhibitFailureReason.NumeroInvalido, $"Numero de zona invalido: {zona}. Deve estar entre {ZonaMinima} e {ZonaMaxima}.");
            }
        }

        if (!_sessionManager.TryGet(numeroSerie, out var session) || session is null)
        {
            _logger.LogWarning("Comando de inibir zonas para {NumeroSerie} falhou: sem sessao ativa (central offline)", numeroSerie);
            return ZoneInhibitResult.Falha(
                ZoneInhibitFailureReason.CentralOffline, $"Central {numeroSerie} nao possui sessao ativa (offline).");
        }

        var dados = EmpacotarBitmap(zonasQueDevemFicarInibidas);
        var cronometro = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Enviando comando de inibir zonas: Central={NumeroSerie} Cmd=0x{Cmd:X2} QuantidadeZonas={QuantidadeZonas} BytesEnviados={BytesEnviados}",
                numeroSerie, (byte)JflCommand.InibirZonas, zonasQueDevemFicarInibidas.Count, dados.Length + 5);

            var pacoteResposta = await session
                .SendAndWaitAsync((byte)JflCommand.InibirZonas, dados, timeout ?? TimeoutPadrao, cancellationToken)
                .ConfigureAwait(false);

            cronometro.Stop();

            var status = CentralStatusResponse.Parse(pacoteResposta.Dados);

            _logger.LogInformation(
                "Resposta do comando de inibir zonas: Central={NumeroSerie} Seq=0x{Seq:X2} BytesRecebidos={BytesRecebidos} TempoRespostaMs={TempoMs}",
                numeroSerie, pacoteResposta.Seq, pacoteResposta.Dados.Length + 5, cronometro.ElapsedMilliseconds);

            return ZoneInhibitResult.Ok(status.Zonas);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            cronometro.Stop();
            _logger.LogWarning(
                "Timeout aguardando resposta do comando de inibir zonas da central {NumeroSerie} apos {TempoMs}ms",
                numeroSerie, cronometro.ElapsedMilliseconds);
            return ZoneInhibitResult.Falha(ZoneInhibitFailureReason.Timeout, "A central nao respondeu ao comando de inibir zonas a tempo.");
        }
        catch (JflProtocolException ex)
        {
            cronometro.Stop();
            _logger.LogError(ex, "Resposta invalida ao comando de inibir zonas da central {NumeroSerie}", numeroSerie);
            return ZoneInhibitResult.Falha(ZoneInhibitFailureReason.RespostaInvalida, ex.Message);
        }
    }

    /// <summary>
    /// Empacota o bitmap MSB-first de 13 bytes (99 zonas) — bit 7 do byte = zona menor,
    /// bit 0 = zona maior (ver remarks da classe: convencao OPOSTA a do campo P-INIB).
    /// </summary>
    private static byte[] EmpacotarBitmap(IReadOnlySet<int> zonas)
    {
        var bitmap = new byte[TamanhoBitmap];

        foreach (var zona in zonas)
        {
            var byteIndex = (zona - 1) / 8;
            var bit = 7 - ((zona - 1) % 8);
            bitmap[byteIndex] |= (byte)(1 << bit);
        }

        return bitmap;
    }
}
