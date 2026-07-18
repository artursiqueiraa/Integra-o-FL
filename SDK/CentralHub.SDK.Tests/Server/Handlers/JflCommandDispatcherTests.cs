using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Jfl.Server.Handlers;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Server.Handlers;

public class JflCommandDispatcherTests
{
    private sealed class HandlerDeTeste : IJflCommandHandler
    {
        private readonly byte _cmd;
        public bool Chamado { get; private set; }

        public HandlerDeTeste(byte cmd) => _cmd = cmd;

        public bool CanHandle(byte cmd) => cmd == _cmd;

        public Task HandleAsync(JflSession session, JflPacket packet, CancellationToken cancellationToken)
        {
            Chamado = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DispatchAsync_deve_chamar_o_handler_correto_pelo_CMD()
    {
        var handlerConexao = new HandlerDeTeste(0x21);
        var handlerKeepAlive = new HandlerDeTeste(0x40);
        var dispatcher = new JflCommandDispatcher([handlerConexao, handlerKeepAlive], NullLogger<JflCommandDispatcher>.Instance);

        var session = new JflSession(new MemoryStream(), "127.0.0.1:1");
        var pacote = new JflPacket { Seq = 1, Cmd = 0x40, Dados = [] };

        await dispatcher.DispatchAsync(session, pacote, CancellationToken.None);

        Assert.False(handlerConexao.Chamado);
        Assert.True(handlerKeepAlive.Chamado);
    }

    [Fact]
    public async Task DispatchAsync_sem_handler_registrado_nao_deve_lancar()
    {
        var dispatcher = new JflCommandDispatcher([], NullLogger<JflCommandDispatcher>.Instance);
        var session = new JflSession(new MemoryStream(), "127.0.0.1:1");
        var pacote = new JflPacket { Seq = 1, Cmd = 0x99, Dados = [] };

        await dispatcher.DispatchAsync(session, pacote, CancellationToken.None); // nao deve lancar
    }
}
