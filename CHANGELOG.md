# Changelog

Histórico de mudanças relevantes do CentralHub, em ordem cronológica inversa (mais recente primeiro).
Para o changelog técnico detalhado, fase a fase, com evidências e resultados de teste de cada
mudança, ver [`Documentation/Protocol/20_CHANGELOG.md`](Documentation/Protocol/20_CHANGELOG.md).

## [Não lançado] — Sprint — Operação Dinâmica por Prédio

### Adicionado
- Cadastro de PGMs por Central (`PgmPredio`): número (1-16), nome, tipo e ícone amigáveis, com
  CRUD completo (`PgmPredioController`/`PgmPredioService`).
- Cadastro de Zonas por Central (`ZonaPredio`): número (1-99), nome e tipo amigáveis, com CRUD
  completo (`ZonaPredioController`/`ZonaPredioService`).
- Painel de gerenciamento inline do cadastro (`CadastroPgmZonaPanel`), embutido na tela Operação.
- Tela **Operação dinâmica por Prédio**: o operador escolhe Prédio → Central (auto-seleciona se
  houver só uma) e o painel inteiro (status da conexão, Arme/Desarme, PGMs cadastradas, Zonas
  cadastradas, log em tempo real) carrega automaticamente — **sem nenhuma digitação manual de
  número de PGM ou Zona**.
- 16 testes de integração novos para os CRUDs (`PgmPredioServiceTests`, `ZonaPredioServiceTests`).

### Alterado
- **Tela Operação passa a operar a central real.** O antigo fluxo simulado (`OperationService` →
  `AdapterFactory` → `FakeAdapter`, que sempre retornava sucesso simulado sem falar com uma
  central de verdade) foi eliminado; `OperationController` agora chama `PgmService` diretamente —
  o mesmo serviço real, contra a mesma sessão TCP homologada, já usado pela Tela Central.
- `PgmPanel`/`ZonasPanel` (componentes React) ganharam uma prop opcional `catalogo`: quando
  presente, mostram só os itens cadastrados/ativos com o nome do cadastro; sem ela, o
  comportamento é idêntico ao de sempre (usado pela Tela Central, sem nenhuma mudança lá).

### Removido
- `Backend/CentralHub.Api/Services/OperationService.cs` — sem consumidores restantes.

### Compatibilidade e qualidade
- **Zero duplicação de lógica**: todo comando (PGM, Arme, Zona) reaproveita exatamente os mesmos
  serviços já homologados (`PgmService`, `ArmService`, `ZoneInhibitService`,
  `CentralStatusService`) — nenhuma lógica nova de protocolo foi criada.
- **Zero mudança de arquitetura**: `SessionManager`, `JflSession`, `Handshake`, `KeepAlive`,
  `Parser`, `Builder` e todo o núcleo homologado do SDK permanecem intocados.
- **Validado contra a central física real** (Active 100 Bus, NS `2751484124`), não só simulação:
  cadastro de PGM/Zona, reconexão automática da central e comando de PGM confirmado de verdade
  pelo equipamento.
- Build: 0 erros, 0 avisos (SDK + Backend + Frontend). Testes: SDK 141/141, Backend 40/40 — zero
  regressão.

---

Entradas anteriores a esta Sprint (arquitetura de sessão real, Arme/Desarme, Inibir Zonas, etc.)
estão documentadas em [`Documentation/Protocol/20_CHANGELOG.md`](Documentation/Protocol/20_CHANGELOG.md).
