using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Tests.TestUtilities;

namespace CentralHub.SDK.Tests.Server;

public class JflSessionSendAndWaitTests
{
    [Fact]
    public async Task SendAndWaitAsync_deve_enviar_o_comando_0x4D_sem_dados()
    {
        var stream = new DuplexMemoryStream([]);
        var session = new JflSession(stream, "127.0.0.1:1");

        var tarefa = session.SendAndWaitAsync((byte)JflCommand.Status, ReadOnlyMemory<byte>.Empty, TimeSpan.FromSeconds(5), CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(new byte[] { 0x7B, 0x05, enviado[2], 0x4D, enviado[4] }, enviado); // CAB QDE SEQ CMD K, sem dados

        // Completa a requisicao pendente para nao deixar a tarefa presa.
        session.TryCompletePendingRequest(new JflPacket { Seq = enviado[2], Cmd = 0x4D, Dados = [] });
        await tarefa;
    }

    [Fact]
    public async Task SendAndWaitAsync_deve_completar_quando_TryCompletePendingRequest_recebe_o_mesmo_SEQ()
    {
        var stream = new DuplexMemoryStream([]);
        var session = new JflSession(stream, "127.0.0.1:1");

        var tarefa = session.SendAndWaitAsync((byte)JflCommand.Status, ReadOnlyMemory<byte>.Empty, TimeSpan.FromSeconds(5), CancellationToken.None);
        var seqEnviado = stream.SaidaComoArray()[2];

        var respostaSimulada = new JflPacket { Seq = seqEnviado, Cmd = 0x4D, Dados = [0xAA, 0xBB] };
        var completou = session.TryCompletePendingRequest(respostaSimulada);

        Assert.True(completou);
        var resultado = await tarefa;
        Assert.Same(respostaSimulada, resultado);
    }

    [Fact]
    public void TryCompletePendingRequest_com_SEQ_diferente_nao_deve_completar_nada()
    {
        var stream = new DuplexMemoryStream([]);
        var session = new JflSession(stream, "127.0.0.1:1");

        _ = session.SendAndWaitAsync((byte)JflCommand.Status, ReadOnlyMemory<byte>.Empty, TimeSpan.FromSeconds(5), CancellationToken.None);

        var pacoteDeOutroSeq = new JflPacket { Seq = 0xEE, Cmd = 0x4D, Dados = [] };
        var completou = session.TryCompletePendingRequest(pacoteDeOutroSeq);

        Assert.False(completou);
    }

    [Fact]
    public async Task SendAndWaitAsync_deve_lancar_apos_o_timeout_se_ninguem_responder()
    {
        var stream = new DuplexMemoryStream([]);
        var session = new JflSession(stream, "127.0.0.1:1");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.SendAndWaitAsync((byte)JflCommand.Status, ReadOnlyMemory<byte>.Empty, TimeSpan.FromMilliseconds(50), CancellationToken.None));
    }

    [Fact]
    public async Task Close_deve_derrubar_requisicoes_pendentes_em_vez_de_deixa_las_presas_para_sempre()
    {
        var stream = new DuplexMemoryStream([]);
        var session = new JflSession(stream, "127.0.0.1:1");

        var tarefa = session.SendAndWaitAsync((byte)JflCommand.Status, ReadOnlyMemory<byte>.Empty, TimeSpan.FromSeconds(30), CancellationToken.None);

        session.Close();

        await Assert.ThrowsAsync<IOException>(() => tarefa);
    }
}
