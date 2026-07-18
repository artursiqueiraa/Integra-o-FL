# 15 — Central Simulator

## Objetivo

Simular uma central JFL Active 100 Bus completa do lado cliente — handshake, keep-alive,
resposta aos comandos de superusuário, disparo de eventos, e cenários de falha (desconexão,
timeout, checksum inválido, pacote quebrado, reconexão) — sem depender de hardware físico.

## Onde fica

`Simulator/CentralHub.Simulator/` (projeto novo, referenciado em `CentralHub.sln`).

## Arquitetura

```
SimuladorActive100Bus
  ├── ConectarAsync(host, porta)      → TcpClient + handshake 0x21
  ├── LoopRecepcao (background)       → le todo pacote; correlaciona respostas
  │                                      pendentes por SEQ ou trata como comando
  │                                      de superusuario (auto-responde)
  ├── LoopKeepAlive (background)      → dispara 0x40 no intervalo negociado
  ├── DispararEventoAsync(...)        → envia 0x24 sob demanda
  ├── SimularDesconexao/Timeout/
  │   ChecksumInvalido/PacoteQuebrado → cenarios de falha injetaveis
  └── Estado: EstadoCentralSimulada   → particoes/zonas/PGMs/bateria/problemas,
                                         injetavel pelo chamador
```

Reaproveita **só** os utilitários genéricos de framing do SDK (`PacketBuilder`, `JflFrameReader`,
`ChecksumCalculator`, `JflCommand`, `JflModel`) — nunca `SessionManager`/`JflSession`, que são
conceitos do lado servidor. `EstadoCentralSimulada.MontarRespostaTelaMonitorar()` é o espelho
"escrever" do que `CentralStatusResponse.Parse` (SDK) "lê" — mesmo layout de bytes (§4.10), sem
duplicar nem alterar o parser homologado.

## Código

```csharp
await using var simulador = new SimuladorActive100Bus("1234567890");
var (liberado, keepMinutos) = await simulador.ConectarAsync("127.0.0.1", 8085, ct);

// Reage a comandos do servidor automaticamente (Status/Armar/PGM/Zonas/Data-Hora) e
// atualiza simulador.Estado de acordo.
simulador.ComandoRecebido += pacote => Console.WriteLine($"Cmd=0x{pacote.Cmd:X2}");

// Dispara um evento sob demanda.
await simulador.DispararEventoAsync("1130", particao: 1, usuarioOuZona: "001",
    contador: 100, spart: 0x02, comProblema: false, ct);

// Cenarios de falha.
simulador.SimularTimeout();          // proximo comando de superusuario nao sera respondido
simulador.SimularChecksumInvalido(); // proximo envio sai com checksum corrompido
simulador.SimularPacoteQuebrado();   // proximo envio sai truncado
simulador.SimularDesconexao();       // fecha o socket
await simulador.ReconectarAsync("127.0.0.1", 8085, ct);
```

## Uso via linha de comando

```powershell
dotnet run --project Simulator/CentralHub.Simulator -- 1234567890 127.0.0.1 8085
```

## Validação

`Simulator/CentralHub.Simulator.Tests/SimuladorActive100BusTests.cs` conecta o simulador contra
um `JflTcpServer` **real** (porta efêmera, mesma infraestrutura homologada do Backend) e confirma,
sem mocks: registro real no `SessionManager`, `PgmCommandService.AcionarAsync` real conseguindo
acionar a PGM do simulador, `CentralStatusQueryService` real lendo o estado do simulador, e
remoção correta da sessão após desconexão simulada. Essa é a prova de que o simulador fala o
protocolo de verdade, não é um duplo solto.

## FAQ

**P: O simulador reimplementa a lógica do `SessionManager`?**
R: Não — ele é só um cliente TCP que fala o protocolo. Toda a lógica de sessão/dispatch que ele
exercita é a real, do lado servidor, sem alteração.

**P: Por que `DispararEventoAsync` falha com timeout hoje?**
R: O handler real de Evento (0x24) só existe a partir da Fase 1 — até lá, o stub
(`EventoCommandHandlerStub`) só loga e nunca responde, então o simulador corretamente reporta
timeout. Comportamento esperado, não um bug do simulador.

---

**Próximo documento:** [`16_REPLAY.md`](16_REPLAY.md)
**Índice:** [`00_INDEX.md`](00_INDEX.md)
