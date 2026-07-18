using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Tests.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Server;

public class ArmCommandServiceTests
{
    private static SessionManager NovoSessionManager() => new(NullLogger<SessionManager>.Instance);

    private static JflSession RegistrarSessao(SessionManager sessionManager, byte[] bytesDeEntrada, string numeroSerie, out DuplexMemoryStream stream)
    {
        stream = new DuplexMemoryStream(bytesDeEntrada);
        var session = new JflSession(stream, "127.0.0.1:1") { NumeroSerie = numeroSerie };
        sessionManager.Registrar(session);
        return session;
    }

    /// <summary>Monta uma resposta 4.10 (113 bytes, sem PGM2/P-PGM2) com uma unica particao no estado pedido.</summary>
    private static byte[] RespostaComParticao(int numeroParticao, PartitionState estado)
    {
        var dados = new byte[113];
        dados[10 + (numeroParticao - 1)] = (byte)estado;
        return dados;
    }

    /// <summary>Monta uma resposta 4.10 com o eletrificador no estado pedido (byte ELET, offset 26).</summary>
    private static byte[] RespostaComEletrificador(ElectrifierState estado)
    {
        var dados = new byte[113];
        dados[26] = (byte)estado;
        return dados;
    }

    [Fact]
    public async Task ArmarAsync_sem_sessao_ativa_deve_retornar_CentralOffline()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);

        var resultado = await service.ArmarAsync("0000000000", 1, CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(ArmCommandFailureReason.CentralOffline, resultado.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(-1)]
    [InlineData(98)]
    public async Task ArmarAsync_com_numero_de_particao_fora_da_faixa_deve_retornar_NumeroInvalido(int particaoInvalida)
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        RegistrarSessao(sessionManager, [], "1234567890", out _);

        var resultado = await service.ArmarAsync("1234567890", particaoInvalida, CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(ArmCommandFailureReason.NumeroInvalido, resultado.Motivo);
    }

    [Fact]
    public async Task ArmarAsync_deve_enviar_0x4E_com_a_particao_e_confirmar_sucesso()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.ArmarAsync("1234567890", particao: 3, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0x4E, enviado[3]); // CMD = Armar
        Assert.Equal(3, enviado[4]); // Dados = numero da particao
        Assert.Equal(6, enviado.Length); // CAB QDE SEQ CMD DADOS(1) K

        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x4E, Dados = RespostaComParticao(3, PartitionState.Armada) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.EstadoConfirmado);
    }

    [Fact]
    public async Task DesarmarAsync_deve_enviar_0x4F_e_confirmar_sucesso()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.DesarmarAsync("1234567890", particao: 5, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0x4F, enviado[3]);
        Assert.Equal(5, enviado[4]);

        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x4F, Dados = RespostaComParticao(5, PartitionState.Desarmada) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.EstadoConfirmado);
    }

    [Fact]
    public async Task ArmarStayAsync_deve_enviar_0x53_e_confirmar_via_ArmadaStay()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.ArmarStayAsync("1234567890", particao: 1, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0x53, enviado[3]);

        // Armada (sem Stay) NAO deve confirmar um pedido de ArmarStay.
        var respostaErrada = new JflPacket { Seq = enviado[2], Cmd = 0x53, Dados = RespostaComParticao(1, PartitionState.Armada) };
        session.TryCompletePendingRequest(respostaErrada);
        var resultado = await tarefa;

        Assert.False(resultado.Sucesso);
        Assert.Equal(ArmCommandFailureReason.RespostaInvalida, resultado.Motivo);
    }

    [Fact]
    public async Task ArmarStayAsync_com_ArmadaStay_na_resposta_deve_confirmar_sucesso()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.ArmarStayAsync("1234567890", particao: 1, CancellationToken.None);
        var enviado = stream.SaidaComoArray();

        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x53, Dados = RespostaComParticao(1, PartitionState.ArmadaStay) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.EstadoConfirmado);
    }

    [Fact]
    public async Task ArmarAwayAsync_deve_enviar_0x54_e_confirmar_via_Armada_normal()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.ArmarAwayAsync("1234567890", particao: 2, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0x54, enviado[3]);
        Assert.Equal(2, enviado[4]);

        // Protocolo nao documenta estado "ArmadaAway" separado no fio: confirma via Armada normal.
        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x54, Dados = RespostaComParticao(2, PartitionState.Armada) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.EstadoConfirmado);
    }

    [Fact]
    public async Task ArmarAsync_com_particao_99_deve_operar_o_eletrificador()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.ArmarAsync("1234567890", particao: 99, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0x4E, enviado[3]);
        Assert.Equal(99, enviado[4]); // 0x63

        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x4E, Dados = RespostaComEletrificador(ElectrifierState.Armado) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.EstadoConfirmado);
    }

    [Fact]
    public async Task DesarmarAsync_com_particao_99_deve_confirmar_via_eletrificador_desarmado()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.DesarmarAsync("1234567890", particao: 99, CancellationToken.None);
        var enviado = stream.SaidaComoArray();

        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x4F, Dados = RespostaComEletrificador(ElectrifierState.Desarmado) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.EstadoConfirmado);
    }

    [Fact]
    public async Task ArmarStayAsync_com_particao_99_deve_retornar_NumeroInvalido()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        RegistrarSessao(sessionManager, [], "1234567890", out _);

        var resultado = await service.ArmarStayAsync("1234567890", particao: 99, CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(ArmCommandFailureReason.NumeroInvalido, resultado.Motivo);
    }

    [Fact]
    public async Task ArmarAsync_quando_a_central_nao_confirma_o_estado_deve_retornar_RespostaInvalida()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.ArmarAsync("1234567890", particao: 4, CancellationToken.None);
        var enviado = stream.SaidaComoArray();

        // Central responde mas a particao 4 continua desarmada (ex.: sem permissao) -> nao confirma.
        var resposta = new JflPacket { Seq = enviado[2], Cmd = 0x4E, Dados = RespostaComParticao(4, PartitionState.Desarmada) };
        session.TryCompletePendingRequest(resposta);

        var resultado = await tarefa;

        Assert.False(resultado.Sucesso);
        Assert.Equal(ArmCommandFailureReason.RespostaInvalida, resultado.Motivo);
    }

    [Fact]
    public async Task ArmarAsync_deve_retornar_Timeout_quando_a_central_nao_responde()
    {
        var sessionManager = NovoSessionManager();
        var service = new ArmCommandService(sessionManager, NullLogger<ArmCommandService>.Instance);
        RegistrarSessao(sessionManager, [], "1234567890", out _);

        var resultado = await service.ArmarAsync("1234567890", 1, CancellationToken.None, timeout: TimeSpan.FromMilliseconds(50));

        Assert.False(resultado.Sucesso);
        Assert.Equal(ArmCommandFailureReason.Timeout, resultado.Motivo);
    }
}
