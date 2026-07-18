using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Jfl.Server.Handlers;
using CentralHub.SDK.Tests.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Server.Handlers;

public class KeepAliveCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_deve_responder_com_o_intervalo_configurado_e_ecoar_o_seq_recebido()
    {
        var sessionManager = new SessionManager(NullLogger<SessionManager>.Instance);
        var options = new JflServerOptions { IntervaloKeepAliveMinutos = 10 };
        var handler = new KeepAliveCommandHandler(sessionManager, options, NullLogger<KeepAliveCommandHandler>.Instance);

        // Pacote real do manual: TX[5]=7B 05 18 40 26 (keep-alive, seq 0x18).
        var pacoteRequisicao = PacketBuilder.Build(seq: 0x18, cmd: 0x40, dados: ReadOnlySpan<byte>.Empty);
        var stream = new DuplexMemoryStream(pacoteRequisicao);
        var session = new JflSession(stream, "127.0.0.1:9999") { NumeroSerie = "1111111111" };

        var pacoteRecebido = await session.ReceiveAsync(CancellationToken.None);
        await handler.HandleAsync(session, pacoteRecebido!, CancellationToken.None);

        // RX[6]=7B 06 18 40 0A <checksum> (KEEP=10 minutos, nao 0x00 do exemplo original de 1 min).
        var respostaEsperada = PacketBuilder.Build(seq: 0x18, cmd: 0x40, dados: [10]);
        Assert.Equal(respostaEsperada, stream.SaidaComoArray());
    }

    [Fact]
    public async Task HandleAsync_com_intervalo_padrao_do_manual_deve_bater_byte_a_byte_com_a_captura_real()
    {
        var sessionManager = new SessionManager(NullLogger<SessionManager>.Instance);
        var options = new JflServerOptions { IntervaloKeepAliveMinutos = 0x00 }; // 0x00 = 1 minuto, per manual
        var handler = new KeepAliveCommandHandler(sessionManager, options, NullLogger<KeepAliveCommandHandler>.Instance);

        var pacoteRequisicao = PacketBuilder.Build(seq: 0x18, cmd: 0x40, dados: ReadOnlySpan<byte>.Empty);
        var stream = new DuplexMemoryStream(pacoteRequisicao);
        var session = new JflSession(stream, "127.0.0.1:9999") { NumeroSerie = "1111111111" };

        var pacoteRecebido = await session.ReceiveAsync(CancellationToken.None);
        await handler.HandleAsync(session, pacoteRecebido!, CancellationToken.None);

        // RX[6]=7B 06 18 40 00 25 — captura real do manual (secao 3.5).
        Assert.Equal(new byte[] { 0x7B, 0x06, 0x18, 0x40, 0x00, 0x25 }, stream.SaidaComoArray());
    }

    [Fact]
    public async Task HandleAsync_deve_notificar_atividade_apenas_se_a_sessao_estiver_registrada()
    {
        var sessionManager = new SessionManager(NullLogger<SessionManager>.Instance);
        var notificacoes = new List<JflSession>();
        sessionManager.AtividadeAtualizada += s => notificacoes.Add(s);

        var options = new JflServerOptions();
        var handler = new KeepAliveCommandHandler(sessionManager, options, NullLogger<KeepAliveCommandHandler>.Instance);

        // Dois keep-alives concatenados no mesmo stream, simulando dois pacotes chegando na mesma sessao.
        var doisKeepAlives = PacketBuilder.Build(0x01, 0x40, ReadOnlySpan<byte>.Empty)
            .Concat(PacketBuilder.Build(0x02, 0x40, ReadOnlySpan<byte>.Empty))
            .ToArray();
        var stream = new DuplexMemoryStream(doisKeepAlives);
        var session = new JflSession(stream, "127.0.0.1:9999") { NumeroSerie = "2222222222" };

        var primeiroPacote = await session.ReceiveAsync(CancellationToken.None);

        // Sessao ainda nao foi registrada -> nao deve notificar.
        await handler.HandleAsync(session, primeiroPacote!, CancellationToken.None);
        Assert.Empty(notificacoes);

        // Apos registrar essa mesma sessao, um novo keep-alive deve notificar.
        sessionManager.Registrar(session);
        var segundoPacote = await session.ReceiveAsync(CancellationToken.None);
        await handler.HandleAsync(session, segundoPacote!, CancellationToken.None);

        Assert.Single(notificacoes);
        Assert.Same(session, notificacoes[0]);
    }
}
