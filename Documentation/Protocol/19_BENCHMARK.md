# 19 — Benchmark

## Objetivo

Medir o custo (tempo, alocação) das operações centrais do protocolo — checksum, montagem/parse
de pacote, leitura incremental de frame, parse da resposta de status, e o pipeline de sessão
completo via socket real — como referência para detectar regressões de performance nas próximas
fases.

## Onde fica

`SDK/CentralHub.SDK.Benchmarks/` (projeto [BenchmarkDotNet](https://benchmarkdotnet.org/)).

- `ProtocoloBenchmarks.cs` — operações isoladas: `ChecksumCalculator`, `PacketBuilder`,
  `PacketParser`, `CentralStatusResponse.Parse`.
- `SessaoBenchmarks.cs` — pipeline completo via socket real (mesma infraestrutura homologada:
  `JflTcpServer` efêmero + [Central Simulator](15_SIMULATOR.md)): `PgmCommandService.AcionarAsync`
  e keep-alive, ponta a ponta.

## Como rodar

```powershell
dotnet run --project SDK/CentralHub.SDK.Benchmarks -c Release
```

BenchmarkDotNet exige build Release (recusa rodar em Debug) e roda várias iterações com
aquecimento — a execução completa leva minutos, não segundos. Para uma checagem rápida durante
desenvolvimento, use `--filter` e `--job Dry` (1 iteração, sem rigor estatístico, só para
confirmar que roda):

```powershell
dotnet run --project SDK/CentralHub.SDK.Benchmarks -c Release -- --filter "*Checksum*" --job Dry
```

O resultado (tabela Markdown) é gerado em `BenchmarkDotNet.Artifacts/results/` — colar a tabela
relevante aqui neste documento após cada execução completa relevante (ex.: antes/depois de uma
mudança que possa afetar performance).

## Validado

Smoke-test executado (`--job Dry`, 1 iteração) durante o desenvolvimento — confirma que o
executável builda e roda corretamente contra o código real (0 erros, benchmark completou e
exportou os artefatos). Uma execução completa (todas as iterações, todos os benchmarks) ainda não
foi registrada aqui — fica para quando houver um baseline de performance real para comparar
(ex.: antes/depois de otimizações, ou como parte do relatório final da Fase 7).

---

**Próximo documento:** [`20_CHANGELOG.md`](20_CHANGELOG.md)
**Índice:** [`00_INDEX.md`](00_INDEX.md)
