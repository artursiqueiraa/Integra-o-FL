using CentralHub.SDK.Jfl.Diagnostics;

// Ferramenta de conveniencia (Fase 0.4 do plano de homologacao) para reproduzir uma captura
// contra um servidor JFL real ja rodando (--loopback, host:porta padrao 127.0.0.1:8085) ou
// contra um servidor efemero criado so para este replay (--efemero). Nao e obrigatoria para
// os testes automatizados (que usam ReplayEngine diretamente) — e so para debug manual.
//
// Uso:
//   dotnet run --project SDK/CentralHub.SDK.Tools/ReplayCli -- <caminho.bin> --loopback [host] [porta]
//   dotnet run --project SDK/CentralHub.SDK.Tools/ReplayCli -- <caminho.bin> --efemero

if (args.Length < 2)
{
    Console.WriteLine("Uso: ReplayCli <caminho.bin> --loopback [host=127.0.0.1] [porta=8085]");
    Console.WriteLine("     ReplayCli <caminho.bin> --efemero");
    return 1;
}

var caminho = args[0];
if (!File.Exists(caminho))
{
    Console.Error.WriteLine($"Arquivo não encontrado: {caminho}");
    return 1;
}

var modo = args[1];
var pacote = await File.ReadAllBytesAsync(caminho);

ReplayResultado resultado;
if (modo == "--efemero")
{
    Console.WriteLine("Subindo servidor JFL efêmero para o replay...");
    resultado = await ReplayEngine.ReplayContraServidorEfemeroAsync(pacote, CancellationToken.None);
}
else if (modo == "--loopback")
{
    var host = args.Length > 2 ? args[2] : "127.0.0.1";
    var porta = args.Length > 3 ? int.Parse(args[3]) : 8085;
    Console.WriteLine($"Reproduzindo contra {host}:{porta}...");
    resultado = await ReplayEngine.ReplayAsync(pacote, host, porta, CancellationToken.None);
}
else
{
    Console.Error.WriteLine($"Modo desconhecido: {modo} (use --loopback ou --efemero)");
    return 1;
}

Console.WriteLine($"Pacote enviado: {Convert.ToHexString(resultado.PacoteEnviado)}");
Console.WriteLine($"Duração: {resultado.Duracao.TotalMilliseconds:F1}ms");

if (resultado.Sucesso)
{
    var r = resultado.RespostaRecebida!;
    Console.WriteLine($"Resposta: Seq=0x{r.Seq:X2} Cmd=0x{r.Cmd:X2} Dados={Convert.ToHexString(r.Dados)}");
    return 0;
}

Console.Error.WriteLine($"Falha: {resultado.Erro}");
return 1;
