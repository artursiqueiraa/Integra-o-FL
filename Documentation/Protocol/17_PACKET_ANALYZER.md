# 17 — Packet Analyzer / Packet Inspector

## Objetivo

Decompor qualquer pacote JFL 0x7B (bruto ou colado em hex) em campos nomeados — CAB, QDE, SEQ,
CMD, cada campo de DADOS com nome/offset/tamanho/valor bruto/valor interpretado/descrição —,
validar o checksum e identificar o comando. Usado tanto programaticamente (biblioteca) quanto
visualmente (página web).

## Onde fica

- **Biblioteca**: `SDK/CentralHub.SDK/Jfl/Diagnostics/PacketAnalyzer.cs`.
- **Backend**: `Backend/CentralHub.Api/Controllers/Dev/PacketInspectorController.cs`
  (`POST /api/dev/packet-inspector/analisar`).
- **Frontend**: `Frontend/src/pages/dev/PacketInspectorPage.tsx`, rota
  `/ferramentas/inspetor-pacotes`.

## Código

```csharp
var resultado = PacketAnalyzer.AnalisarHex("7B 05 18 40 26");
// resultado.CmdNome == "KeepAlive"
// resultado.ChecksumValido == true
// resultado.Campos: lista de CampoAnalisado (Nome/Offset/Tamanho/ValorBrutoHex/ValorInterpretado/Descricao)
```

Reaproveita os parsers de mensagem já existentes quando o `CMD` é reconhecido e o tamanho do
payload bate com um formato conhecido (`ConnectionRequest`, resposta de conexão, `KeepAlive`,
`CentralStatusResponse` para qualquer resposta "tela monitorar", bitmap de Inibir Zonas). Para
comandos ainda sem decodificação específica (Evento antes da Fase 1, comandos com senha antes da
Fase 4, etc.), cai graciosamente para exibir os bytes brutos de `DADOS` com um aviso — nunca
inventa uma interpretação sem um parser correspondente.

## Como usar a página web

1. Suba o Backend (`dotnet run` em `Backend/CentralHub.Api`) e o Frontend (`npm run dev`).
2. Acesse `/ferramentas/inspetor-pacotes` (botão "Inspetor de Pacotes" na barra superior).
3. Cole o pacote em hex (com ou sem espaços) — ex.: `7B 05 18 40 26`.
4. Veja a tabela de campos, o indicador de checksum, e os avisos (se houver).

## Validação

`SDK/CentralHub.SDK.Tests/Diagnostics/PacketAnalyzerTests.cs` — usa capturas **reais** do manual
oficial como fixtures (keep-alive, conexão de 102 bytes, inibir zonas), não dados inventados.
Cobre: decomposição correta de cada campo, checksum inválido sinalizado, CMD desconhecido
sinalizado, cabeçalho inválido não lança exceção.

## FAQ

**P: Por que uma resposta de Status "vira" uma lista enorme de campos (Partição 1-16, PGM 1-16,
Zonas)?**
R: É proposital — "tudo explicado" é o requisito. Zonas desabilitadas são resumidas numa única
linha (em vez de 99 linhas repetitivas) para não poluir a tabela; partições e PGMs, que são só
16, aparecem uma a uma sempre.

**P: A ferramenta tem autenticação?**
R: Não — o projeto inteiro ainda não tem autenticação em nenhum endpoint. O namespace
`Controllers.Dev` e a rota `/ferramentas/` deixam claro que é ferramenta de desenvolvimento, não
tela operacional.

---

**Próximo documento:** [`18_STRESS_TEST.md`](18_STRESS_TEST.md)
**Índice:** [`00_INDEX.md`](00_INDEX.md)
