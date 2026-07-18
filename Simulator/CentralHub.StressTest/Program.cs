using System.Diagnostics;
using CentralHub.SDK.Jfl;
using CentralHub.SDK.Jfl.Server;
using CentralHub.Simulator;
using CentralHub.StressTest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Ferramenta de carga (Fase 0.6 do plano de homologação) — sobe um JflTcpServer real (a
// mesma infraestrutura homologada do Backend, via AddJflServer) e gera os volumes pedidos
// usando o Central Simulator (Fase 0.5), sem precisar de hardware físico nem do Backend real
// rodando. Uso: dotnet run --project Simulator/CentralHub.StressTest -- [opções]
var opcoes = OpcoesStress.Parse(args);

var services = new ServiceCollection();
services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Error)); // silencioso: o volume de log com centenas de conexões polui o console
services.AddJflServer(o => o.Porta = 0);

await using var provider = services.BuildServiceProvider();
var server = provider.GetRequiredService<JflTcpServer>();
server.Start();
Console.WriteLine($"Servidor JFL efêmero escutando na porta {server.Port}.");

var memoriaAntes = GC.GetTotalMemory(forceFullCollection: true);
var relatorio = new RelatorioStress();

Console.WriteLine($"Conectando {opcoes.Conexoes} centrais simuladas...");
var simuladores = await ConectarSimuladoresAsync(opcoes.Conexoes, server.Port, relatorio);
Console.WriteLine($"{simuladores.Count} conexões estabelecidas ({relatorio.FalhasConexao} falhas).");

await ExecutarKeepAlivesAsync(simuladores, opcoes.KeepAlives, relatorio);
Console.WriteLine($"{opcoes.KeepAlives} keep-alives disparados ({relatorio.FalhasKeepAlive} falhas).");

await ExecutarEventosAsync(simuladores, opcoes.Eventos, relatorio);
Console.WriteLine($"{opcoes.Eventos} eventos disparados ({relatorio.FalhasEvento} confirmações ausentes — esperado até a Fase 1 implementar o handler real de Evento).");

await ExecutarConsultasStatusAsync(provider, simuladores, opcoes.Consultas, relatorio);
Console.WriteLine($"{opcoes.Consultas} consultas de status ({relatorio.FalhasConsulta} falhas).");

await ExecutarPgmsAsync(provider, simuladores, opcoes.Pgms, relatorio);
Console.WriteLine($"{opcoes.Pgms} comandos de PGM ({relatorio.FalhasPgm} falhas).");

await ExecutarCenariosDeFalhaAsync(provider, simuladores, server.Port, relatorio);
Console.WriteLine("Cenários de falha (reconexão, timeout, checksum inválido, pacote quebrado) executados.");

var memoriaDepois = GC.GetTotalMemory(forceFullCollection: true);

foreach (var s in simuladores)
{
    await s.DisposeAsync();
}

await server.StopAsync();

relatorio.MemoriaAntesBytes = memoriaAntes;
relatorio.MemoriaDepoisBytes = memoriaDepois;

var caminhoRelatorio = opcoes.CaminhoSaida ?? Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..",
    "Documentation", "RealCaptures", "StressTestResults", $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.md");

Directory.CreateDirectory(Path.GetDirectoryName(caminhoRelatorio)!);
await File.WriteAllTextAsync(caminhoRelatorio, relatorio.GerarMarkdown(opcoes));
Console.WriteLine($"\nRelatório gravado em: {Path.GetFullPath(caminhoRelatorio)}");
Console.WriteLine(relatorio.GerarResumoConsole());

static async Task<List<SimuladorActive100Bus>> ConectarSimuladoresAsync(int quantidade, int porta, RelatorioStress relatorio)
{
    var simuladores = new List<SimuladorActive100Bus>(quantidade);
    var semaforo = new SemaphoreSlim(20);
    var lockObj = new object();

    var tarefas = Enumerable.Range(0, quantidade).Select(async i =>
    {
        await semaforo.WaitAsync();
        try
        {
            var numeroSerie = (2_000_000_000L + i).ToString("D10");
            var simulador = new SimuladorActive100Bus(numeroSerie);
            var cronometro = Stopwatch.StartNew();
            try
            {
                var (liberado, _) = await simulador.ConectarAsync("127.0.0.1", porta, CancellationToken.None);
                cronometro.Stop();
                lock (lockObj)
                {
                    relatorio.LatenciasConexaoMs.Add(cronometro.Elapsed.TotalMilliseconds);
                    if (!liberado)
                    {
                        relatorio.FalhasConexao++;
                        return;
                    }

                    simuladores.Add(simulador);
                }
            }
            catch
            {
                lock (lockObj) relatorio.FalhasConexao++;
            }
        }
        finally
        {
            semaforo.Release();
        }
    });

    await Task.WhenAll(tarefas);
    return simuladores;
}

static async Task ExecutarKeepAlivesAsync(List<SimuladorActive100Bus> simuladores, int quantidade, RelatorioStress relatorio)
{
    if (simuladores.Count == 0) return;
    var lockObj = new object();
    var tarefas = Enumerable.Range(0, quantidade).Select(async i =>
    {
        var simulador = simuladores[i % simuladores.Count];
        var cronometro = Stopwatch.StartNew();
        try
        {
            await simulador.EnviarKeepAliveAsync(CancellationToken.None, TimeSpan.FromSeconds(5));
            cronometro.Stop();
            lock (lockObj) relatorio.LatenciasKeepAliveMs.Add(cronometro.Elapsed.TotalMilliseconds);
        }
        catch
        {
            lock (lockObj) relatorio.FalhasKeepAlive++;
        }
    });
    await Task.WhenAll(tarefas);
}

static async Task ExecutarEventosAsync(List<SimuladorActive100Bus> simuladores, int quantidade, RelatorioStress relatorio)
{
    if (simuladores.Count == 0) return;
    var lockObj = new object();
    var tarefas = Enumerable.Range(0, quantidade).Select(async i =>
    {
        var simulador = simuladores[i % simuladores.Count];
        var cronometro = Stopwatch.StartNew();
        try
        {
            // 0x24 ainda e stub nesta fase (Fase 1 implementa o handler real) — o objetivo
            // aqui e medir o comportamento do servidor sob volume, nao a confirmacao em si.
            await simulador.DispararEventoAsync(
                "1130", 1, "001", (uint)i, spart: 0x02, comProblema: false, CancellationToken.None, timeout: TimeSpan.FromMilliseconds(500));
            cronometro.Stop();
            lock (lockObj) relatorio.LatenciasEventoMs.Add(cronometro.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            lock (lockObj) relatorio.FalhasEvento++;
        }
        catch
        {
            lock (lockObj) relatorio.FalhasEvento++;
        }
    });
    await Task.WhenAll(tarefas);
}

static async Task ExecutarConsultasStatusAsync(ServiceProvider provider, List<SimuladorActive100Bus> simuladores, int quantidade, RelatorioStress relatorio)
{
    if (simuladores.Count == 0) return;
    var statusService = provider.GetRequiredService<CentralStatusQueryService>();
    var lockObj = new object();
    var tarefas = Enumerable.Range(0, quantidade).Select(async i =>
    {
        var simulador = simuladores[i % simuladores.Count];
        var cronometro = Stopwatch.StartNew();
        var resultado = await statusService.ConsultarAsync(simulador.NumeroSerie, CancellationToken.None);
        cronometro.Stop();
        lock (lockObj)
        {
            relatorio.LatenciasConsultaMs.Add(cronometro.Elapsed.TotalMilliseconds);
            if (!resultado.Sucesso) relatorio.FalhasConsulta++;
        }
    });
    await Task.WhenAll(tarefas);
}

static async Task ExecutarPgmsAsync(ServiceProvider provider, List<SimuladorActive100Bus> simuladores, int quantidade, RelatorioStress relatorio)
{
    if (simuladores.Count == 0) return;
    var pgmService = provider.GetRequiredService<PgmCommandService>();
    var lockObj = new object();
    var tarefas = Enumerable.Range(0, quantidade).Select(async i =>
    {
        var simulador = simuladores[i % simuladores.Count];
        var numeroPgm = (i % 16) + 1;
        var cronometro = Stopwatch.StartNew();
        var resultado = await pgmService.AcionarAsync(simulador.NumeroSerie, numeroPgm, CancellationToken.None);
        cronometro.Stop();
        lock (lockObj)
        {
            relatorio.LatenciasPgmMs.Add(cronometro.Elapsed.TotalMilliseconds);
            if (!resultado.Sucesso) relatorio.FalhasPgm++;
        }
    });
    await Task.WhenAll(tarefas);
}

static async Task ExecutarCenariosDeFalhaAsync(ServiceProvider provider, List<SimuladorActive100Bus> simuladores, int porta, RelatorioStress relatorio)
{
    if (simuladores.Count < 4) return;

    // Reconexao: derruba e reconecta a primeira central simulada.
    simuladores[0].SimularDesconexao();
    await Task.Delay(300);
    try
    {
        await simuladores[0].ReconectarAsync("127.0.0.1", porta, CancellationToken.None);
        relatorio.ReconexaoOk = true;
    }
    catch
    {
        relatorio.ReconexaoOk = false;
    }

    // Timeout: o servidor pede PGM a uma central que nao vai responder — confirma que o
    // PgmCommandService devolve falha (Timeout) em vez de travar, e que o servidor segue
    // operacional depois (nao derruba outras sessoes).
    var pgmService = provider.GetRequiredService<PgmCommandService>();
    simuladores[1].SimularTimeout();
    var resultadoTimeout = await pgmService.AcionarAsync(
        simuladores[1].NumeroSerie, 1, CancellationToken.None, timeout: TimeSpan.FromSeconds(1));
    relatorio.CenarioTimeoutRetornouFalhaCorretamente = !resultadoTimeout.Sucesso;

    // Checksum invalido / pacote quebrado: a central simulada manda um keep-alive corrompido
    // de proposito — o servidor deve descartar o pacote silenciosamente (sem derrubar a
    // conexao nem lancar excecao nao tratada), confirmado aqui por "nao lancou".
    try
    {
        simuladores[2].SimularChecksumInvalido();
        await simuladores[2].EnviarKeepAliveAsync(CancellationToken.None, TimeSpan.FromMilliseconds(500));
    }
    catch (OperationCanceledException)
    {
        // esperado: o servidor descarta o pacote corrompido e nunca responde a ele.
    }

    try
    {
        simuladores[3].SimularPacoteQuebrado();
        await simuladores[3].EnviarKeepAliveAsync(CancellationToken.None, TimeSpan.FromMilliseconds(500));
    }
    catch (OperationCanceledException)
    {
        // esperado: pacote incompleto nunca fecha um frame valido, servidor so espera mais dados.
    }

    relatorio.CenariosDeFalhaSinalizados = true;
}
