using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers.Stubs;

/// <summary>
/// Base para comandos de negocio reconhecidos pelo protocolo mas ainda nao
/// implementados nesta base de infraestrutura (PGM, zonas, arme, usuarios, status
/// completo, eventos, data/hora). Cada stub apenas loga o recebimento e nao
/// responde — e o ponto de extensao onde a implementacao real deve entrar depois.
/// </summary>
/// <remarks>
/// Atencao ao priorizar a implementacao real: o comando de evento (0x24) e
/// "mandatorio" no protocolo — enquanto ele continuar como stub (sem ACK), o
/// equipamento vai reenviar o mesmo evento ate 3 vezes e, sem resposta, encerrar e
/// reabrir a conexao (secao 3.3/3.4 do protocolo), gerando reconexoes desnecessarias
/// em producao assim que houver eventos reais para reportar.
/// </remarks>
public abstract class StubCommandHandlerBase : IJflCommandHandler
{
    private readonly ILogger _logger;

    protected StubCommandHandlerBase(ILogger logger)
    {
        _logger = logger;
    }

    protected abstract IReadOnlySet<byte> ComandosSuportados { get; }

    public bool CanHandle(byte cmd) => ComandosSuportados.Contains(cmd);

    public Task HandleAsync(JflSession session, JflPacket packet, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Comando 0x{Cmd:X2} recebido de {NumeroSerie} ({RemoteEndPoint}) — ainda nao implementado (stub). Dados=[{Dados}]",
            packet.Cmd, session.NumeroSerie ?? "desconhecida", session.RemoteEndPoint, Convert.ToHexString(packet.Dados));

        return Task.CompletedTask;
    }
}
