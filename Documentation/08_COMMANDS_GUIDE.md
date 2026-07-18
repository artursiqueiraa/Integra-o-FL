# 08 — COMMANDS GUIDE

> **Público-alvo:** referência técnica completa de cada comando implementado — use este documento
> quando precisar saber exatamente o formato de bytes de um comando específico, seja para depurar,
> seja para implementar um comando novo seguindo o mesmo padrão.

---

## Índice

1. [Convenções usadas neste documento](#1-convenções-usadas-neste-documento)
2. [Comando: Conexão (Handshake) — 0x21](#2-comando-conexão-handshake--0x21)
3. [Comando: Keep-Alive — 0x40](#3-comando-keep-alive--0x40)
4. [Comando: Status (superusuário) — 0x4D](#4-comando-status-superusuário--0x4d)
5. [Comando: Acionar PGM — 0x50](#5-comando-acionar-pgm--0x50)
6. [Comando: Desacionar PGM — 0x51](#6-comando-desacionar-pgm--0x51)
7. [Comando composto: Pulso (não é um comando de fio)](#7-comando-composto-pulso-não-é-um-comando-de-fio)
8. [Tabela mestra de todos os comandos](#8-tabela-mestra-de-todos-os-comandos)
9. [Casos de uso reais](#9-casos-de-uso-reais)
10. [Boas práticas](#10-boas-práticas)
11. [Problemas comuns](#11-problemas-comuns)
12. [Como testar cada comando](#12-como-testar-cada-comando)
13. [Como depurar cada comando](#13-como-depurar-cada-comando)
14. [FAQ](#14-faq)
15. [Checklist](#15-checklist)

---

## 1. Convenções usadas neste documento

- Todo byte é mostrado em hexadecimal, com o prefixo `0x` (ex.: `0x7B`).
- "Pedido" é sempre quem inicia o comando; "Resposta" é a réplica.
- Exemplos marcados como **[REAL]** foram capturados de verdade, seja do manual oficial da JFL,
  seja de logs reais gerados durante a homologação deste projeto contra hardware físico — nenhum
  exemplo neste documento é inventado.
- O checksum e o cálculo de `QDE` (tamanho) estão explicados em profundidade em
  [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md) — este documento foca no conteúdo de cada
  comando, não repete a mecânica de framing.

## 2. Comando: Conexão (Handshake) — 0x21

**Objetivo:** primeira mensagem que a central envia ao conectar; identifica quem ela é.

**Quem envia:** a Central. **Quando:** sempre a primeira coisa, logo após o TCP abrir.

**Implementado em:**
[`ConnectionCommandHandler.cs`](../SDK/CentralHub.SDK/Jfl/Server/Handlers/ConnectionCommandHandler.cs) +
[`ConnectionRequest.cs`](../SDK/CentralHub.SDK/Jfl/Messages/ConnectionRequest.cs).

### Payload do pedido (campo DADOS)

| Campo | Offset | Tamanho | Formato |
|---|---|---|---|
| NS (Número de Série) | 0 | 10 bytes | ASCII, só dígitos |
| IMEI | 10 | 15 bytes | ASCII, ou `0xFF` repetido = vazio |
| MAC | 25 | 12 bytes | ASCII hexadecimal (A-F maiúsculo) |
| MOD (Modelo) | 37 | 1 byte | `0xA4` = Active 100 Bus |
| VER (Versão) | 38 | 3 bytes | ASCII, 3 dígitos (ex.: `"650"` → "6.5") |
| IP | 41 | 1 byte | Qual IP de destino está em uso (1 ou 2) |
| SIMCARD | 42 | 1 byte | Qual chip está em uso |
| VIA | 43 | 1 byte | `0x00`=GPRS, `0x01`=Ethernet |
| OPE (Operadora) | 44 | 1 byte | Operadora de celular, se aplicável |
| STATUS | 45+ | resto do pacote | Payload opaco (não decodificado por este projeto) |

### Payload da resposta

| Campo | Tamanho | Formato |
|---|---|---|
| RESULT | 1 byte | `0x01` = liberado, `0x00` = bloqueado |
| KEEP | 1 byte | Minutos até o próximo keep-alive esperado (1-20; `0x00` = 1 min) |

### Exemplo **[REAL]** — capturado durante a homologação contra hardware físico (central real, número de série 2751484124, firmware 6.5)

```
Pedido (Central → Servidor), 102 bytes:
7B 66 01 21 32 37 35 31 34 38 34 31 32 34 FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF
38 43 34 46 30 30 30 41 37 33 34 38 A4 36 35 30 01 01 01 06 00 01 01 00 01 00 02 00 03
00 04 00 05 00 06 00 07 00 08 00 09 00 10 00 11 00 12 00 13 00 14 00 15 00 16 00 02 00
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00

Resposta (Servidor → Central), 7 bytes:
7B 07 01 21 01 05 58
```

**Decodificação byte a byte da resposta:**

| Byte | Valor | Significado |
|---|---|---|
| 1º | `0x7B` | Cabeçalho |
| 2º | `0x07` | Tamanho total: 7 bytes |
| 3º | `0x01` | SEQ — o mesmo do pedido, ecoado |
| 4º | `0x21` | CMD — Conexão |
| 5º | `0x01` | RESULT — Liberado |
| 6º | `0x05` | KEEP — 5 minutos |
| 7º | `0x58` | Checksum |

E do pedido, os campos de identificação decodificados: `NS = "2751484124"`, `MAC =
"8C4F000A7348"`, `MOD = 0xA4` (Active 100 Bus), `VER = "650"` → `"6.5"`, `VIA = 0x01` (Ethernet).

**Timeout:** não aplicável — este comando é sempre iniciado pela central, o servidor apenas
responde assim que processa; não há uma espera ativa por parte do CentralHub.

## 3. Comando: Keep-Alive — 0x40

**Objetivo:** confirmar que a conexão continua viva.

**Quem envia:** a Central, periodicamente. **Implementado em:**
[`KeepAliveCommandHandler.cs`](../SDK/CentralHub.SDK/Jfl/Server/Handlers/KeepAliveCommandHandler.cs).

**Payload do pedido:** nenhum (0 bytes de DADOS).
**Payload da resposta:** 1 byte, `KEEP` (mesmo significado do handshake).

### Exemplo **[REAL]** — do manual oficial da JFL

```
Pedido:    7B 05 18 40 26
Resposta:  7B 06 18 40 00 25
```

Decodificação da resposta: `SEQ=0x18` (ecoado), `CMD=0x40`, `KEEP=0x00` (→ 1 minuto),
`checksum=0x25`.

**Timeout:** não aplicável (a central inicia; o servidor só responde).

## 4. Comando: Status (superusuário) — 0x4D

**Objetivo:** obter uma "fotografia" completa do estado da central: 16 partições, 99 zonas, 16
PGMs, eletrificador, bateria, alimentação AC, e um mapa de 40 bits de problemas.

**Quem envia:** o CentralHub (Servidor). **Implementado em:**
[`CentralStatusQueryService.cs`](../SDK/CentralHub.SDK/Jfl/Server/CentralStatusQueryService.cs) +
[`CentralStatusResponse.cs`](../SDK/CentralHub.SDK/Jfl/Messages/Status/CentralStatusResponse.cs).

**Payload do pedido:** nenhum.

### Payload da resposta (formato "tela monitorar", seção 4.10 do manual oficial)

| Campo | Offset (nos Dados) | Tamanho |
|---|---|---|
| KP (não usado) | 0 | 2 bytes |
| HORA (data/hora da central, BCD) | 2 | 6 bytes |
| BAT (bateria) | 8 | 1 byte |
| PGM (estado das PGMs 1-8) | 9 | 1 byte |
| PART (estado de cada uma das 16 partições) | 10 | 16 bytes |
| ELET (eletrificador) | 26 | 1 byte |
| ZONA (estado de cada zona, por nibble) | 27 | 50 bytes |
| PROB (5 bytes de flags de problema) | 77 | 5 bytes |
| P-ELET (permissões do eletrificador) | 82 | 1 byte |
| P-PGM (permissões das PGMs 1-8) | 83 | 1 byte |
| P-PART (permissões de cada partição) | 84 | 16 bytes |
| P-INIB (quais zonas podem ser inibidas) | 100 | 13 bytes |
| PGM2 (estado das PGMs 9-16) — **opcional** | 113 | 1 byte |
| P-PGM2 (permissões das PGMs 9-16) — **opcional** | 114 | 1 byte |

> `PGM2`/`P-PGM2` são tratados como **opcionais**: se a resposta tiver só 113 bytes de dados (sem
> eles), o parser assume PGMs 9-16 como desligadas/sem permissão, em vez de falhar — isso segue
> orientação explícita do próprio manual da JFL sobre compatibilidade com equipamentos/firmwares
> mais antigos que não têm 16 PGMs.

### Exemplo **[REAL]** — capturado do manual oficial (resposta a um comando de "armar partição A")

```
7B 76 42 4E 01 79 21 06 21 11 51 39 00 00 02 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00
04 77 77 77 77 70 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
00 00 00 00 00 00 00 00 00 00 00 00 00 00 60 00 00 00 00 0F 1B 1B 00 00 00 00 00 00 00 00
00 00 00 00 00 00 FF 01 00 00 00 00 00 00 00 00 00 00 00 E0
```

Interpretação de alguns campos-chave: `PART[0] = 0x02` → Partição 1 = **Armada** (bate com o
comando que gerou essa resposta); `PROB[1] = 0x60` → bits 5 e 6 setados → problemas de "Sirene" e
"Bateria"; `P-PGM = 0x0F` → PGMs 1-4 permitidas (essa captura é de uma central com só 4 PGMs).

**Timeout:** 10 segundos, por padrão (`CentralStatusQueryService`, constante `TimeoutPadrao`).

## 5. Comando: Acionar PGM — 0x50

**Objetivo:** ligar uma saída PGM específica.

**Implementado em:**
[`PgmCommandService.cs`](../SDK/CentralHub.SDK/Jfl/Server/PgmCommandService.cs).

**Payload do pedido:** 1 byte — o número da PGM (`0x01` a `0x10`, ou seja, 1 a 16).
**Payload da resposta:** o mesmo formato completo do comando 0x4D (seção 4) — o CentralHub verifica
se a PGM pedida aparece como "acionada" na resposta, para confirmar sucesso.

### Exemplo **[REAL]** — validado em teste de integração automatizado deste projeto (socket TCP real)

```
Pedido:    7B 06 SEQ 50 04 K       (PGM 4, onde SEQ e K variam a cada execução)
Resposta:  7B 73 SEQ 50 ... (payload de status completo, com o bit da PGM4 setado) ... K
```

**Timeout:** 10 segundos, por padrão.

## 6. Comando: Desacionar PGM — 0x51

Idêntico ao 0x50 em estrutura — só muda o `CMD` (`0x51`) e o significado (desligar em vez de
ligar). Ver seção 5.

## 7. Comando composto: Pulso (não é um comando de fio)

> Não existe, em nenhum lugar da documentação oficial da JFL, um byte de comando dedicado a
> "Pulso". O CentralHub implementa Pulso (`PgmCommandService.PulsoAsync`) como:

```
1. Envia Acionar PGM (0x50) e espera a confirmação.
2. Se confirmado: Task.Delay(duracaoMs)  — aguarda o tempo pedido, sem enviar nada.
3. Envia Desacionar PGM (0x51) e espera a confirmação.
4. Se o passo 1 falhar, os passos 2 e 3 nunca acontecem.
```

Isso foi validado com um teste automatizado que mede o tempo real decorrido, confirmando que o
`Task.Delay` realmente pausa a execução pelo tempo pedido (não é um retorno imediato "fingindo" que
esperou).

## 8. Tabela mestra de todos os comandos

| CMD | Nome | Quem inicia | Dados no pedido | Dados na resposta | Timeout | Implementado? |
|---|---|---|---|---|---|---|
| `0x21` | Conexão | Central | ~97 bytes (identificação) | 2 bytes (RESULT+KEEP) | N/A | ✅ |
| `0x40` | Keep-Alive | Central | 0 bytes | 1 byte (KEEP) | N/A | ✅ |
| `0x4D` | Status | Servidor | 0 bytes | até 115 bytes | 10s | ✅ |
| `0x50` | Acionar PGM | Servidor | 1 byte | até 115 bytes | 10s | ✅ |
| `0x51` | Desacionar PGM | Servidor | 1 byte | até 115 bytes | 10s | ✅ |
| `0x24` | Evento | Central | variável (Contact ID) | 5 bytes (OK+contador) | N/A | ❌ Stub |
| `0x93` | Pedir Status (leve) | Servidor | 0 bytes | variável | N/A | ❌ Stub |
| `0x4E`/`0x4F`/`0x53`/`0x54` | Armar/Desarmar/Stay/Away | Servidor | 1 byte (partição) | até 115 bytes | 10s | ✅ |
| `0x52` | Inibir zonas | Servidor | 13 bytes (bitmap) | até 115 bytes | 10s | ✅ |
| `0x37` | Comandos com senha (envelope) | Servidor | variável | 4 bytes (resultado curto) | N/A | ❌ Stub |

## 9. Casos de uso reais

Ver [`04_PROTOCOL_FLOW.md`](04_PROTOCOL_FLOW.md), seções 10 e 11, para o fluxo completo de uma
consulta de Status e de um comando de PGM, incluindo o papel de cada camada de código.

## 10. Boas práticas

- Sempre reaproveitar `CentralStatusResponse.Parse` para qualquer comando novo que retorne o
  formato "tela monitorar" — não duplicar a lógica de parsing.
- Sempre confirmar sucesso comparando o **estado observado na resposta**, nunca assumindo sucesso
  só porque o pacote chegou sem erro de checksum — o protocolo de superusuário não tem um código de
  erro explícito por comando, ao contrário do envelope com senha (`0x37`).

## 11. Problemas comuns

- **PGM não confirma mesmo depois de "acionar"** → verificar se aquela PGM tem permissão
  configurada na central (campo `P-PGM`/`P-PGM2` da resposta) — sem permissão, a central
  simplesmente ignora o comando e a PGM nunca muda de estado.
- **Status demora exatos 10 segundos e falha** → sintoma clássico de timeout — verificar se a
  sessão realmente está ativa (`SessionManager.TryGet`) antes de suspeitar do comando em si.

## 12. Como testar cada comando

Testes unitários (sem hardware, usando duplos de teste) para cada comando estão em:
- 0x21: `SDK/CentralHub.SDK.Tests/Server/Handlers/ConnectionCommandHandlerTests.cs`
- 0x40: `SDK/CentralHub.SDK.Tests/Server/Handlers/KeepAliveCommandHandlerTests.cs`
- 0x4D: `SDK/CentralHub.SDK.Tests/Server/CentralStatusQueryServiceTests.cs`
- 0x50/0x51: `SDK/CentralHub.SDK.Tests/Server/PgmCommandServiceTests.cs`

Teste de integração com socket TCP real (todos os comandos, ponta a ponta):
`SDK/CentralHub.SDK.Tests/Server/JflTcpServerIntegrationTests.cs`.

## 13. Como depurar cada comando

Todo pacote recebido é logado em nível `Debug`, incluindo os bytes brutos em hexadecimal — ver
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md), seção 18.

## 14. FAQ

**P: Por que a resposta do comando de PGM é tão grande (mais de 100 bytes) para uma pergunta tão
simples?**
R: Porque o protocolo da JFL reaproveita a mesma resposta "completa" para todos os comandos da
"tela monitorar" — em vez de ter um formato de resposta diferente para cada comando, a central
sempre manda uma fotografia inteira do seu estado atual. Isso, na prática, também serve como uma
consulta de Status "de brinde" a cada comando de PGM.

**P: O que acontece se eu mandar um número de PGM que não existe (ex.: 20)?**
R: O `PgmCommandService` valida a faixa (1-16) **antes** de sequer tentar enviar, devolvendo
`PgmCommandFailureReason.NumeroInvalido` imediatamente — nenhum byte chega a ser enviado à central.

## 15. Checklist

- [ ] Sei o formato completo do pedido e da resposta de cada um dos 5 comandos implementados.
- [ ] Sei por que a resposta de PGM reaproveita o formato de Status.
- [ ] Sei onde encontrar exemplos reais capturados para cada comando.
- [ ] Sei os arquivos de teste correspondentes a cada comando.

---

**Documento anterior:** [`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md)
**Próximo documento:** [`09_WEB_GUIDE.md`](09_WEB_GUIDE.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
