using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers.Stubs;

/// <summary>4.6 - Comando de inibir zonas (bitmap de 13 bytes) como superusuario.</summary>
public sealed class ZoneCommandHandlerStub : StubCommandHandlerBase
{
    protected override IReadOnlySet<byte> ComandosSuportados { get; } = new HashSet<byte> { (byte)JflCommand.InibirZonas };

    public ZoneCommandHandlerStub(ILogger<ZoneCommandHandlerStub> logger) : base(logger)
    {
    }
}
