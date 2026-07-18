using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers.Stubs;

/// <summary>
/// 3.4 - Comando de evento (mandatorio). Decodificacao do Contact ID, persistencia
/// e notificacao em tempo real ficam para uma implementacao futura.
/// </summary>
public sealed class EventoCommandHandlerStub : StubCommandHandlerBase
{
    protected override IReadOnlySet<byte> ComandosSuportados { get; } = new HashSet<byte> { (byte)JflCommand.Evento };

    public EventoCommandHandlerStub(ILogger<EventoCommandHandlerStub> logger) : base(logger)
    {
    }
}
