using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Tests.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Server;

public class PgmCommandServiceTests
{
    private static SessionManager NovoSessionManager() => new(NullLogger<SessionManager>.Instance);

    private static JflSession RegistrarSessao(SessionManager sessionManager, byte[] bytesDeEntrada, string numeroSerie, out DuplexMemoryStream stream)
    {
        stream = new DuplexMemoryStream(bytesDeEntrada);
        var session = new JflSession(stream, "127.0.0.1:1") { NumeroSerie = numeroSerie };
        sessionManager.Registrar(session);
        return session;
    }

    /// <summary>Monta uma resposta 4.10 (115 bytes) com uma unica PGM no estado pedido.</summary>
    private static byte[] RespostaComPgm(int numeroPgm, bool acionada)
    {
        var dados = new byte[115];
        if (!acionada)
        {
            return dados;
        }

        if (numeroPgm <= 8)
        {
            dados[9] = (byte)(1 << (numeroPgm - 1)); // PGM 1-8
        }
        else
        {
            dados[113] = (byte)(1 << (numeroPgm - 9)); // PGM 9-16 (PGM2)
        }

        return dados;
    }

    [Fact]
    public async Task AcionarAsync_sem_sessao_ativa_deve_retornar_CentralOffline()
    {
        var sessionManager = NovoSessionManager();
        var service = new PgmCommandService(sessionManager, NullLogger<PgmCommandService>.Instance);

        var resultado = await service.AcionarAsync("0000000000", 1, CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(PgmCommandFailureReason.CentralOffline, resultado.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(-1)]
    public async Task AcionarAsync_com_numero_de_PGM_fora_da_faixa_deve_retornar_NumeroInvalido(int pgmInvalido)
    {
        var sessionManager = NovoSessionManager();
        var service = new PgmCommandService(sessionManager, NullLogger<PgmCommandService>.Instance);
        RegistrarSessao(sessionManager, [], "1234567890", out _);

        var resultado = await service.AcionarAsync("1234567890", pgmInvalido, CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(PgmCommandFailureReason.NumeroInvalido, resultado.Motivo);
    }

    [Fact]
    public async Task AcionarAsync_deve_enviar_0x50_com_o_numero_da_PGM_e_confirmar_sucesso()
    {
        var sessionManager = NovoSessionManager();
        var service = new PgmCommandService(sessionManager, NullLogger<PgmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.AcionarAsync("1234567890", pgmNumero: 3, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0x50, enviado[3]); // CMD = Acionar PGM
        Assert.Equal(3, enviado[4]); // Dados = numero da PGM
        Assert.Equal(6, enviado.Length); // CAB QDE SEQ CMD DADOS(1) K

        var seqEnviado = enviado[2];
        var resposta = new JflPacket { Seq = seqEnviado, Cmd = 0x50, Dados = RespostaComPgm(3, acionada: true) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.EstadoConfirmado);
    }

    [Fact]
    public async Task DesacionarAsync_deve_enviar_0x51_e_confirmar_sucesso()
    {
        var sessionManager = NovoSessionManager();
        var service = new PgmCommandService(sessionManager, NullLogger<PgmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.DesacionarAsync("1234567890", pgmNumero: 10, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0x51, enviado[3]);
        Assert.Equal(10, enviado[4]);

        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x51, Dados = RespostaComPgm(10, acionada: false) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.EstadoConfirmado);
    }

    [Fact]
    public async Task AcionarAsync_quando_a_central_nao_confirma_o_estado_deve_retornar_RespostaInvalida()
    {
        var sessionManager = NovoSessionManager();
        var service = new PgmCommandService(sessionManager, NullLogger<PgmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.AcionarAsync("1234567890", pgmNumero: 5, CancellationToken.None);
        var enviado = stream.SaidaComoArray();

        // Central responde mas a PGM 5 continua desligada (ex.: sem permissao) -> nao confirma.
        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x50, Dados = RespostaComPgm(5, acionada: false) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.False(resultado.Sucesso);
        Assert.Equal(PgmCommandFailureReason.RespostaInvalida, resultado.Motivo);
    }

    [Fact]
    public async Task AcionarAsync_com_resposta_curta_demais_deve_retornar_RespostaInvalida()
    {
        var sessionManager = NovoSessionManager();
        var service = new PgmCommandService(sessionManager, NullLogger<PgmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.AcionarAsync("1234567890", pgmNumero: 1, CancellationToken.None);
        var enviado = stream.SaidaComoArray();

        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x50, Dados = [0x01, 0x02] };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.False(resultado.Sucesso);
        Assert.Equal(PgmCommandFailureReason.RespostaInvalida, resultado.Motivo);
    }

    [Fact]
    public async Task AcionarAsync_deve_retornar_Timeout_quando_a_central_nao_responde()
    {
        var sessionManager = NovoSessionManager();
        var service = new PgmCommandService(sessionManager, NullLogger<PgmCommandService>.Instance);
        RegistrarSessao(sessionManager, [], "1234567890", out _);

        var resultado = await service.AcionarAsync("1234567890", 1, CancellationToken.None, timeout: TimeSpan.FromMilliseconds(50));

        Assert.False(resultado.Sucesso);
        Assert.Equal(PgmCommandFailureReason.Timeout, resultado.Motivo);
    }

    [Fact]
    public async Task PulsoAsync_deve_enviar_acionar_e_depois_desacionar_na_mesma_sessao()
    {
        var sessionManager = NovoSessionManager();
        var service = new PgmCommandService(sessionManager, NullLogger<PgmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.PulsoAsync("1234567890", pgmNumero: 7, duracaoMs: 30, CancellationToken.None);

        // Primeiro comando: Acionar (0x50).
        var enviado1 = stream.SaidaComoArray();
        Assert.Equal(0x50, enviado1[3]);
        session.TryCompletePendingRequest(new JflPacket { Seq = enviado1[2], Cmd = 0x50, Dados = RespostaComPgm(7, acionada: true) });

        // Apos o intervalo do pulso, o segundo comando deve ser Desacionar (0x51).
        JflPacket? pedidoDesacionar = null;
        for (var tentativa = 0; tentativa < 40 && pedidoDesacionar is null; tentativa++)
        {
            await Task.Delay(10);
            var saidaAtual = stream.SaidaComoArray();
            if (saidaAtual.Length > enviado1.Length)
            {
                var novoTrecho = saidaAtual.AsSpan(enviado1.Length);
                var parse = PacketParser.TryParse(novoTrecho);
                if (parse.Status == JflParseStatus.Success)
                {
                    pedidoDesacionar = parse.Packet;
                }
            }
        }

        Assert.NotNull(pedidoDesacionar);
        Assert.Equal(0x51, pedidoDesacionar!.Cmd);
        Assert.Equal(7, pedidoDesacionar.Dados[0]);

        session.TryCompletePendingRequest(new JflPacket { Seq = pedidoDesacionar.Seq, Cmd = 0x51, Dados = RespostaComPgm(7, acionada: false) });

        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.EstadoConfirmado); // estado final apos o pulso: desligada
    }

    [Fact]
    public async Task PulsoAsync_nao_deve_desacionar_se_o_acionar_falhar()
    {
        var sessionManager = NovoSessionManager();
        var service = new PgmCommandService(sessionManager, NullLogger<PgmCommandService>.Instance);

        // Sem sessao: o Acionar ja falha com CentralOffline, entao nunca deveria tentar o Desacionar.
        var resultado = await service.PulsoAsync("0000000000", 1, duracaoMs: 10, CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(PgmCommandFailureReason.CentralOffline, resultado.Motivo);
    }
}
