using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers.Stubs;

/// <summary>
/// 4.1 - Comando de status como superusuario (resposta completa da tela monitorar,
/// item 4.10). A consulta de status "de verdade" e feita por
/// <see cref="CentralStatusQueryService"/>, que envia o 0x4D via
/// <see cref="JflSession.SendAndWaitAsync"/> e correlaciona a resposta pelo SEQ
/// antes dela chegar ate o dispatcher normal — por isso este handler so ve pacotes
/// 0x4D orfaos (ex.: resposta que chegou depois do timeout do chamador).
/// </summary>
public sealed class StatusCommandHandlerStub : StubCommandHandlerBase
{
    protected override IReadOnlySet<byte> ComandosSuportados { get; } = new HashSet<byte> { (byte)JflCommand.Status };

    public StatusCommandHandlerStub(ILogger<StatusCommandHandlerStub> logger) : base(logger)
    {
    }
}
