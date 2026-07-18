using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers.Stubs;

/// <summary>4.9 - Comando de atualizar a data e hora da central.</summary>
public sealed class AtualizarDataHoraCommandHandlerStub : StubCommandHandlerBase
{
    protected override IReadOnlySet<byte> ComandosSuportados { get; } = new HashSet<byte> { (byte)JflCommand.AtualizarDataHora };

    public AtualizarDataHoraCommandHandlerStub(ILogger<AtualizarDataHoraCommandHandlerStub> logger) : base(logger)
    {
    }
}
