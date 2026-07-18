using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers.Stubs;

/// <summary>4.2/4.3/4.7/4.8 - Armar, desarmar, armar STAY e armar AWAY como superusuario.</summary>
public sealed class ArmCommandHandlerStub : StubCommandHandlerBase
{
    protected override IReadOnlySet<byte> ComandosSuportados { get; } = new HashSet<byte>
    {
        (byte)JflCommand.Armar,
        (byte)JflCommand.Desarmar,
        (byte)JflCommand.ArmarStay,
        (byte)JflCommand.ArmarAway,
    };

    public ArmCommandHandlerStub(ILogger<ArmCommandHandlerStub> logger) : base(logger)
    {
    }
}
