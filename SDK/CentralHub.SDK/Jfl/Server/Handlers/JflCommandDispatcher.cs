using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers;

/// <summary>Roteia cada pacote recebido para o <see cref="IJflCommandHandler"/> responsavel pelo seu CMD.</summary>
public sealed class JflCommandDispatcher
{
    private readonly IReadOnlyList<IJflCommandHandler> _handlers;
    private readonly ILogger<JflCommandDispatcher> _logger;

    public JflCommandDispatcher(IEnumerable<IJflCommandHandler> handlers, ILogger<JflCommandDispatcher> logger)
    {
        _handlers = handlers.ToList();
        _logger = logger;
    }

    public async Task DispatchAsync(JflSession session, JflPacket packet, CancellationToken cancellationToken)
    {
        var handler = _handlers.FirstOrDefault(h => h.CanHandle(packet.Cmd));

        if (handler is null)
        {
            _logger.LogWarning(
                "Nenhum handler registrado para o comando 0x{Cmd:X2} (central {NumeroSerie}, {RemoteEndPoint})",
                packet.Cmd, session.NumeroSerie ?? "desconhecida", session.RemoteEndPoint);
            return;
        }

        await handler.HandleAsync(session, packet, cancellationToken).ConfigureAwait(false);
    }
}
