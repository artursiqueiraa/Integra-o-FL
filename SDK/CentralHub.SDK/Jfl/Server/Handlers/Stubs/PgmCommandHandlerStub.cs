using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers.Stubs;

/// <summary>
/// 4.4/4.5 - Acionar e desacionar PGM como superusuario. O envio real e feito por
/// <see cref="PgmCommandService"/>, que usa <see cref="JflSession.SendAndWaitAsync"/>
/// e correlaciona a resposta pelo SEQ antes dela chegar ate o dispatcher normal —
/// por isso este handler so ve pacotes 0x50/0x51 orfaos (ex.: resposta que chegou
/// depois do timeout do chamador).
/// </summary>
public sealed class PgmCommandHandlerStub : StubCommandHandlerBase
{
    protected override IReadOnlySet<byte> ComandosSuportados { get; } = new HashSet<byte>
    {
        (byte)JflCommand.AcionarPgm,
        (byte)JflCommand.DesacionarPgm,
    };

    public PgmCommandHandlerStub(ILogger<PgmCommandHandlerStub> logger) : base(logger)
    {
    }
}
