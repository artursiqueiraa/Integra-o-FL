using CentralHub.SDK.Jfl.Diagnostics;

namespace CentralHub.SDK.Tests.Diagnostics;

public class ReplayEngineTests
{
    [Fact]
    public async Task ReplayContraServidorEfemeroAsync_Handshake_deve_receber_conexao_liberada()
    {
        // Mesma captura real usada em Documentation/RealCaptures/Handshake.bin.
        byte[] pacoteConexao =
        [
            0x7B, 0x66, 0x17, 0x21,
            0x32, 0x37, 0x33, 0x35, 0x38, 0x37, 0x39, 0x32, 0x35, 0x34,
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x39, 0x38, 0x46, 0x34, 0x41, 0x42, 0x36, 0x45, 0x46, 0x34, 0x46, 0x30,
            0xA3,
            0x36, 0x30, 0x30,
            0x01, 0x01, 0x01, 0x06,
            0x00, 0x01, 0x01, 0x00, 0x01, 0x00, 0x02,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x04, 0x01,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x41,
        ];

        var resultado = await ReplayEngine.ReplayContraServidorEfemeroAsync(pacoteConexao, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.NotNull(resultado.RespostaRecebida);
        Assert.Equal(0x21, resultado.RespostaRecebida!.Cmd);
        Assert.Equal(0x01, resultado.RespostaRecebida.Dados[0]); // Liberado (autorizacao default libera tudo)
    }

    [Fact]
    public async Task ReplayContraServidorEfemeroAsync_KeepAlive_deve_receber_intervalo_configurado()
    {
        // Mesma captura real usada em Documentation/RealCaptures/KeepAlive.bin.
        byte[] pacoteKeepAlive = [0x7B, 0x05, 0x18, 0x40, 0x26];

        var resultado = await ReplayEngine.ReplayContraServidorEfemeroAsync(
            pacoteKeepAlive, CancellationToken.None, configurar: o => o.IntervaloKeepAliveMinutos = 5);

        Assert.True(resultado.Sucesso);
        Assert.Equal(0x40, resultado.RespostaRecebida!.Cmd);
        Assert.Equal(5, resultado.RespostaRecebida.Dados[0]);
    }

    [Fact]
    public async Task ReplayContraServidorEfemeroAsync_ComandoOrfaoDeStub_deve_dar_timeout()
    {
        // 0x50 (Acionar PGM) e um comando Tipo A: so gera resposta quando o SERVIDOR o envia
        // e correlaciona por SEQ. Enviado "do nada" (como se a central tivesse iniciado), cai
        // no PgmCommandHandlerStub, que so loga e nunca responde — comportamento correto.
        byte[] pacotePgmOrfao = [0x7B, 0x06, 0x01, 0x50, 0x01, 0x00];
        // Ajusta o checksum para o pacote ser aceito pelo parser antes de chegar ao dispatcher.
        byte k = 0;
        foreach (var b in pacotePgmOrfao.AsSpan(0, 5)) k ^= b;
        pacotePgmOrfao[5] = k;

        var resultado = await ReplayEngine.ReplayContraServidorEfemeroAsync(
            pacotePgmOrfao, CancellationToken.None, timeout: TimeSpan.FromMilliseconds(500));

        Assert.False(resultado.Sucesso);
        Assert.Contains("Timeout", resultado.Erro);
    }

    [Fact]
    public async Task ReplayAsync_sem_servidor_escutando_deve_reportar_falha_sem_lancar()
    {
        var resultado = await ReplayEngine.ReplayAsync(
            [0x7B, 0x05, 0x18, 0x40, 0x26], "127.0.0.1", 1, CancellationToken.None, TimeSpan.FromMilliseconds(500));

        Assert.False(resultado.Sucesso);
        Assert.NotNull(resultado.Erro);
    }
}
