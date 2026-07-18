# 18 — Stress Test

## Objetivo

Gerar carga (conexões, keep-alives, eventos, consultas de status, comandos de PGM) e cenários de
falha (reconexão, timeout, checksum inválido, pacote quebrado) contra a infraestrutura JFL real,
medindo latência por categoria e uso de memória — sem precisar de hardware físico nem do Backend
real rodando.

## Onde fica

`Simulator/CentralHub.StressTest/` (projeto console novo).

## Como funciona

Sobe um `JflTcpServer` efêmero (mesma extensão `AddJflServer` do Backend real), conecta N
[Central Simulators](15_SIMULATOR.md) concorrentemente, e usa os próprios serviços do SDK
(`PgmCommandService`, `CentralStatusQueryService`) — os mesmos que o Backend real usa — para
gerar consultas/comandos contra eles. Isso significa que o Stress Test exercita o pipeline
completo e homologado (dispatcher, handlers, sessão), não uma via alternativa simplificada.

## Uso

```powershell
dotnet run --project Simulator/CentralHub.StressTest -- `
    --conexoes 100 --keepalives 1000 --eventos 500 --consultas 500 --pgms 100 `
    --saida Documentation/RealCaptures/StressTestResults/resultado.md
```

Todos os parâmetros são opcionais (os valores acima são o padrão). Sem `--saida`, o relatório vai
para `Documentation/RealCaptures/StressTestResults/<timestamp>.md`.

## O que o relatório contém

- Latência por categoria (conexão, keep-alive, evento, consulta, PGM): amostras, média, P95, P99,
  máximo.
- Contagem de falhas por categoria (eventos sem confirmação são **esperados** até a Fase 1
  implementar o handler real de Evento — o relatório já sinaliza isso, não é uma regressão).
- Resultado dos cenários de falha: reconexão após desconexão simulada, timeout de comando
  corretamente reportado como falha (sem travar o servidor), checksum inválido / pacote quebrado
  enviados e descartados sem exceção não tratada.
- Delta de memória do processo da própria ferramenta antes/depois da carga.

## Validado

Executado manualmente (15 conexões, volumes reduzidos) durante o desenvolvimento — conectou,
gerou carga em todas as categorias, executou os 4 cenários de falha sem exceção não tratada, e
produziu o relatório em Markdown corretamente. Uma execução completa nos volumes padrão (100
conexões / 1000 keep-alives / etc.) fica para a Fase 7, junto com a homologação em hardware real.

---

**Próximo documento:** [`19_BENCHMARK.md`](19_BENCHMARK.md)
**Índice:** [`00_INDEX.md`](00_INDEX.md)
