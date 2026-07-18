using CentralHub.SDK.Jfl.Protocol;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Jfl.Server.Handlers.Stubs;

/// <summary>
/// 5.x - Comandos remotos com senha da linha Active, todos multiplexados sob o CMD
/// 0x37: armar/desarmar (sub-comando 0xC1), inibir/desinibir zonas (0xC3/0xCF),
/// PGM (0xC7), consulta de usuario (0xC8) e programar senha/atributos (0xC9). A
/// leitura do sub-comando e o roteamento para cada operacao de negocio ficam para
/// uma implementacao futura — aqui so o envelope 0x37 e reconhecido.
/// </summary>
public sealed class PasswordCommandHandlerStub : StubCommandHandlerBase
{
    protected override IReadOnlySet<byte> ComandosSuportados { get; } = new HashSet<byte> { (byte)JflCommand.ComSenha };

    public PasswordCommandHandlerStub(ILogger<PasswordCommandHandlerStub> logger) : base(logger)
    {
    }
}
