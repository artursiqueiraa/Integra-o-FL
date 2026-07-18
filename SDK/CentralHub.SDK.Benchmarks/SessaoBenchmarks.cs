using BenchmarkDotNet.Attributes;
using CentralHub.SDK.Jfl;
using CentralHub.SDK.Jfl.Server;
using CentralHub.Simulator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CentralHub.SDK.Benchmarks;

/// <summary>
/// Benchmarks de ponta a ponta via socket real (mesma infraestrutura homologada do Backend,
/// `AddJflServer`) — mede o pipeline completo de sessão (dispatcher + handler + resposta),
/// não só o parsing isolado. Usa o Central Simulator (Fase 0.5) do lado cliente.
/// </summary>
[MemoryDiagnoser]
public class SessaoBenchmarks
{
    private ServiceProvider _provider = null!;
    private JflTcpServer _server = null!;
    private SimuladorActive100Bus _simulador = null!;
    private PgmCommandService _pgmService = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Error));
        services.AddJflServer(o => o.Porta = 0);
        _provider = services.BuildServiceProvider();
        _server = _provider.GetRequiredService<JflTcpServer>();
        _server.Start();

        _simulador = new SimuladorActive100Bus("9000000001");
        _simulador.ConectarAsync("127.0.0.1", _server.Port, CancellationToken.None).GetAwaiter().GetResult();

        _pgmService = _provider.GetRequiredService<PgmCommandService>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _simulador.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _server.StopAsync().GetAwaiter().GetResult();
        _provider.Dispose();
    }

    [Benchmark]
    public async Task PgmCommandService_AcionarAsync_RoundTripCompleto()
    {
        await _pgmService.AcionarAsync(_simulador.NumeroSerie, 1, CancellationToken.None);
    }

    [Benchmark]
    public async Task KeepAlive_RoundTripCompleto()
    {
        await _simulador.EnviarKeepAliveAsync(CancellationToken.None);
    }
}
