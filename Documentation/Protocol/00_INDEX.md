# Documentation/Protocol — Índice

Série de referência dedicada ao protocolo JFL e às ferramentas de homologação criadas para
fechar a integração com a Active 100 Bus por completo (ver plano de homologação). Complementa,
sem substituir, a documentação geral já existente em [`../INDEX.md`](../INDEX.md) — em especial
[`02_JFL_PROTOCOL_GUIDE.md`](../02_JFL_PROTOCOL_GUIDE.md),
[`03_NETWORK_ARCHITECTURE.md`](../03_NETWORK_ARCHITECTURE.md) e
[`10_HOW_TO_ADD_NEW_COMMAND.md`](../10_HOW_TO_ADD_NEW_COMMAND.md), que continuam sendo a
referência de arquitetura/padrões de código do projeto.

## Estado de cada documento

| # | Documento | Status |
|---|---|---|
| 00 | [Índice](00_INDEX.md) | ✅ |
| 01 | Arquitetura | ⏳ pendente |
| 02 | [Rede e captura (Wireshark)](02_NETWORK.md) | ✅ |
| 03 | Protocolo (framing 0x7B) | ⏳ pendente (ver `../02_JFL_PROTOCOL_GUIDE.md` enquanto isso) |
| 04 | SessionManager | ⏳ pendente (ver `../07_SESSION_MANAGER_GUIDE.md` enquanto isso) |
| 05 | Handshake | ⏳ pendente |
| 06 | KeepAlive | ⏳ pendente |
| 07 | Status | ⏳ pendente |
| 08 | PGM | ⏳ pendente |
| 09 | [Eventos (planejado — Fase 1)](09_EVENTS.md) | 📋 especificação pronta, implementação pendente |
| 10 | [Arme (implementado — Fase 2)](10_ARM.md) | ✅ implementado (hardware real pendente) |
| 11 | [Zonas (implementado — Fase 3)](11_ZONES.md) | ✅ implementado (hardware real pendente) |
| 12 | [Usuários (planejado — Fase 4)](12_USERS.md) | 📋 especificação pronta, implementação pendente |
| 13 | [Data/Hora (planejado — Fase 5)](13_DATETIME.md) | 📋 especificação pronta, implementação pendente |
| 14 | [Catálogo de pacotes](14_PACKET_REFERENCE.md) | ✅ (comandos já homologados) |
| 15 | [Central Simulator](15_SIMULATOR.md) | ✅ |
| 16 | [Replay Engine](16_REPLAY.md) | ✅ |
| 17 | [Packet Analyzer / Inspector](17_PACKET_ANALYZER.md) | ✅ |
| 18 | [Stress Test](18_STRESS_TEST.md) | ✅ |
| 19 | [Benchmark](19_BENCHMARK.md) | ✅ |
| 20 | [Changelog](20_CHANGELOG.md) | ✅ (atualizado a cada fase) |

## Por onde começar

- **Quer entender um comando específico do protocolo?** → [`14_PACKET_REFERENCE.md`](14_PACKET_REFERENCE.md)
  (comandos já implementados) ou o documento planejado correspondente (09-13, para os que ainda
  faltam).
- **Vai homologar um comando novo contra hardware real?** → [`02_NETWORK.md`](02_NETWORK.md)
  (como capturar com Wireshark) + [`17_PACKET_ANALYZER.md`](17_PACKET_ANALYZER.md) (como
  decodificar o que capturou).
- **Quer testar sem hardware físico?** → [`15_SIMULATOR.md`](15_SIMULATOR.md) (simula uma
  central) + [`16_REPLAY.md`](16_REPLAY.md) (reproduz uma captura salva).
- **Quer saber o que mudou e quando?** → [`20_CHANGELOG.md`](20_CHANGELOG.md).

## Convenção

Todo comando novo implementado a partir daqui segue exatamente o processo já documentado em
[`../10_HOW_TO_ADD_NEW_COMMAND.md`](../10_HOW_TO_ADD_NEW_COMMAND.md) — esta série de documentos
só adiciona a referência de protocolo (bytes, capturas reais) e as ferramentas, não substitui
aquele guia de processo.
