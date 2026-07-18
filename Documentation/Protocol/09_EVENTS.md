# 09 — Eventos (0x24) — planejado, Fase 1

> Status: 📋 especificação confirmada contra o manual oficial e capturas reais; implementação
> pendente. Este documento existe para que a Fase 1 comece direto na implementação, sem
> reabrir a pesquisa do protocolo.

## Tipo de comando

**Tipo B** (central avisa, servidor reage) — a central inicia, sem que o servidor peça. É
**mandatório**: toda central JFL deve implementar e o servidor deve sempre responder.

## Formato (protocolo 0x7B — Active 100 Bus)

A Active 100 Bus usa cabeçalho 0x7B (tabela §1.3 do manual), então **só os campos base** se
aplicam — os campos estendidos de nome/localização/UUID (data-hora, campo-part, nome-part,
campo-u/z, nome-u/z, location, tipo-usuário, UUID, tipo-credencial, reservado) são exclusivos do
protocolo 0x7A e não são enviados por este hardware (confirmado: "A partir do 4º e 5º BYTE
USUÁRIO, somente os produtos com protocolo 7A enviam").

```
Envio:    0x24 CONTA(4,ASCII) EVENTO(4,ASCII) PART(2,ASCII) U/Z(3,ASCII) CONTADOR(4,HEX) SPART(1) PROB(1)
          = 19 bytes de DADOS
Resposta: 0x24 OK/ERRO(1) CONTADOR(4,ecoado)
          = 5 bytes de DADOS
```

| Campo | Tamanho | Codificação | Descrição |
|---|---|---|---|
| CONTA | 4 | ASCII (dígitos) | Conta da partição. |
| EVENTO | 4 | ASCII (dígitos) | Código Contact ID — **ASCII puro, 1 dígito por byte** (confirmado por captura real: `0x31 0x31 0x32 0x30` = "1120", não BCD/nibble apesar do texto do manual dizer "por nibble"). |
| PART | 2 | ASCII | Partição, "00" a "99". |
| U/Z | 3 | ASCII | Usuário ou zona — o protocolo base 0x7B não tem um campo separado dizendo qual é qual (isso só existe no 7A); decidir pela semântica do código Contact ID. |
| CONTADOR | 4 | **Hexadecimal puro** (não ASCII) | Identificador único do evento — deve ser ecoado byte a byte na resposta. |
| SPART | 1 | Byte de estado | Mesma tabela do byte PART de §4.10, mais valores de incêndio/acesso/portão (não aplicáveis à Active 100 Bus). |
| PROB | 1 | `0x00`/`0x01` | Se o equipamento está com problema. |

**Resposta**: `OK/ERRO` = `0x01` (decodificado) / `0x00` (não decodificado); `CONTADOR` ecoado.

## Captura real confirmada

```
Envio:    7B 18 21 24 30 30 30 31 31 31 32 30 30 31 30 30 30 00 00 57 FC 01 01 FF
          CONTA="0001" EVENTO="1120" PART="01" U/Z="000" CONTADOR=0x000057FC SPART=0x01 PROB=0x01
Resposta: 7B 0A 21 24 01 00 00 57 FC DE
          OK=0x01 CONTADOR=0x000057FC (ecoado)
```

## Achado crítico: tabela de códigos Contact ID incompleta neste repositório

O manual diz textualmente: *"No manual de instruções da central tem a tabela com todos os
eventos possíveis"* — um documento diferente (manual de instalação da Active 100 Bus), não
encontrado em nenhuma pasta `Documentação JFL Alarmes/` deste repositório. Só 2 códigos têm
significado confirmado no próprio texto do protocolo:

| Código | Significado |
|---|---|
| 1130 / E130 | Disparo de zona |
| 1401 / E401 | Desarme por usuário |

**Implementação deve nascer com um catálogo parcial e extensível** (`ContactIdCatalog`),
fallback "Desconhecido (código XXXX)" para qualquer código não catalogado — nunca inventar
significado sem fonte. Expandir com o manual de instalação oficial, ou incrementalmente com
códigos observados em hardware real (Fase 7).

## Cuidado de implementação

O handler roda **dentro do laço de leitura da sessão** (`JflTcpServer.HandleClientAsync`) — não
pode bloquear (ex.: escrita lenta em banco). Publicar a persistência como fire-and-forget
controlado, mesmo padrão já usado por `JflSessionPersistenceService`.

## Arquivos a criar (Fase 1)

- `SDK/CentralHub.SDK/Jfl/Messages/EventoRequest.cs` — parser dos 19 bytes acima.
- `SDK/CentralHub.SDK/Jfl/Messages/ContactIdCatalog.cs` — catálogo parcial de códigos.
- `SDK/CentralHub.SDK/Jfl/Server/EventoNotifier.cs` — bridge pub/sub (classe própria, não
  reaproveita nem altera `SessionManager`).
- `SDK/CentralHub.SDK/Jfl/Server/Handlers/EventoCommandHandler.cs` — **substitui de fato** o stub
  `EventoCommandHandlerStub` (único comando desta leva que tem o stub removido — é Tipo B).
- Backend: `Models/Evento.cs`, `Services/EventoPersistenceService.cs` (IHostedService irmão de
  `JflSessionPersistenceService`), `Services/EventoService.cs`, `DTOs/EventoDtos.cs`,
  `Controllers/EventoController.cs`.

---

**Índice:** [`00_INDEX.md`](00_INDEX.md)
