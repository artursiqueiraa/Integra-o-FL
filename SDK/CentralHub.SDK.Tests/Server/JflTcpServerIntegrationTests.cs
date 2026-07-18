using System.Net;
using System.Net.Sockets;
using System.Text;
using CentralHub.SDK.Jfl;
using CentralHub.SDK.Jfl.Protocol;
using CentralHub.SDK.Jfl.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Tests.Server;

/// <summary>
/// Teste ponta a ponta: sobe um <see cref="JflTcpServer"/> real numa porta efemera
/// (via DI, exatamente como o Backend faz) e conecta um <see cref="TcpClient"/> nele,
/// simulando uma central Active 100 Bus real: conexao (0x21) seguida de keep-alive (0x40).
/// </summary>
public class JflTcpServerIntegrationTests : IAsyncLifetime
{
    private readonly ServiceProvider _provider;
    private readonly JflTcpServer _server;

    public JflTcpServerIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddJflServer(options =>
        {
            options.Porta = 0; // porta efemera, escolhida pelo SO
            options.IntervaloKeepAliveMinutos = 5;
        });

        _provider = services.BuildServiceProvider();
        _server = _provider.GetRequiredService<JflTcpServer>();
    }

    public Task InitializeAsync()
    {
        _server.Start();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Servidor_deve_aceitar_conexao_processar_0x21_e_0x40_e_responder_corretamente()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _server.Port);
        var stream = client.GetStream();
        var reader = new JflFrameReader(stream);

        var dadosConexao = BuildDadosConexao("9988776655", modelo: (byte)JflModel.Active100Bus);
        var pacoteConexao = PacketBuilder.Build(seq: 0x01, cmd: 0x21, dadosConexao);
        await stream.WriteAsync(pacoteConexao);

        var respostaConexao = await reader.ReadPacketAsync(CancellationToken.None);
        Assert.NotNull(respostaConexao);
        Assert.Equal(0x21, respostaConexao!.Cmd);
        Assert.Equal(0x01, respostaConexao.Seq); // ecoou o SEQ da requisicao
        Assert.Equal(0x01, respostaConexao.Dados[0]); // Liberado
        Assert.Equal(5, respostaConexao.Dados[1]); // keep-alive: 5 minutos

        var sessionManager = _provider.GetRequiredService<SessionManager>();
        Assert.True(sessionManager.TryGet("9988776655", out var sessao));
        Assert.Equal(JflSessionState.Ativa, sessao!.State);

        var pacoteKeepAlive = PacketBuilder.Build(seq: 0x02, cmd: 0x40, ReadOnlySpan<byte>.Empty);
        await stream.WriteAsync(pacoteKeepAlive);

        var respostaKeepAlive = await reader.ReadPacketAsync(CancellationToken.None);
        Assert.NotNull(respostaKeepAlive);
        Assert.Equal(0x40, respostaKeepAlive!.Cmd);
        Assert.Equal(0x02, respostaKeepAlive.Seq);
        Assert.Equal(5, respostaKeepAlive.Dados[0]);
    }

    [Fact]
    public async Task Servidor_deve_remover_a_sessao_quando_a_central_desconecta()
    {
        var sessionManager = _provider.GetRequiredService<SessionManager>();

        using (var client = new TcpClient())
        {
            await client.ConnectAsync(IPAddress.Loopback, _server.Port);
            var stream = client.GetStream();
            var reader = new JflFrameReader(stream);

            var pacoteConexao = PacketBuilder.Build(0x01, 0x21, BuildDadosConexao("5544332211", (byte)JflModel.Active100Bus));
            await stream.WriteAsync(pacoteConexao);
            await reader.ReadPacketAsync(CancellationToken.None);

            Assert.True(sessionManager.TryGet("5544332211", out _));
        } // fecha o socket ao sair do using

        // Da um tempo para o servidor detectar o fechamento e remover a sessao.
        var removida = false;
        for (var tentativa = 0; tentativa < 20 && !removida; tentativa++)
        {
            await Task.Delay(50);
            removida = !sessionManager.TryGet("5544332211", out _);
        }

        Assert.True(removida, "A sessao deveria ter sido removida apos o fechamento da conexao.");
    }

    [Fact]
    public async Task Servidor_deve_encaminhar_consulta_de_status_0x4D_e_repassar_a_resposta_da_central()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _server.Port);
        var stream = client.GetStream();
        var reader = new JflFrameReader(stream);

        var pacoteConexao = PacketBuilder.Build(0x01, 0x21, BuildDadosConexao("1112223334", (byte)JflModel.Active100Bus));
        await stream.WriteAsync(pacoteConexao);
        await reader.ReadPacketAsync(CancellationToken.None); // resposta da conexao, nao interessa aqui

        var queryService = _provider.GetRequiredService<CentralStatusQueryService>();
        var tarefaConsulta = queryService.ConsultarAsync("1112223334", CancellationToken.None);

        // O "equipamento" (do lado do TcpClient) le o 0x4D que o servidor enviou e
        // responde com um status sintetico, exatamente como uma central real faria
        // no mesmo socket que ela abriu para se conectar.
        var pedidoStatus = await reader.ReadPacketAsync(CancellationToken.None);
        Assert.NotNull(pedidoStatus);
        Assert.Equal(0x4D, pedidoStatus!.Cmd);
        Assert.Empty(pedidoStatus.Dados); // comando de status nao leva dados (secao 4.1)

        var respostaStatus = PacketBuilder.Build(pedidoStatus.Seq, 0x4D, new byte[113]);
        await stream.WriteAsync(respostaStatus);

        var resultado = await tarefaConsulta;

        Assert.True(resultado.Sucesso);
        Assert.NotNull(resultado.Status);
        Assert.Equal(16, resultado.Status!.Particoes.Count);
        Assert.Equal(99, resultado.Status.Zonas.Count);
        Assert.Equal(16, resultado.Status.Pgms.Count);
    }

    [Fact]
    public async Task Servidor_deve_encaminhar_comando_de_PGM_0x50_e_repassar_a_confirmacao_da_central()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _server.Port);
        var stream = client.GetStream();
        var reader = new JflFrameReader(stream);

        var pacoteConexao = PacketBuilder.Build(0x01, 0x21, BuildDadosConexao("7778889990", (byte)JflModel.Active100Bus));
        await stream.WriteAsync(pacoteConexao);
        await reader.ReadPacketAsync(CancellationToken.None);

        var pgmService = _provider.GetRequiredService<PgmCommandService>();
        var tarefaComando = pgmService.AcionarAsync("7778889990", pgmNumero: 4, CancellationToken.None);

        var pedidoPgm = await reader.ReadPacketAsync(CancellationToken.None);
        Assert.NotNull(pedidoPgm);
        Assert.Equal(0x50, pedidoPgm!.Cmd); // Acionar PGM (secao 4.4)
        Assert.Equal(4, pedidoPgm.Dados[0]); // numero da PGM

        var dadosResposta = new byte[115];
        dadosResposta[9] = 0b0000_1000; // bit3 = PGM4 acionada
        var respostaPgm = PacketBuilder.Build(pedidoPgm.Seq, 0x50, dadosResposta);
        await stream.WriteAsync(respostaPgm);

        var resultado = await tarefaComando;

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.EstadoConfirmado);
    }

    private static byte[] BuildDadosConexao(string numeroSerie, byte modelo)
    {
        var dados = new List<byte>();
        dados.AddRange(Encoding.ASCII.GetBytes(numeroSerie)); // NS (10)
        dados.AddRange(Enumerable.Repeat((byte)0xFF, 15)); // IMEI vazio
        dados.AddRange(Enumerable.Repeat((byte)0xFF, 12)); // MAC vazio
        dados.Add(modelo);
        dados.AddRange(Encoding.ASCII.GetBytes("400")); // VER = 4.0
        dados.Add(0x01); // IP
        dados.Add(0x01); // SIMCARD
        dados.Add(0x01); // VIA = Ethernet
        dados.Add(0x06); // OPE = nao existe
        return dados.ToArray();
    }

    public async Task DisposeAsync()
    {
        await _server.StopAsync();
        await _provider.DisposeAsync();
    }
}
