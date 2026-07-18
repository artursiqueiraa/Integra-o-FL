using CentralHub.SDK.Jfl;
using CentralHub.SDK.Jfl.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralHub.Simulator.Tests;

/// <summary>
/// Valida o simulador contra um <see cref="JflTcpServer"/> real (porta efêmera, mesma
/// infraestrutura homologada do Backend) — a prova de que o simulador "fala o protocolo de
/// verdade" e não é um duplo solto.
/// </summary>
public class SimuladorActive100BusTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private JflTcpServer _server = null!;

    public Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddJflServer(options => options.Porta = 0);

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
    public async Task ConectarAsync_deve_ser_liberado_e_registrar_sessao_no_SessionManager_real()
    {
        await using var simulador = new SimuladorActive100Bus("1234567890");

        var (liberado, keep) = await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        Assert.True(liberado);
        Assert.True(keep is >= 1 and <= 20);

        var sessionManager = _provider.GetRequiredService<SessionManager>();
        Assert.True(sessionManager.TryGet("1234567890", out var sessao));
        Assert.Equal(JflSessionState.Ativa, sessao!.State);
    }

    [Fact]
    public async Task PgmCommandService_real_deve_conseguir_acionar_a_PGM_do_simulador()
    {
        await using var simulador = new SimuladorActive100Bus("1111111111");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        var pgmService = _provider.GetRequiredService<PgmCommandService>();
        var resultado = await pgmService.AcionarAsync("1111111111", 3, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.EstadoConfirmado);
        Assert.True(simulador.Estado.PgmsAcionadas[2]); // PGM 3 -> índice 2
    }

    [Fact]
    public async Task CentralStatusQueryService_real_deve_ler_o_estado_do_simulador()
    {
        await using var simulador = new SimuladorActive100Bus("2222222222");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);
        simulador.Estado.Particoes[0] = 0x02; // particao 1 armada

        var statusService = _provider.GetRequiredService<CentralStatusQueryService>();
        var resultado = await statusService.ConsultarAsync("2222222222", CancellationToken.None);

        Assert.True(resultado.Sucesso);
        var particao1 = resultado.Status!.Particoes.Single(p => p.Numero == 1);
        Assert.Equal(CentralHub.SDK.Jfl.Messages.Status.PartitionState.Armada, particao1.Estado);
    }

    [Fact]
    public async Task DispararEventoAsync_deve_ser_confirmado_pelo_stub_de_evento()
    {
        await using var simulador = new SimuladorActive100Bus("3333333333");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        // O stub de Evento (Fase 0, antes da Fase 1 implementar o handler real) so loga e nao
        // responde -- confirma que o simulador reporta corretamente a falta de ACK (timeout
        // curto, para o teste nao esperar o timeout padrao de 10s), em vez de travar.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            simulador.DispararEventoAsync(
                "1130", 1, "001", 100, spart: 0x02, comProblema: false, CancellationToken.None, timeout: TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public async Task SimularDesconexao_deve_remover_a_sessao_do_SessionManager()
    {
        await using var simulador = new SimuladorActive100Bus("4444444444");
        await simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None);

        simulador.SimularDesconexao();
        await Task.Delay(500); // da tempo do servidor processar o fechamento da conexao

        var sessionManager = _provider.GetRequiredService<SessionManager>();
        Assert.False(sessionManager.TryGet("4444444444", out _));
    }
}
