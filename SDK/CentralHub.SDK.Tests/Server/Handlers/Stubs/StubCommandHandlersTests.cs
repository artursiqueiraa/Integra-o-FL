using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Jfl.Server.Handlers;
using CentralHub.SDK.Jfl.Server.Handlers.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Server.Handlers.Stubs;

public class StubCommandHandlersTests
{
    [Fact]
    public void CanHandle_de_cada_stub_deve_cobrir_exatamente_os_comandos_esperados()
    {
        AssertComandos(new PedirStatusCommandHandlerStub(NullLogger<PedirStatusCommandHandlerStub>.Instance), 0x93);
        AssertComandos(new EventoCommandHandlerStub(NullLogger<EventoCommandHandlerStub>.Instance), 0x24);
        AssertComandos(new StatusCommandHandlerStub(NullLogger<StatusCommandHandlerStub>.Instance), 0x4D);
        AssertComandos(new ArmCommandHandlerStub(NullLogger<ArmCommandHandlerStub>.Instance), 0x4E, 0x4F, 0x53, 0x54);
        AssertComandos(new PgmCommandHandlerStub(NullLogger<PgmCommandHandlerStub>.Instance), 0x50, 0x51);
        AssertComandos(new ZoneCommandHandlerStub(NullLogger<ZoneCommandHandlerStub>.Instance), 0x52);
        AssertComandos(new AtualizarDataHoraCommandHandlerStub(NullLogger<AtualizarDataHoraCommandHandlerStub>.Instance), 0x55);
        AssertComandos(new PasswordCommandHandlerStub(NullLogger<PasswordCommandHandlerStub>.Instance), 0x37);
    }

    private static void AssertComandos(IJflCommandHandler handler, params byte[] comandosEsperados)
    {
        foreach (var cmd in comandosEsperados)
        {
            Assert.True(handler.CanHandle(cmd), $"{handler.GetType().Name} deveria tratar 0x{cmd:X2}");
        }

        // Um comando fora da lista (0x21, sempre tratado por ConnectionCommandHandler) nunca deve ser aceito por um stub.
        Assert.False(handler.CanHandle(0x21));
    }

    [Fact]
    public async Task Stub_nao_deve_lancar_e_nao_deve_responder_nada_na_sessao()
    {
        var handler = new PgmCommandHandlerStub(NullLogger<PgmCommandHandlerStub>.Instance);
        var stream = new MemoryStream();
        var session = new JflSession(stream, "127.0.0.1:1") { NumeroSerie = "123" };
        var pacote = new JflPacket { Seq = 1, Cmd = 0x50, Dados = [0x01] };

        await handler.HandleAsync(session, pacote, CancellationToken.None);

        Assert.Equal(0, stream.Length); // nada foi escrito de volta
    }
}
