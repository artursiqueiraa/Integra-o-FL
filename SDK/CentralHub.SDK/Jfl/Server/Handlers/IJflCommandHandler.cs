using CentralHub.SDK.Jfl.Protocol;

namespace CentralHub.SDK.Jfl.Server.Handlers;

/// <summary>
/// Processa um comando especifico (ou familia de comandos) recebido em uma sessao
/// ja aberta. Cada byte de CMD do protocolo deve ter no maximo um handler capaz de
/// trata-lo (ver <see cref="JflCommandDispatcher"/>).
/// </summary>
public interface IJflCommandHandler
{
    bool CanHandle(byte cmd);

    Task HandleAsync(JflSession session, JflPacket packet, CancellationToken cancellationToken);
}
