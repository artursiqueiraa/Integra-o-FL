using System.Net;
using System.Net.Sockets;
using CentralHub.SDK.Jfl;
using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Tests.Diagnostics;

/// <summary>
/// Confirma que <c>LogHexAtivado=true</c> (Fase 0.8) não muda nenhum comportamento do
/// servidor real — mesmo teste de handshake+keep-alive já coberto por
/// <c>JflTcpServerIntegrationTests</c>, agora com o log HEX ligado, provando que o decorator
/// é de fato transparente (comparado byte a byte com a resposta esperada).
/// </summary>
public class JflTcpServerHexLoggingIntegrationTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private JflTcpServer _server = null!;

    public Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug)); // Debug: exercita o caminho de log do decorator
        services.AddJflServer(options =>
        {
            options.Porta = 0;
            options.LogHexAtivado = true;
        });

        _provider = services.BuildServiceProvider();
        _server = _provider.GetRequiredService<JflTcpServer>();
        _server.Start();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _server.StopAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task ComLogHexAtivado_handshake_e_keepalive_devem_funcionar_normalmente()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _server.Port);
        var stream = client.GetStream();
        var reader = new JflFrameReader(stream);

        var dadosConexao = new byte[45]; // NS(10)+IMEI(15)+MAC(12)+MOD(1)+VER(3)+IP(1)+SIMCARD(1)+VIA(1)+OPE(1)
        "9988001122"u8.ToArray().CopyTo(dadosConexao, 0);
        Array.Fill(dadosConexao, (byte)0xFF, 10, 27); // IMEI+MAC vazios
        dadosConexao[37] = (byte)JflModel.Active100Bus;
        "650"u8.ToArray().CopyTo(dadosConexao, 38);
        dadosConexao[41] = 0x01; // IP
        dadosConexao[42] = 0x01; // SIMCARD
        dadosConexao[43] = 0x01; // VIA
        dadosConexao[44] = 0x06; // OPE

        var pacoteConexao = PacketBuilder.Build(seq: 0x01, cmd: 0x21, dadosConexao);
        await stream.WriteAsync(pacoteConexao);

        var respostaConexao = await reader.ReadPacketAsync(CancellationToken.None);
        Assert.NotNull(respostaConexao);
        Assert.Equal(0x01, respostaConexao!.Dados[0]); // Liberado

        var pacoteKeepAlive = PacketBuilder.Build(seq: 0x02, cmd: 0x40, ReadOnlySpan<byte>.Empty);
        await stream.WriteAsync(pacoteKeepAlive);

        var respostaKeepAlive = await reader.ReadPacketAsync(CancellationToken.None);
        Assert.NotNull(respostaKeepAlive);
        Assert.Equal(0x40, respostaKeepAlive!.Cmd);

        var sessionManager = _provider.GetRequiredService<SessionManager>();
        Assert.True(sessionManager.TryGet("9988001122", out var sessao));
        Assert.Equal(JflSessionState.Ativa, sessao!.State);
    }
}
