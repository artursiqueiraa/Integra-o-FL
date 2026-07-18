# 16 — Replay Engine

## Objetivo

Reproduzir uma captura salva (`Documentation/RealCaptures/*.bin`) contra um servidor JFL real,
byte a byte, para reproduzir bugs de forma determinística — sem depender de hardware físico
presente no momento da depuração.

## Onde fica

`SDK/CentralHub.SDK/Jfl/Diagnostics/ReplayEngine.cs` (biblioteca) +
`SDK/CentralHub.SDK.Tools/ReplayCli/` (CLI de conveniência).

## Código

```csharp
// Contra um Backend real já rodando:
var resultado = await ReplayEngine.ReplayArquivoAsync(
    "Documentation/RealCaptures/PGM_ON.bin", "127.0.0.1", 8085, ct);

// Contra um servidor efêmero criado só para este replay (não precisa do Backend rodando):
var resultado2 = await ReplayEngine.ReplayContraServidorEfemeroAsync(pacoteBruto, ct);

if (resultado.Sucesso)
    Console.WriteLine($"Resposta: {resultado.RespostaRecebida}");
else
    Console.WriteLine($"Falha: {resultado.Erro}");
```

`ReplayContraServidorEfemeroAsync` sobe um `JflTcpServer` real (porta 0, mesma extensão de DI
`AddJflServer` que o Backend usa) — não é uma simulação separada, é a infraestrutura homologada
de verdade, só numa porta descartável.

## CLI

```powershell
# Contra um Backend já rodando na porta padrão:
dotnet run --project SDK/CentralHub.SDK.Tools/ReplayCli -- Documentation/RealCaptures/PGM_ON.bin --loopback

# Contra um servidor efêmero (não precisa de nada rodando):
dotnet run --project SDK/CentralHub.SDK.Tools/ReplayCli -- Documentation/RealCaptures/Handshake.bin --efemero
```

## Validação

`SDK/CentralHub.SDK.Tests/Diagnostics/ReplayEngineTests.cs` — replay de Handshake/KeepAlive reais
contra servidor efêmero (confirma resposta correta), replay de um comando órfão de Tipo A (cai no
stub, confirma timeout reportado sem exceção), e replay contra endereço sem ninguém escutando
(confirma falha reportada sem lançar).

---

**Próximo documento:** [`17_PACKET_ANALYZER.md`](17_PACKET_ANALYZER.md)
**Índice:** [`00_INDEX.md`](00_INDEX.md)
