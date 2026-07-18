using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Jfl.Server.Handlers;
using CentralHub.SDK.Tests.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Server.Handlers;

public class ConnectionCommandHandlerTests
{
    private static readonly byte[] DadosConexaoMinimos = BuildDadosConexao("0000000001", modelo: 0xA4);

    private static byte[] BuildDadosConexao(string numeroSerie, byte modelo)
    {
        var dados = new List<byte>();
        dados.AddRange(System.Text.Encoding.ASCII.GetBytes(numeroSerie)); // NS (10)
        dados.AddRange(Enumerable.Repeat((byte)0xFF, 15)); // IMEI vazio
        dados.AddRange(Enumerable.Repeat((byte)0xFF, 12)); // MAC vazio
        dados.Add(modelo); // MOD
        dados.AddRange(System.Text.Encoding.ASCII.GetBytes("400")); // VER = 4.0
        dados.Add(0x01); // IP
        dados.Add(0x03); // SIMCARD (nao existe)
        dados.Add(0x01); // VIA = Ethernet
        dados.Add(0x06); // OPE = nao existe
        return dados.ToArray();
    }

    private static (JflSession session, DuplexMemoryStream stream) NovaSessaoComRequisicao(byte[] dadosConexao, byte seq = 0x01, byte cmd = 0x21)
    {
        var pacoteRequisicao = PacketBuilder.Build(seq, cmd, dadosConexao);
        var stream = new DuplexMemoryStream(pacoteRequisicao);
        var session = new JflSession(stream, "127.0.0.1:9999");
        return (session, stream);
    }

    [Fact]
    public async Task HandleAsync_deve_registrar_a_sessao_e_responder_liberado_quando_autorizado()
    {
        var sessionManager = new SessionManager(NullLogger<SessionManager>.Instance);
        var options = new JflServerOptions { IntervaloKeepAliveMinutos = 7 };
        var handler = new ConnectionCommandHandler(
            sessionManager, new LiberarTodasCentraisAuthorizationProvider(), options, NullLogger<ConnectionCommandHandler>.Instance);

        var (session, stream) = NovaSessaoComRequisicao(DadosConexaoMinimos);
        var pacoteRecebido = await session.ReceiveAsync(CancellationToken.None);

        await handler.HandleAsync(session, pacoteRecebido!, CancellationToken.None);

        Assert.Equal(JflSessionState.Ativa, session.State);
        Assert.Equal("0000000001", session.NumeroSerie);
        Assert.Equal((byte)JflModel.Active100Bus, session.Modelo);
        Assert.True(sessionManager.TryGet("0000000001", out var registrada));
        Assert.Same(session, registrada);

        var respostaEsperada = PacketBuilder.Build(pacoteRecebido!.Seq, 0x21, [0x01, 7]); // Liberado + keep-alive 7 min
        Assert.Equal(respostaEsperada, stream.SaidaComoArray());
    }

    [Fact]
    public async Task HandleAsync_nao_deve_registrar_a_sessao_quando_nao_autorizado()
    {
        var sessionManager = new SessionManager(NullLogger<SessionManager>.Instance);
        var options = new JflServerOptions { IntervaloKeepAliveMinutos = 5 };
        var handler = new ConnectionCommandHandler(
            sessionManager, new BloquearTodasCentraisAuthorizationProvider(), options, NullLogger<ConnectionCommandHandler>.Instance);

        var (session, stream) = NovaSessaoComRequisicao(DadosConexaoMinimos);
        var pacoteRecebido = await session.ReceiveAsync(CancellationToken.None);

        await handler.HandleAsync(session, pacoteRecebido!, CancellationToken.None);

        Assert.Equal(JflSessionState.Conectando, session.State); // nunca chegou a ficar Ativa
        Assert.False(sessionManager.TryGet("0000000001", out _));

        var respostaEsperada = PacketBuilder.Build(pacoteRecebido!.Seq, 0x21, [0x00, 5]); // Bloqueado
        Assert.Equal(respostaEsperada, stream.SaidaComoArray());
    }

    [Fact]
    public void CanHandle_deve_aceitar_0x21_e_0x2A()
    {
        var handler = new ConnectionCommandHandler(
            new SessionManager(NullLogger<SessionManager>.Instance),
            new LiberarTodasCentraisAuthorizationProvider(),
            new JflServerOptions(),
            NullLogger<ConnectionCommandHandler>.Instance);

        Assert.True(handler.CanHandle(0x21));
        Assert.True(handler.CanHandle(0x2A));
        Assert.False(handler.CanHandle(0x40));
    }

    private sealed class BloquearTodasCentraisAuthorizationProvider : ICentralAuthorizationProvider
    {
        public Task<bool> EstaLiberadaAsync(string numeroSerie, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
