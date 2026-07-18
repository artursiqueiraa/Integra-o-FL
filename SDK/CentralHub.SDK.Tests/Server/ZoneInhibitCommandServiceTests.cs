using CentralHub.SDK.Jfl.Messages.Status;
using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using CentralHub.SDK.Tests.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace CentralHub.SDK.Tests.Server;

public class ZoneInhibitCommandServiceTests
{
    private static SessionManager NovoSessionManager() => new(NullLogger<SessionManager>.Instance);

    private static JflSession RegistrarSessao(SessionManager sessionManager, byte[] bytesDeEntrada, string numeroSerie, out DuplexMemoryStream stream)
    {
        stream = new DuplexMemoryStream(bytesDeEntrada);
        var session = new JflSession(stream, "127.0.0.1:1") { NumeroSerie = numeroSerie };
        sessionManager.Registrar(session);
        return session;
    }

    /// <summary>Monta uma resposta 4.10 (113 bytes) com as zonas informadas marcadas como Inibida (nibble ZONA, offset 27).</summary>
    private static byte[] RespostaComZonasInibidas(params int[] zonasInibidas)
    {
        var dados = new byte[113];
        foreach (var numero in zonasInibidas)
        {
            var indiceByte = (numero - 1) / 2;
            var primeiroDoPar = (numero - 1) % 2 == 0;
            var valorNibble = (byte)ZoneState.Inibida;
            dados[27 + indiceByte] |= primeiroDoPar ? (byte)(valorNibble << 4) : valorNibble;
        }
        return dados;
    }

    [Fact]
    public async Task InibirZonasAsync_sem_sessao_ativa_deve_retornar_CentralOffline()
    {
        var sessionManager = NovoSessionManager();
        var service = new ZoneInhibitCommandService(sessionManager, NullLogger<ZoneInhibitCommandService>.Instance);

        var resultado = await service.InibirZonasAsync("0000000000", new HashSet<int> { 1 }, CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(ZoneInhibitFailureReason.CentralOffline, resultado.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(-1)]
    public async Task InibirZonasAsync_com_zona_fora_da_faixa_deve_retornar_NumeroInvalido(int zonaInvalida)
    {
        var sessionManager = NovoSessionManager();
        var service = new ZoneInhibitCommandService(sessionManager, NullLogger<ZoneInhibitCommandService>.Instance);
        RegistrarSessao(sessionManager, [], "1234567890", out _);

        var resultado = await service.InibirZonasAsync("1234567890", new HashSet<int> { zonaInvalida }, CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal(ZoneInhibitFailureReason.NumeroInvalido, resultado.Motivo);
    }

    [Fact]
    public async Task InibirZonasAsync_com_zona_1_deve_enviar_0x80_no_primeiro_byte()
    {
        var sessionManager = NovoSessionManager();
        var service = new ZoneInhibitCommandService(sessionManager, NullLogger<ZoneInhibitCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.InibirZonasAsync("1234567890", new HashSet<int> { 1 }, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0x52, enviado[3]); // CMD = Inibir Zonas
        Assert.Equal(18, enviado.Length); // CAB QDE SEQ CMD DADOS(13) K
        Assert.Equal(0x80, enviado[4]); // byte 1 do bitmap: bit7 = zona 1 (MSB-first)
        for (var i = 5; i < 17; i++)
        {
            Assert.Equal(0x00, enviado[i]);
        }

        session.TryCompletePendingRequest(new JflPacket { Seq = enviado[2], Cmd = 0x52, Dados = RespostaComZonasInibidas(1) });
        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public async Task InibirZonasAsync_com_zonas_1_a_4_deve_enviar_0xF0()
    {
        var sessionManager = NovoSessionManager();
        var service = new ZoneInhibitCommandService(sessionManager, NullLogger<ZoneInhibitCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.InibirZonasAsync("1234567890", new HashSet<int> { 1, 2, 3, 4 }, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0xF0, enviado[4]);

        session.TryCompletePendingRequest(new JflPacket { Seq = enviado[2], Cmd = 0x52, Dados = RespostaComZonasInibidas(1, 2, 3, 4) });
        await tarefa;
    }

    [Fact]
    public async Task InibirZonasAsync_com_zonas_1_a_9_deve_enviar_0xFF_0x80_exemplo_real_do_manual()
    {
        var sessionManager = NovoSessionManager();
        var service = new ZoneInhibitCommandService(sessionManager, NullLogger<ZoneInhibitCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var zonas = Enumerable.Range(1, 9).ToHashSet();
        var tarefa = service.InibirZonasAsync("1234567890", zonas, CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        Assert.Equal(0xFF, enviado[4]); // byte 1: zonas 1-8
        Assert.Equal(0x80, enviado[5]); // byte 2: zona 9 (bit7)

        session.TryCompletePendingRequest(new JflPacket { Seq = enviado[2], Cmd = 0x52, Dados = RespostaComZonasInibidas(zonas.ToArray()) });
        await tarefa;
    }

    [Fact]
    public async Task InibirZonasAsync_com_conjunto_vazio_deve_enviar_bitmap_zerado_desinibindo_tudo()
    {
        var sessionManager = NovoSessionManager();
        var service = new ZoneInhibitCommandService(sessionManager, NullLogger<ZoneInhibitCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.InibirZonasAsync("1234567890", new HashSet<int>(), CancellationToken.None);

        var enviado = stream.SaidaComoArray();
        for (var i = 4; i < 17; i++)
        {
            Assert.Equal(0x00, enviado[i]);
        }

        session.TryCompletePendingRequest(new JflPacket { Seq = enviado[2], Cmd = 0x52, Dados = RespostaComZonasInibidas() });
        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.All(resultado.ZonasResultantes!, z => Assert.NotEqual(ZoneState.Inibida, z.Estado));
    }

    [Fact]
    public async Task InibirZonasAsync_deve_devolver_ZonasResultantes_da_resposta_da_central()
    {
        var sessionManager = NovoSessionManager();
        var service = new ZoneInhibitCommandService(sessionManager, NullLogger<ZoneInhibitCommandService>.Instance);
        var session = RegistrarSessao(sessionManager, [], "1234567890", out var stream);

        var tarefa = service.InibirZonasAsync("1234567890", new HashSet<int> { 7 }, CancellationToken.None);
        var enviado = stream.SaidaComoArray();

        session.TryCompletePendingRequest(new JflPacket { Seq = enviado[2], Cmd = 0x52, Dados = RespostaComZonasInibidas(7) });
        var resultado = await tarefa;

        Assert.True(resultado.Sucesso);
        Assert.NotNull(resultado.ZonasResultantes);
        Assert.Equal(CentralStatusResponse.QuantidadeZonas, resultado.ZonasResultantes!.Count);
        Assert.Equal(ZoneState.Inibida, resultado.ZonasResultantes!.Single(z => z.Numero == 7).Estado);
    }

    [Fact]
    public async Task InibirZonasAsync_deve_retornar_Timeout_quando_a_central_nao_responde()
    {
        var sessionManager = NovoSessionManager();
        var service = new ZoneInhibitCommandService(sessionManager, NullLogger<ZoneInhibitCommandService>.Instance);
        RegistrarSessao(sessionManager, [], "1234567890", out _);

        var resultado = await service.InibirZonasAsync(
            "1234567890", new HashSet<int> { 1 }, CancellationToken.None, timeout: TimeSpan.FromMilliseconds(50));

        Assert.False(resultado.Sucesso);
        Assert.Equal(ZoneInhibitFailureReason.Timeout, resultado.Motivo);
    }
}
