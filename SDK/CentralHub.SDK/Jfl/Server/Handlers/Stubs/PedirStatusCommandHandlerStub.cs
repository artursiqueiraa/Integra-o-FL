using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers.Stubs;

/// <summary>3.2 - Comando de pedir status (opcional). Resposta com SINAL/PROB/partições/PGM/eletrificador fica para depois.</summary>
public sealed class PedirStatusCommandHandlerStub : StubCommandHandlerBase
{
    protected override IReadOnlySet<byte> ComandosSuportados { get; } = new HashSet<byte> { (byte)JflCommand.PedirStatus };

    public PedirStatusCommandHandlerStub(ILogger<PedirStatusCommandHandlerStub> logger) : base(logger)
    {
    }
}
