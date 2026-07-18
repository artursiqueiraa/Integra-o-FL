using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers;

/// <summary>
/// Trata o comando de keep-alive (3.3): responde com o intervalo configurado e
/// notifica o <see cref="SessionManager"/> para que a atividade seja refletida
/// (ex.: persistida como "ultimo keep-alive" por quem estiver ouvindo os eventos).
/// </summary>
public sealed class KeepAliveCommandHandler : IJflCommandHandler
{
    private readonly SessionManager _sessionManager;
    private readonly JflServerOptions _options;
    private readonly ILogger<KeepAliveCommandHandler> _logger;

    public KeepAliveCommandHandler(SessionManager sessionManager, JflServerOptions options, ILogger<KeepAliveCommandHandler> logger)
    {
        _sessionManager = sessionManager;
        _options = options;
        _logger = logger;
    }

    public bool CanHandle(byte cmd) => cmd == (byte)JflCommand.KeepAlive;

    public async Task HandleAsync(JflSession session, JflPacket packet, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Keep-alive recebido de {NumeroSerie} ({RemoteEndPoint})", session.NumeroSerie ?? "desconhecida", session.RemoteEndPoint);

        await session.ReplyAsync(packet, packet.Cmd, new[] { _options.IntervaloKeepAliveMinutos }, cancellationToken)
            .ConfigureAwait(false);

        _sessionManager.NotificarAtividade(session);
    }
}
