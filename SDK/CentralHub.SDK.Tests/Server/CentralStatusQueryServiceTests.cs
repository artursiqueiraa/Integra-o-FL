using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Tests.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Server;

public class CentralStatusQueryServiceTests
{
    [Fact]
    public async Task ConsultarAsync_sem_sessao_ativa_deve_retornar_falha_CentralOffline()
    {
        var sessionManager = new SessionManager(NullLogger<SessionManager>.Instance);
        var service = new CentralStatusQueryService(sessionManager, NullLogger<CentralStatusQueryService>.Instance);

        var resultado = await service.ConsultarAsync("0000000000", CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(CentralStatusQueryFailureReason.CentralOffline, resultado.Motivo);
        Assert.Null(resultado.Status);
    }

    [Fact]
    public async Task ConsultarAsync_deve_enviar_0x4D_na_sessao_registrada_e_parsear_a_resposta()
    {
        var sessionManager = new SessionManager(NullLogger<SessionManager>.Instance);
        var service = new CentralStatusQueryService(sessionManager, NullLogger<CentralStatusQueryService>.Instance);

        var stream = new DuplexMemoryStream([]);
        var session = new JflSession(stream, "127.0.0.1:1") { NumeroSerie = "1234567890" };
        sessionManager.Registrar(session);

        var tarefaConsulta = service.ConsultarAsync("1234567890", CancellationToken.None);

        // A essa altura o comando 0x4D ja deve ter sido escrito no stream (sem I/O real de socket envolvido).
        var enviado = stream.SaidaComoArray();
        Assert.Equal(0x4D, enviado[3]);
        Assert.Equal(5, enviado.Length); // CAB QDE SEQ CMD K -- sem dados no pedido de status

        var seqEnviado = enviado[2];
        var respostaMinimaValida = new byte[113]; // todos os campos zerados = pacote sintaticamente valido
        session.TryCompletePendingRequest(new JflPacket { Seq = seqEnviado, Cmd = 0x4D, Dados = respostaMinimaValida });

        var resultado = await tarefaConsulta;

        Assert.True(resultado.Sucesso);
        Assert.NotNull(resultado.Status);
        Assert.Equal(16, resultado.Status!.Particoes.Count);
        Assert.Equal(99, resultado.Status.Zonas.Count);
        Assert.Equal(16, resultado.Status.Pgms.Count);
    }

    [Fact]
    public async Task ConsultarAsync_com_resposta_curta_demais_deve_retornar_falha_RespostaInvalida()
    {
        var sessionManager = new SessionManager(NullLogger<SessionManager>.Instance);
        var service = new CentralStatusQueryService(sessionManager, NullLogger<CentralStatusQueryService>.Instance);

        var stream = new DuplexMemoryStream([]);
        var session = new JflSession(stream, "127.0.0.1:1") { NumeroSerie = "1234567890" };
        sessionManager.Registrar(session);

        var tarefaConsulta = service.ConsultarAsync("1234567890", CancellationToken.None);
        var seqEnviado = stream.SaidaComoArray()[2];

        session.TryCompletePendingRequest(new JflPacket { Seq = seqEnviado, Cmd = 0x4D, Dados = [0x01, 0x02] });

        var resultado = await tarefaConsulta;

        Assert.False(resultado.Sucesso);
        Assert.Equal(CentralStatusQueryFailureReason.RespostaInvalida, resultado.Motivo);
    }

    [Fact]
    public async Task ConsultarAsync_deve_retornar_falha_Timeout_quando_a_central_nao_responde()
    {
        var sessionManager = new SessionManager(NullLogger<SessionManager>.Instance);
        var service = new CentralStatusQueryService(sessionManager, NullLogger<CentralStatusQueryService>.Instance);

        var stream = new DuplexMemoryStream([]);
        var session = new JflSession(stream, "127.0.0.1:1") { NumeroSerie = "1234567890" };
        sessionManager.Registrar(session);

        var resultado = await service.ConsultarAsync("1234567890", CancellationToken.None, timeout: TimeSpan.FromMilliseconds(50));

        Assert.False(resultado.Sucesso);
        Assert.Equal(CentralStatusQueryFailureReason.Timeout, resultado.Motivo);
    }
}
