using CentralHub.Simulator;

// Uso manual: dotnet run --project Simulator/CentralHub.Simulator -- <numeroSerie10digitos> [host=127.0.0.1] [porta=8085]
if (args.Length < 1 || args[0].Length != 10)
{
    Console.WriteLine("Uso: CentralHub.Simulator <numeroSerie (10 dígitos)> [host=127.0.0.1] [porta=8085]");
    return 1;
}

var numeroSerie = args[0];
var host = args.Length > 1 ? args[1] : "127.0.0.1";
var porta = args.Length > 2 ? int.Parse(args[2]) : 8085;

await using var simulador = new SimuladorActive100Bus(numeroSerie);
simulador.ComandoRecebido += p => Console.WriteLine($"Comando recebido e respondido: Cmd=0x{p.Cmd:X2} Seq=0x{p.Seq:X2}");

Console.WriteLine($"Conectando em {host}:{porta} como {numeroSerie} (Active 100 Bus)...");
var (liberado, keep) = await simulador.ConectarAsync(host, porta, CancellationToken.None);
Console.WriteLine(liberado
    ? $"Conectado. Keep-alive a cada {keep} minuto(s). Pressione Ctrl+C para encerrar."
    : "Conexão recusada pelo servidor (número de série bloqueado).");

if (liberado)
{
    await Task.Delay(Timeout.Infinite);
}

return 0;
