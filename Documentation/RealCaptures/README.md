# RealCaptures — Capturas reais do protocolo JFL

Cada arquivo `.bin` contém os bytes brutos (sem hex-string, sem separadores) de um pacote 0x7B
real, um por comando, catalogados como parte da Fase 0.3 do plano de homologação. Servem de
fixture para o Packet Inspector, o Replay Engine (Fase 0.4) e os testes automatizados que
comparam byte a byte contra o que o manual documenta.

## Proveniência

| Arquivo | Comando | Origem | Hardware/firmware de origem |
|---|---|---|---|
| `Handshake.bin` | Conexão (0x21) | Manual oficial, §3.5 | Active 20 Ethernet (via módulo ME-05) |
| `KeepAlive.bin` | KeepAlive (0x40) | Manual oficial, §3.5 | Active 20 Ethernet (via módulo ME-05) |
| `Evento.bin` | Evento (0x24) | Manual oficial, §3.5 | Active 20 Ethernet (via módulo ME-05) |
| `PGM_ON.bin` | Acionar PGM (0x50) | Manual oficial, §4.11 | Active 20 Ethernet (via módulo ME-05) |
| `PGM_OFF.bin` | Desacionar PGM (0x51) | Manual oficial, §4.11 | Active 20 Ethernet (via módulo ME-05) |
| `Arme.bin` | Armar (0x4E) | Manual oficial, §4.11 | Active 20 Ethernet (via módulo ME-05) |
| `Desarme.bin` | Desarmar (0x4F) | Manual oficial, §4.11 | Active 20 Ethernet (via módulo ME-05) |
| `Zona.bin` | Inibir Zonas (0x52) | Manual oficial, §4.11 | Active 20 Ethernet (via módulo ME-05) |
| `DataHora.bin` | Atualizar Data/Hora (0x55) | Manual oficial, §4.11 | Active 20 Ethernet (via módulo ME-05) |

Todos os arquivos acima foram transcritos byte a byte do texto do manual
(`Documentação JFL Alarmes/Protocolo comunicação softwares monitoramento - publico.pdf`, revisão
17/07/2025) e **validados automaticamente**: o checksum XOR de cada um fecha em zero (ver
`seed_captures.py`, script de geração usado uma única vez para criar estes arquivos — não faz
parte do build, mantido só como registro de como as capturas nasceram).

**Nenhum destes vem do hardware Active 100 Bus deste projeto** — são exemplos reais, mas de outro
modelo (Active 20 Ethernet) e outra sessão de captura, incluídos no próprio manual oficial da JFL.
Servem como seed inicial para os testes/ferramentas funcionarem desde já, sem esperar a Fase 7.

## Pendentes (placeholders vazios até a Fase 7 — hardware real do projeto)

| Arquivo | Motivo |
|---|---|
| `Status.bin` | O manual não trouxe uma captura completa de 113+ bytes da resposta de status. |
| `Stay.bin` | Sem captura de exemplo do comando Armar Stay (0x53) no manual. |
| `Away.bin` | Sem captura de exemplo do comando Armar Away (0x54) no manual. |
| `Usuario.bin` | Sem captura de exemplo dos comandos de usuário (0x37/0xC8/0xC9) no manual. |
| `PGM_PULSE.bin` | "Pulso" não é um comando de fio — é Acionar (`PGM_ON.bin`) seguido de Desacionar (`PGM_OFF.bin`) após um intervalo, implementado em software (ver `PgmCommandService.PulsoAsync`). Não existe captura única para isso; fica vazio de propósito. |

Ao homologar cada comando pendente contra a Active 100 Bus real (Fase 7), **substituir** o
arquivo placeholder correspondente pela captura genuína — nunca misturar uma captura de outro
modelo com uma do hardware real do projeto sem deixar claro na tabela acima.

## Como usar

- **Packet Inspector** (`/ferramentas/inspetor-pacotes`): abra o `.bin` em qualquer editor hex,
  copie os bytes, cole na ferramenta.
- **Replay Engine** (Fase 0.4): `ReplayEngine.ReplayAsync("Documentation/RealCaptures/PGM_ON.bin", ...)`.
- **Testes**: usar como fixture, comparando a saída do parser contra o que o manual documenta
  para aquele mesmo exemplo.
