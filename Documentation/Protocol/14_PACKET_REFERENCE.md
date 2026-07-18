# 14 — Catálogo de Pacotes (referência completa)

> Fonte única de verdade: `Documentação JFL Alarmes/Protocolo comunicação softwares
> monitoramento - publico.pdf` (revisão 17/07/2025). Todo byte documentado aqui foi conferido
> contra esse manual e, quando indicado, contra capturas reais nele incluídas (de uma Active 20
> Ethernet via módulo ME-05 — não é a Active 100 Bus deste projeto; ver
> `Documentation/RealCaptures/README.md` para a distinção). Active 100 Bus usa cabeçalho **0x7B**
> (não 0x7A) — os campos exclusivos do protocolo 0x7A não se aplicam a este hardware.

Convenções: `PART`/`PGM`/`ZONA` numerados a partir de 1. Checksum = XOR de todos os bytes do
pacote (incluindo o próprio K) deve fechar em zero (§2.5). `QDE` é o tamanho total do pacote
(inclui CAB+QDE+SEQ+CMD+DADOS+K).

---

## 3.1 — Conexão (`0x21` central de alarme, `0x2A` módulos M-300+/M-300 Flex)

**Fluxo:** Tipo B (central inicia, obrigatório, uma vez por conexão TCP).
**Implementado em:** `SDK/CentralHub.SDK/Jfl/Messages/ConnectionRequest.cs` (parse) +
`SDK/CentralHub.SDK/Jfl/Server/Handlers/ConnectionCommandHandler.cs` (handler real).

```
Envio:    0x21 NS(10) IMEI(15) MAC(12) MOD(1) VER(3) IP(1) SIMCARD(1) VIA(1) OPE(1) STATUS(variável)
Resposta: 0x21 RESULT(1) KEEP(1)
```
`RESULT`: `0x00`=bloqueado (equipamento derruba a conexão), `0x01`=liberado. `KEEP`: minutos até
o próximo keep-alive (1-20; fora da faixa assume 1). `MOD=0xA4` identifica Active 100 Bus.

**Captura real** (`Documentation/RealCaptures/Handshake.bin`):
```
TX[102]=7B 66 17 21 32 37 33 35 38 37 39 32 35 34 FF×15 39 38 46 34 41 42 36 45 46 34 46 30
         A3 36 30 30 01 01 01 06 [52 bytes de STATUS] 41
```
NS="2735879254", MOD=0xA3 (Active 20 Ethernet, hardware da captura original).

## 3.3 — KeepAlive (`0x40`)

**Fluxo:** Tipo B (central inicia periodicamente; toda resposta do servidor conta como keep-alive).
**Implementado em:** `SDK/CentralHub.SDK/Jfl/Server/Handlers/KeepAliveCommandHandler.cs`.

```
Envio:    0x40 (sem dados)
Resposta: 0x40 KEEP(1)
```
**Captura real** (`Documentation/RealCaptures/KeepAlive.bin`): `7B 05 18 40 26` (pedido) →
`7B 06 18 40 00 25` (resposta, KEEP=0x00 → assume 1 minuto por estar fora da faixa 1-20).

## 4.1 — Status (`0x4D`, superusuário)

**Fluxo:** Tipo A (servidor pergunta).
**Implementado em:** `SDK/CentralHub.SDK/Jfl/Server/CentralStatusQueryService.cs` +
`SDK/CentralHub.SDK/Jfl/Messages/Status/CentralStatusResponse.cs`.

```
Envio:    0x4D (sem dados)
Resposta: ver "Resposta da tela monitorar" abaixo (§4.10) — mesmo formato usado por Armar,
          Desarmar, PGM, Inibir Zonas e Data/Hora.
```

## 4.2-4.5, 4.7-4.9 — Comandos de superusuário com payload de 1-6 bytes

| Comando | CMD | Payload de envio | Status |
|---|---|---|---|
| Armar | `0x4E` | `PART(1)` | ✅ implementado (`ArmCommandService.ArmarAsync`, ver [`10_ARM.md`](10_ARM.md)) |
| Desarmar | `0x4F` | `PART(1)` | ✅ implementado (`ArmCommandService.DesarmarAsync`) |
| Acionar PGM | `0x50` | `PGM(1)` | ✅ implementado (`PgmCommandService.AcionarAsync`) |
| Desacionar PGM | `0x51` | `PGM(1)` | ✅ implementado (`PgmCommandService.DesacionarAsync`) |
| Inibir Zonas | `0x52` | `ZONA(13, bitmap)` | ✅ implementado (`ZoneInhibitCommandService.InibirZonasAsync`, ver [`11_ZONES.md`](11_ZONES.md)) |
| Armar Stay | `0x53` | `PART(1)` | ✅ implementado (`ArmCommandService.ArmarStayAsync`) |
| Armar Away | `0x54` | `PART(1)` | ✅ implementado (`ArmCommandService.ArmarAwayAsync`) |
| Atualizar Data/Hora | `0x55` | `HORA MIN SEG DIA MES ANO` (BCD) | 📋 planejado Fase 5, ver [`13_DATETIME.md`](13_DATETIME.md) |

`PART`: 1-16 = `0x01`-`0x10`; **`0x63` (99) é valor especial: opera o eletrificador** como se
fosse uma partição (confirmado por captura real — ver `Documentation/RealCaptures/Arme.bin` e
`Desarme.bin`, e o exemplo "COMANDO DE ARMAR O ELETRIFICADOR" do manual).

Todos respondem no formato "tela monitorar" (§4.10) — reaproveitado por
`CentralStatusResponse.Parse`, nunca reimplementado por comando.

**Capturas reais**: `PGM_ON.bin` (`7B 06 46 50 01 6A`), `PGM_OFF.bin` (`7B 06 4B 51 01 66`),
`Arme.bin` (`7B 06 42 4E 01 70`), `Desarme.bin` (`7B 06 44 4F 01 77`), `Zona.bin`
(`7B 12 4F 52 80 00×11 F4` — inibe só a zona 1), `DataHora.bin` (`7B 0B 4A 55 11 52 16 21 06 21 3C`).

## 4.10 — Resposta da "tela monitorar" (formato compartilhado)

Resposta a Status/Armar/Desarmar/PGM/Inibir Zonas/Armar Stay/Armar Away/Data-Hora — **113 bytes
mínimo** (+2 opcionais PGM2/P-PGM2 = 115), já 100% implementado e auditado (Fase 6) em
`CentralStatusResponse.cs`:

| Campo | Offset | Tamanho | Descrição |
|---|---|---|---|
| KP | 0 | 2 | Não usar. |
| HORA | 2 | 6 | Data/hora da central, BCD (dia,mês,ano,hora,min,seg). |
| BAT | 8 | 1 | 0=sem bateria; 1-100=lítio %; 101-210=chumbo (7,2-15V); 255=carregando. |
| PGM | 9 | 1 | Bitmap PGM 1-8 (bit0=PGM1). |
| PART | 10 | 16 | Estado de cada partição (`PartitionState`: 0x01 Desarmada, 0x02 Armada, 0x03 ArmadaStay, +0x80 em disparo). |
| ELET | 26 | 1 | Estado do eletrificador (`ElectrifierState`). |
| ZONA | 27 | 50 | Estado de cada zona, por nibble (`ZoneState`: 0 desabilitada, 1 inibida, 2 disparo, 3 sem comunicação, 4 curto, 5 tamper, 6 bateria baixa, 7 aberta, 8 fechada). Ordem do nibble dentro do par **não confirmada por hardware ainda** — ver [`../11_HARDWARE_VALIDATION.md`](../11_HARDWARE_VALIDATION.md) pendências. |
| PROB | 77 | 5 | 40 flags de problema (`ProblemFlags`). |
| P-ELET | 82 | 1 | Permissões do eletrificador. |
| P-PGM | 83 | 1 | Permissões PGM 1-8. |
| P-PART | 84 | 16 | Permissões por partição (desarmar/armar/stay/away/pronta). |
| P-INIB | 100 | 13 | Bitmap de permissão de inibir por zona — **LSB-first** (bit0=zona menor). |
| PGM2 | 113 | 1 | Opcional: bitmap PGM 9-16. |
| P-PGM2 | 114 | 1 | Opcional: permissões PGM 9-16. |

## 3.4 — Evento (`0x24`)

**Fluxo:** Tipo B (central inicia a qualquer momento) — **mandatório**.
**Status:** 📋 planejado Fase 1, especificação completa em [`09_EVENTS.md`](09_EVENTS.md).

## 5.1-5.5 — Comandos com senha (`0x37`)

Envelope para Armar/Desarmar (`0xC1`), Inibir/Desinibir Zonas (`0xC3`/`0xCF`), PGM (`0xC7`),
Consulta de Usuário (`0xC8`) e Programar Senha/Atributos (`0xC9`) — resposta curta `0x37 0x03
0xC0 RESP` (erro/confirmação) ou longa para `0xC8` com sucesso. **Status:** só `0xC8`/`0xC9`
estão no escopo atual (Fase 4, [`12_USERS.md`](12_USERS.md)) — `0xC1`/`0xC3`/`0xCF`/`0xC7` (Arme/
Zona/PGM com senha) são **decisão explícita de fora de escopo** (a superuser já cobre a mesma
funcionalidade; ver [`11_ZONES.md`](11_ZONES.md) para a justificativa).

---

**Próximo documento:** [`15_SIMULATOR.md`](15_SIMULATOR.md)
**Índice:** [`00_INDEX.md`](00_INDEX.md)
