# 14 — ROADMAP

> **Público-alvo:** qualquer pessoa planejando o próximo passo do projeto — o que já existe, o que
> falta, e em que ordem faz sentido implementar o que falta, com a justificativa arquitetural de
> cada prioridade.

---

## Índice

1. [O que já está implementado e homologado](#1-o-que-já-está-implementado-e-homologado)
2. [O que existe apenas como stub](#2-o-que-existe-apenas-como-stub)
3. [O que não existe ainda](#3-o-que-não-existe-ainda)
4. [Prioridades sugeridas](#4-prioridades-sugeridas)
5. [Eventos em tempo real](#5-eventos-em-tempo-real)
6. [SignalR / WebSocket (substituindo polling)](#6-signalr--websocket-substituindo-polling)
7. [Arme / Desarme / Zonas — concluído](#7-arme--desarme--zonas--concluído)
8. [Usuários](#8-usuários)
9. [Programação remota](#9-programação-remota)
10. [Suporte a Intelbras e outros fabricantes](#10-suporte-a-intelbras-e-outros-fabricantes)
11. [Multi-Tenant real](#11-multi-tenant-real)
12. [Migração para EF Core Migrations formais](#12-migração-para-ef-core-migrations-formais)
13. [Remoção do código legado](#13-remoção-do-código-legado)
14. [Boas práticas para quem for implementar o roadmap](#14-boas-práticas-para-quem-for-implementar-o-roadmap)
15. [FAQ](#15-faq)
16. [Checklist](#16-checklist)

---

## 1. O que já está implementado e homologado

| Funcionalidade | Status |
|---|---|
| Servidor TCP (arquitetura correta: central conecta no CentralHub) | ✅ Homologado contra hardware real |
| Handshake (0x21) | ✅ Homologado contra hardware real |
| Keep-Alive (0x40) | ✅ Homologado contra hardware real |
| Persistência de sessão + vínculo automático ao cadastro | ✅ Homologado contra hardware real |
| Status completo (0x4D: partições, zonas, PGMs, eletrificador, bateria, AC, problemas) | ✅ Homologado contra hardware real |
| PGM (Ligar/Desligar/Pulso) | ✅ Homologado contra simulação fiel (ver [`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md)) |
| Arme (Armar/Desarmar/Stay/Away, incluindo eletrificador) | ✅ Testado contra simulação fiel — hardware real pendente |
| Zonas (Inibir/Desinibir/Consultar) | ✅ Testado contra simulação fiel — hardware real pendente |
| Interface web (Tela Central, Status, Painel de PGM, Painel de Arme, Zonas clicáveis) | ✅ Implementada e testada |
| Documentação completa | ✅ Este conjunto de documentos |

## 2. O que existe apenas como stub

Estes comandos são recebidos e reconhecidos pelo servidor (não quebram a conexão), mas não fazem
nada além de confirmar o recebimento — ver
[`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md):

- `0x24` — Evento
- `0x93` — Pedir status (formato curto)
- `0x37` — Envelope de comandos com senha (usuários)

(`0x4E`/`0x4F`/`0x53`/`0x54` — Arme/Desarme — e `0x52` — Inibir zonas — **saíram desta lista**:
implementados na Fase 2/3, ver seção 1 acima. Os stubs `ArmCommandHandlerStub`/
`ZoneCommandHandlerStub` continuam registrados, mas agora só como rede de segurança para respostas
órfãs/atrasadas, o mesmo papel que `PgmCommandHandlerStub` já cumpria ao lado do `PgmCommandService`
real.)

## 3. O que não existe ainda

- Eventos em tempo real processados e persistidos (histórico de disparos, aberturas, etc.)
- Push em tempo real para o frontend (hoje é só polling)
- Gestão de usuários da central
- Programação remota (zonas, partições, configurações da central via CentralHub)
- Suporte real a outros fabricantes (Intelbras, etc. — hoje são apenas mocks legados)
- Multi-Tenant real (isolamento de dados por cliente/prédio)
- Migrations formais do EF Core (hoje é `EnsureCreated()`)
- Empacotamento para deploy (Docker, CI/CD)

## 4. Prioridades sugeridas

```
Prioridade 1 (maior valor, menor risco):
  → Migrar para EF Core Migrations formais (evita futuras dores de cabeça de schema)
  → Eventos em tempo real (alto valor operacional, reaproveita muito do parser já existente)

Prioridade 2 (valor alto, mais esforço):
  → SignalR/WebSocket (melhora experiência, substitui polling)

Prioridade 3 (valor médio):
  → Usuários (envelope de protocolo diferente, mais complexo)
  → Programação remota (escopo grande, potencialmente arriscado)

Prioridade 4 (estratégico, não urgente):
  → Multi-Tenant real
  → Suporte a outros fabricantes
  → Remoção do código legado
```

## 5. Eventos em tempo real

**Por que priorizar:** o comando `0x24` já está identificado e roteado — falta apenas escrever o
parser do payload de evento e persistir/expor os dados. É o comando Tipo B mais próximo de virar
realidade, e o guia [`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md), seção sobre
Tipo B, já descreve o caminho.

**O que precisa:** parser do payload de evento (consultar seção do manual da JFL sobre 0x24),
tabela `Events` no banco, endpoint de consulta de histórico, e — idealmente — o mecanismo de push
da seção 6 abaixo para notificar a interface web imediatamente.

## 6. SignalR / WebSocket (substituindo polling)

**Por que ainda não existe:** o polling (seção 9 do doc 09) foi a escolha inicial mais simples para
entregar a interface funcional rapidamente — não é uma limitação técnica do protocolo JFL, é uma
simplificação deliberada do lado web.

**Caminho de implementação:** introduzir SignalR no Backend, com um Hub que emite eventos quando o
`SessionManager` dispara `SessaoRegistrada`/`SessaoRemovida`/`AtividadeAtualizada` (ver
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md)) e quando um evento 0x24 chega — o
frontend assina o Hub em vez de fazer `setInterval`.

## 7. Arme / Desarme / Zonas — ✅ concluído

**Status:** implementado (`ArmCommandService`/`ZoneInhibitCommandService` no SDK,
`ArmService`/`ZoneInhibitService` no Backend, `ArmPanel`/`ZonasPanel` no Frontend) e testado
ponta a ponta contra um `JflTcpServer` real e o Central Simulator — ver
[`10_ARM.md`](Protocol/10_ARM.md) e [`11_ZONES.md`](Protocol/11_ZONES.md) para os detalhes
técnicos, exemplos reais do manual e a lista completa de arquivos.

**O que falta para fechar como definitivo:** validação contra hardware real (Active 100 Bus
física), na mesma linha do que já foi feito para Handshake/KeepAlive/Status/PGM — ver
[`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md). Dois pontos específicos merecem atenção
nessa validação: (1) se "Armada Away" realmente compartilha o mesmo byte de estado que "Armada"
normal (o manual não documenta um estado de fio separado); (2) a ordem dos nibbles dentro de cada
byte do campo ZONA da resposta de status (não tem exemplo numérico explícito no manual).

## 8. Usuários

**Por que é mais complexo que Arme/PGM:** usa um envelope de protocolo diferente (`0x37`, com senha
e resposta curta `0x37 0x03 0xC0 RESP`), exigindo um parser específico — ver seção correspondente em
[`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md).

## 9. Programação remota

**Escopo maior e mais arriscado:** envolve alterar configurações persistentes da central (zonas,
partições) via rede — recomenda-se tratar com o mesmo cuidado dado à decisão de não testar PGM
contra hardware real sem certeza do que está fisicamente conectado (ver
[`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md)).

## 10. Suporte a Intelbras e outros fabricantes

Existem classes legadas (`IntelbrasAdapter`, `JflAdapter` antigo) da arquitetura anterior — não
devem ser reaproveitadas como base, pois foram desenhadas para o modelo de conexão de saída
(incorreto). Um novo suporte a fabricante deveria seguir a mesma abstração de "servidor TCP +
parser específico do protocolo daquele fabricante" usada para JFL.

## 11. Multi-Tenant real

Hoje `Buildings` é uma tabela simples sem isolamento de dados por tenant/cliente. Implementar
multi-tenant real exigiria, no mínimo: filtro de tenant em toda consulta ao banco, autenticação
por tenant, e revisão de segurança de que uma sessão TCP de uma central não vaze para o tenant
errado.

## 12. Migração para EF Core Migrations formais

Ver o passo a passo prático em [`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md), seção 10. Depois
de gerar a primeira migration, `EnsureCreated()` deve ser substituído por `Database.Migrate()` na
inicialização do Backend.

## 13. Remoção do código legado

`ConnectionService.cs` e o endpoint `POST /api/central/testar-conexao` **já foram removidos**
(concluído — ver [`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md)), substituídos
pelo painel de monitoramento de sessão (`CentralSessionService` + `SessionManager`).

`OperationService.cs` **também já foi removido** — `OperationController.cs` chama `PgmService`
diretamente hoje (mesmo serviço real da Tela Central), e `OperationPage.tsx` deixou de ser legada
(virou o painel dinâmico por Prédio/Central). Ver [`Protocol/20_CHANGELOG.md`](Protocol/20_CHANGELOG.md)
("Fase 4").

Candidatos restantes, agora **sem nenhum consumidor real**: `SDK/CentralHub.SDK/Adapters/*` (já
marcado `[Obsolete]`) e `KeepAliveService.cs` (ver [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md),
seção 6) — podem ser removidos com segurança numa próxima faxina, sem depender de mais nenhuma
reescrita.

## 14. Boas práticas para quem for implementar o roadmap

- Sempre rodar a suite de testes do SDK antes e depois de qualquer mudança que toque protocolo.
- Sempre seguir o padrão de "sessão ativa via SessionManager", nunca abrir uma nova conexão TCP
  paralela para um novo comando.
- Sempre atualizar a documentação correspondente ao terminar uma funcionalidade nova — a
  documentação deste projeto foi tratada como parte entregável, não como afterthought.

## 15. FAQ

**P: Existe uma ordem obrigatória para implementar os itens deste roadmap?**
R: Não obrigatória, mas a seção 4 reflete uma ordem recomendada com base em valor/risco.

**P: Alguma dessas funcionalidades futuras quebra o que já foi homologado?**
R: Nenhuma delas deveria, se a regra de "nunca alterar Handshake/Parser/Checksum/KeepAlive sem
rodar a suite completa de testes" for respeitada.

## 16. Checklist

- [ ] Sei o que já está homologado versus o que é apenas stub versus o que não existe.
- [ ] Sei a ordem de prioridade sugerida e por quê.
- [ ] Sei onde encontrar o exemplo de código para o próximo comando mais provável (Arme).

---

**Documento anterior:** [`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md)
**Próximo documento:** [`15_GLOSSARY.md`](15_GLOSSARY.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
