# 20 — Changelog

Uma entrada por fase/item entregue, no formato completo exigido pelo plano de homologação.

---

## Fase 0.1 — Packet Analyzer

**Data:** 2026-07-14
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `SDK/CentralHub.SDK/Jfl/Diagnostics/PacketAnalyzer.cs`
- `SDK/CentralHub.SDK.Tests/Diagnostics/PacketAnalyzerTests.cs`

**Arquivos alterados:** nenhum.

**Motivação:** ferramenta permanente de inspeção — decompor qualquer pacote 0x7B em campos
nomeados, sem depender de leitura manual do manual toda vez.

**Descrição técnica:** classe estática `PacketAnalyzer` reaproveita `PacketParser`/
`ChecksumCalculator` (framing) e delega a parsers de mensagem já existentes
(`ConnectionRequest`, `CentralStatusResponse`) quando o CMD é reconhecido; cai para exibição de
bytes brutos quando não há decodificação específica ainda.

**Fluxo:** hex/bytes → `PacketAnalyzer.Analisar`/`AnalisarHex` → `PacoteAnalisado` (campos +
avisos).

**Exemplo real:** captura de KeepAlive do manual (§3.5).
**Exemplo hexadecimal:** `7B 05 18 40 26` (pedido) → `CmdNome="KeepAlive"`, `ChecksumValido=true`.
**Exemplo do manual:** §3.3 (KeepAlive), §3.1 (Conexão), §4.10 (resposta tela monitorar).

**Impacto:** nenhum em código existente — só arquivos novos.
**Compatibilidade:** total; nenhum parser homologado foi alterado.
**Testes realizados:** 9 testes novos, usando capturas reais do manual como fixtures.
**Resultado:** 100% verde. Corrigiu, durante o desenvolvimento, uma interpretação errada do
próprio analisador (KEEP=0x00 deve significar "1 minuto", não "0 minutos") — pego pelo teste
antes de virar bug.
**Hardware utilizado:** nenhum (dados do manual).
**Firmware:** N/A.

---

## Fase 0.2 — Packet Inspector

**Data:** 2026-07-14
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `Backend/CentralHub.Api/Controllers/Dev/PacketInspectorController.cs`
- `Backend/CentralHub.Api/DTOs/PacketAnalysisDtos.cs`
- `Frontend/src/pages/dev/PacketInspectorPage.tsx`

**Arquivos alterados:**
- `Frontend/src/App.tsx` (aditivo: nova rota `/ferramentas/inspetor-pacotes` + item de menu).

**Motivação:** interface visual sobre o Packet Analyzer (0.1), para colar hex e ver a
decomposição sem precisar escrever código.

**Descrição técnica:** endpoint `POST /api/dev/packet-inspector/analisar` chama
`PacketAnalyzer.AnalisarHex` e devolve DTOs; página React consome via Axios e renderiza tabela.

**Impacto:** nenhum em telas/rotas existentes (aditivo).
**Compatibilidade:** total.
**Testes realizados:** `dotnet build` (Backend, output isolado) e `npm run build` (Frontend), 0
erros. Validação end-to-end (subir Backend+Frontend e testar no navegador) **não foi possível
nesta sessão** — uma central real estava conectada à instância do Backend já em execução, e não
travamos essa conexão para evitar interromper uma sessão de hardware real ativa. Pendente
validação manual na próxima oportunidade.
**Resultado:** compila limpo dos dois lados.
**Hardware utilizado:** N/A (não testado ao vivo nesta sessão).
**Firmware:** N/A.

---

## Fase 0.3 — Packet Capture

**Data:** 2026-07-14
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `Documentation/RealCaptures/{Handshake,KeepAlive,Evento,PGM_ON,PGM_OFF,Arme,Desarme,Zona,DataHora}.bin`
- `Documentation/RealCaptures/{Status,Stay,Away,Usuario,PGM_PULSE}.bin` (placeholders vazios)
- `Documentation/RealCaptures/README.md`

**Motivação:** catalogar capturas reais como fixtures permanentes para testes/replay/inspeção.

**Descrição técnica:** bytes extraídos e conferidos token a token contra o texto do manual
oficial; cada arquivo teve o checksum XOR validado automaticamente (fecha em zero) antes de ser
aceito como seed.

**Exemplo real:** todos os 9 arquivos preenchidos vêm de exemplos reais do manual (Active 20
Ethernet via módulo ME-05) — não são inventados.

**Impacto:** nenhum em código.
**Compatibilidade:** N/A.
**Testes realizados:** validação de checksum automática (script `seed_captures.py`, não faz parte
do build).
**Resultado:** 9/9 capturas com checksum válido.
**Hardware utilizado:** nenhum diretamente (dados do manual, de outro modelo — Active 20
Ethernet). Nenhuma captura da Active 100 Bus deste projeto ainda — pendente Fase 7.
**Firmware:** N/A.

---

## Fase 0.4 — Replay Engine

**Data:** 2026-07-14
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `SDK/CentralHub.SDK/Jfl/Diagnostics/ReplayEngine.cs`
- `SDK/CentralHub.SDK.Tests/Diagnostics/ReplayEngineTests.cs`
- `SDK/CentralHub.SDK.Tools/ReplayCli/` (`ReplayCli.csproj`, `Program.cs`)

**Arquivos alterados:**
- `SDK/CentralHub.SDK/CentralHub.SDK.csproj` (adicionadas as implementações concretas
  `Microsoft.Extensions.DependencyInjection`/`Microsoft.Extensions.Logging`, além das já
  existentes `.Abstractions`, necessárias para `ReplayContraServidorEfemeroAsync` montar seu
  próprio container de DI).
- `CentralHub.sln` (novo projeto `ReplayCli` adicionado).

**Motivação:** reproduzir bugs de forma determinística contra a infraestrutura real, sem
hardware.

**Descrição técnica:** `ReplayEngine.ReplayAsync` abre um `TcpClient` comum e envia os bytes
exatamente como capturados; `ReplayContraServidorEfemeroAsync` sobe um `JflTcpServer` real (porta
0, via `AddJflServer`) para replays sem precisar do Backend rodando.

**Testes realizados:** 4 testes novos — replay de Handshake/KeepAlive reais (sucesso), replay de
comando órfão de Tipo A (timeout correto), replay contra endereço sem listener (falha reportada
sem exceção).
**Resultado:** 100% verde.
**Hardware utilizado:** nenhum.
**Firmware:** N/A.

---

## Fase 0.5 — Central Simulator

**Data:** 2026-07-14
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `Simulator/CentralHub.Simulator/` (`CentralHub.Simulator.csproj`, `EstadoCentralSimulada.cs`,
  `SimuladorActive100Bus.cs`, `Program.cs`)
- `Simulator/CentralHub.Simulator.Tests/` (`CentralHub.Simulator.Tests.csproj`,
  `SimuladorActive100BusTests.cs`)

**Arquivos alterados:**
- `CentralHub.sln` (2 novos projetos adicionados).

**Motivação:** testar o Backend sem hardware físico, com um cliente que fala o protocolo de
verdade (não um mock).

**Descrição técnica:** `SimuladorActive100Bus` reaproveita `PacketBuilder`/`JflFrameReader`/
`ChecksumCalculator`/`JflCommand`/`JflModel` do SDK; nunca `SessionManager`/`JflSession`.
`EstadoCentralSimulada` monta o payload de resposta "tela monitorar" (espelho de escrita do que
`CentralStatusResponse.Parse` lê, sem duplicar/alterar o parser).

**Impacto:** nenhum em código existente.
**Compatibilidade:** total.
**Testes realizados:** 5 testes de integração **contra um `JflTcpServer` real** (porta efêmera) —
handshake real registra sessão real no `SessionManager`; `PgmCommandService`/
`CentralStatusQueryService` reais operam com sucesso contra o simulador; desconexão simulada
remove a sessão corretamente.
**Resultado:** 100% verde — validação forte de que o simulador fala o protocolo corretamente
contra a infraestrutura homologada, sem modificá-la.
**Hardware utilizado:** nenhum (é o próprio objetivo desta ferramenta).
**Firmware:** simulado como "6.5.0" (Active 100 Bus, `MOD=0xA4`), refletindo o firmware 6.5 real
documentado do hardware do projeto.

---

## Fase 0.6 — Stress Test

**Data:** 2026-07-14
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `Simulator/CentralHub.StressTest/` (`CentralHub.StressTest.csproj`, `Program.cs`,
  `OpcoesStress.cs`, `RelatorioStress.cs`)

**Arquivos alterados:**
- `CentralHub.sln` (novo projeto adicionado).

**Motivação:** medir comportamento sob carga e cenários de falha, sem hardware.

**Descrição técnica:** sobe `JflTcpServer` efêmero, conecta N `SimuladorActive100Bus`
concorrentes, usa `PgmCommandService`/`CentralStatusQueryService` reais para gerar carga, injeta
cenários de falha (reconexão, timeout, checksum inválido, pacote quebrado), gera relatório
Markdown com latência (média/P95/P99) por categoria.

**Testes realizados:** execução manual em escala reduzida (15 conexões) durante o
desenvolvimento — conectou, gerou carga em todas as categorias, executou os 4 cenários de falha
sem exceção não tratada, relatório gerado corretamente. Artefato de validação removido do
repositório após conferência (não é um resultado real de stress test nos volumes documentados).
**Resultado:** ferramenta funcional, ponta a ponta, contra infraestrutura real.
**Hardware utilizado:** nenhum.
**Firmware:** N/A.

---

## Fase 0.7 — Benchmark

**Data:** 2026-07-14
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `SDK/CentralHub.SDK.Benchmarks/` (`CentralHub.SDK.Benchmarks.csproj`,
  `ProtocoloBenchmarks.cs`, `SessaoBenchmarks.cs`, `Program.cs`)

**Arquivos alterados:**
- `CentralHub.sln` (novo projeto adicionado).

**Motivação:** referência de performance para detectar regressões nas próximas fases.

**Descrição técnica:** projeto BenchmarkDotNet; `ProtocoloBenchmarks` mede operações isoladas
(checksum, builder, parser, `CentralStatusResponse.Parse`); `SessaoBenchmarks` mede o pipeline
completo via socket real (servidor efêmero + Central Simulator).

**Testes realizados:** smoke-test com `--job Dry` (1 iteração) — confirma que builda e roda contra
o código real, sem medir performance com rigor estatístico ainda.
**Resultado:** ferramenta funcional; execução completa com números reais fica para quando houver
um baseline útil de comparar (ex.: Fase 7).
**Hardware utilizado:** nenhum.
**Firmware:** N/A.

---

## Fase 0.8 — Logs HEX

**Data:** 2026-07-14
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `SDK/CentralHub.SDK/Jfl/Diagnostics/HexLoggingStream.cs`
- `SDK/CentralHub.SDK.Tests/Diagnostics/HexLoggingStreamTests.cs`
- `SDK/CentralHub.SDK.Tests/Diagnostics/JflTcpServerHexLoggingIntegrationTests.cs`

**Arquivos alterados:**
- `SDK/CentralHub.SDK/Jfl/Server/JflServerOptions.cs` (aditivo: propriedade `LogHexAtivado`,
  padrão `false`).
- `SDK/CentralHub.SDK/Jfl/Server/JflTcpServer.cs` (**único arquivo de infraestrutura de conexão
  tocado nesta fase** — método `CriarSessao` substitui a chamada direta a
  `JflSession.FromTcpClient`, replicando a mesma extração de stream/endpoint mas envolvendo
  opcionalmente o stream num `HexLoggingStream` transparente. `JflSession.cs` **não foi alterado**
  — a mudança usa o construtor público `JflSession(Stream, string, IDisposable?, string?)` que já
  existia e já era usado pelos testes).
- `Backend/CentralHub.Api/appsettings.json` (aditivo: `Jfl:LogHexAtivado: false`).
- `Backend/CentralHub.Api/Program.cs` (aditivo: lê `Jfl:LogHexAtivado` da configuração).

**Motivação:** log opcional de RX/TX em hex+ASCII para depuração, sem instrumentar cada comando
individualmente.

**Descrição técnica:** `HexLoggingStream` é um decorator de `Stream` puro (pass-through byte a
byte idêntico, com ou sem log) — zero lógica de protocolo. Desligado por padrão.

**Impacto:** comportamento do servidor **idêntico** com a opção desligada (padrão) — comprovado
por toda a suíte de testes existente continuando 100% verde após a mudança. Com a opção ligada,
comportamento também idêntico (só adiciona log em nível Debug) — comprovado por um teste de
integração dedicado.
**Compatibilidade:** total.
**Testes realizados:** 3 testes novos (pass-through byte-exato do decorator, comportamento
idêntico com/sem log habilitado, integração completa handshake+keep-alive com
`LogHexAtivado=true`) + suíte completa do SDK (116 testes) e do Simulator (5 testes) re-executadas
para confirmar zero regressão.
**Resultado:** 100% verde em ambas as suítes, antes e depois da mudança.
**Hardware utilizado:** nenhum.
**Firmware:** N/A.

---

## Fase 0.9 — Painel de Monitoramento de Sessão (SessionManager)

**Data:** 2026-07-17
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `Backend/CentralHub.Api/Logging/AtividadeLogEntry.cs`
- `Backend/CentralHub.Api/Logging/SessionActivityLogService.cs`
- `Backend/CentralHub.Api/Logging/SdkActivityLogger.cs`
- `Backend/CentralHub.Api/Logging/SdkActivityLoggerProvider.cs`
- `Backend/CentralHub.Api/Services/CentralSessionService.cs`
- `Backend/CentralHub.Api/DTOs/SessaoDtos.cs`
- `Backend/CentralHub.Api.Tests/` (`CentralHub.Api.Tests.csproj`, `SessionActivityLogServiceTests.cs`,
  `CentralSessionServiceTests.cs`) — primeiro projeto de testes do Backend
- `Frontend/src/components/session/` (`StatusConexaoCard.tsx`, `SessaoTcpPanel.tsx`,
  `IndicadoresChips.tsx`, `LogCentralPanel.tsx`, `DetalhesSessaoModal.tsx`, `DiagnosticoPanel.tsx`)
- `Frontend/src/utils/formatters.ts`
- `Documentation/ARQUITETURA_SESSION_MANAGER.md`

**Arquivos removidos:**
- `Backend/CentralHub.Api/Services/ConnectionService.cs` (sem outros consumidores).
- Action `TestarConexao` (rota `POST api/central/testar-conexao`) em `CentralController.cs`.
- `TestarConexaoDto`/`ConexaoResultDto` em `DTOs/CentralDtos.cs`.
- Registro de `ConnectionService` em `Program.cs`.
- Botão "Testar Conexão" e os campos IP/Porta/Usuário/Senha do formulário em
  `Frontend/src/pages/CentralsPage.tsx`; tipo `ConexaoResult` em `Frontend/src/types/index.ts`.

**Arquivos alterados:**
- `CentralController.cs` — 4 actions novas (`GET ~/api/centrais/{id}/sessao`,
  `GET .../log`, `POST .../reconectar`, `GET .../diagnostico`), injetando `CentralSessionService`.
- `CentralService.cs` — removida a dependência de `ConnectionService`; `CriarAsync`/`AtualizarAsync`
  agora usam valores-padrão (`string.Empty`/`0`) para IP/Porta/Usuário/Senha quando omitidos.
- `DTOs/CentralDtos.cs` — `IP`/`Porta`/`Usuario`/`Senha` viram campos opcionais (`string?`/`int?`,
  sem `[Required]`) em `CreateCentralDto`/`UpdateCentralDto`/`CentralDto`.
- `Program.cs` — registra `CentralSessionService` (Scoped), `SessionActivityLogService` (Singleton +
  `IHostedService`) e `SdkActivityLoggerProvider` (`ILoggerProvider`, resolução preguiçosa via
  `IServiceProvider` para não criar dependência circular com `ILoggerFactory`/`SessionManager`).
- `SDK/CentralHub.SDK/Adapters/TcpConnectionHelper.cs`, `JflAdapter.cs`, `IntelbrasAdapter.cs`,
  `AdapterFactory.cs` — anotados com `[Obsolete]` + comentário XML (só nos métodos de conexão de
  saída; `AcionarPGM`/`DesligarPGM`/`PulsoPGM`/`Criar`/`ResolverPorNome` continuam sem anotação,
  pois ainda alimentam `OperationService`). **Nenhuma lógica alterada.**
- `Frontend/src/pages/CentralsPage.tsx` — reescrita: formulário com Nome/Número de Série/Prédio,
  tabela com coluna "Número de Série" no lugar de "IP".
- `Frontend/src/pages/CentralDetailPage.tsx` — reescrita: orquestra os componentes novos de sessão
  (`StatusConexaoCard`, `SessaoTcpPanel`, `DiagnosticoPanel`, `LogCentralPanel`,
  `DetalhesSessaoModal`) além do bloco de Status/PGM já existente (inalterado).
- `Frontend/src/types/index.ts` — `SessaoInfo`, `AtividadeLogEntry`, `DiagnosticoResultado`,
  `ReconectarResultado` no lugar de `ConexaoResult`.
- `Documentation/01_PROJECT_OVERVIEW.md`, `05_SOURCE_CODE_GUIDE.md`, `06_DATABASE_GUIDE.md`,
  `09_WEB_GUIDE.md`, `12_FAQ.md` (Q43/Q44), `14_ROADMAP.md` (item 13), `INDEX.md` — atualizados para
  refletir a remoção do teste de conexão e a arquitetura nova.

**Motivação:** a tela "Centrais" e o endpoint `testar-conexao` ainda refletiam a arquitetura
antiga (o Backend discando para IP/Porta cadastrados), incompatível com o modelo real já
homologado (`TcpListener` + `SessionManager`, a central disca para o servidor). Isso causava
confusão real: uma central com sessão ativa de verdade podia "falhar" no teste de conexão, porque
o teste tentava abrir uma conexão de saída que nunca é aceita pela central (ela não escuta nessa
porta como servidor).

**Descrição técnica:** `JflSession`/`SessionManager` (SDK, não alterados) já expõem publicamente
tudo que a maior parte dos campos pedidos precisa (`RemoteEndPoint`, `State`, `NumeroSerie`,
`Modelo`, `VersaoFirmware`, `ConectadoEmUtc`, `UltimaAtividadeUtc`...). O que faltava (SEQ atual,
bytes enviados/recebidos, último comando, latência por comando) **já era logado hoje**,
estruturadamente, pelas classes existentes do SDK (`JflTcpServer`, `PgmCommandService`,
`CentralStatusQueryService`) em nível Debug/Information — e `appsettings.json` já tinha
`"CentralHub.SDK.Jfl": "Debug"` configurado. A solução foi só **capturar** esses logs num buffer em
memória (`SessionActivityLogService`, `ConcurrentQueue` limitado a 500 entradas), via um
`ILoggerProvider` customizado (`SdkActivityLoggerProvider`/`SdkActivityLogger`) que extrai as
propriedades nomeadas do structured logging já existente (`{Cmd}`, `{Seq}`, `{BytesRecebidos}`...)
— **zero alteração em qualquer arquivo do SDK**, 100% aditivo.

**Fluxo:** `GET /api/centrais/{id}/sessao` consulta `SessionManager.TryGet` (ao vivo) + histórico do
banco + a última entrada relevante de `SessionActivityLogService` para montar um snapshot completo;
`POST /api/centrais/{id}/reconectar` chama `session.Close()` + `sessionManager.Remover(session)` —
**nunca abre um socket**, só limpa a sessão registrada, deixando a central reconectar sozinha.

**Impacto:** nenhum em `Handshake`/`KeepAlive`/`Parser`/`Builder`/`PGM`/`Status`/`SessionManager`/
`JflSession`/`Protocol`/`Handlers` (núcleo homologado — nenhum desses arquivos foi tocado).
`OperationService`/`OperationPage` (tela "Operação" legada) continuam funcionando sem mudança,
porque `AdapterFactory`/`JflAdapter`/`IntelbrasAdapter` só ganharam anotações `[Obsolete]`, não
mudança de comportamento.
**Compatibilidade:** total com o núcleo do protocolo. `POST /api/central/testar-conexao` deixou de
existir (era usado só pela tela removida); nenhum outro endpoint mudou de contrato.
**Testes realizados:** 11 testes novos em `Backend/CentralHub.Api.Tests` (4 de
`SessionActivityLogService`, 7 de `CentralSessionService` — estes últimos contra um `JflTcpServer`
real em porta efêmera e o Central Simulator, sem mocks: conectar simulador → `StatusConexao`
"Online"; desconectar → volta a "Offline"; `ReconectarAsync` derruba a sessão real e some do
`SessionManager`; comando PGM real gera entrada de log com SEQ) + suíte completa do SDK (116
testes) re-executada para confirmar zero regressão + `npm run build` (Frontend, 0 erros
TypeScript).
**Resultado:** 11/11 novos testes verdes, 116/116 testes do SDK continuam verdes, build completo
(Backend + Frontend) sem erros.
**Hardware utilizado:** nenhum (Central Simulator).
**Firmware:** simulado como "6.5.0" (mesmo simulador da Fase 0.5).

---

## Fase 2 e 3 — Arme (Armar/Desarmar/Stay/Away) e Zonas (Inibir/Desinibir)

**Data:** 2026-07-18
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `SDK/CentralHub.SDK/Jfl/Server/ArmCommandService.cs`
- `SDK/CentralHub.SDK.Tests/Server/ArmCommandServiceTests.cs`
- `SDK/CentralHub.SDK/Jfl/Server/ZoneInhibitCommandService.cs`
- `SDK/CentralHub.SDK.Tests/Server/ZoneInhibitCommandServiceTests.cs`
- `Backend/CentralHub.Api/Services/ArmService.cs`, `DTOs/ArmDtos.cs`
- `Backend/CentralHub.Api/Services/ZoneInhibitService.cs`, `DTOs/ZoneInhibitDtos.cs`
- `Backend/CentralHub.Api.Tests/ArmServiceTests.cs`, `ZoneInhibitServiceTests.cs`
- `Frontend/src/components/ArmPanel.tsx`, `ZonasPanel.tsx`

**Arquivos alterados:**
- `SDK/CentralHub.SDK/Jfl/JflServiceCollectionExtensions.cs` — registra `ArmCommandService`/
  `ZoneInhibitCommandService` (aditivo; `ArmCommandHandlerStub`/`ZoneCommandHandlerStub` continuam
  registrados como rede de segurança para respostas órfãs/atrasadas — mesmo papel que
  `PgmCommandHandlerStub` já cumpre ao lado do `PgmCommandService` real).
- `Backend/CentralHub.Api/Controllers/CentralController.cs` — 7 actions novas (`POST
  ~/api/centrais/{id}/particoes/{p}/armar|desarmar|armar-stay|armar-away`, `POST
  ~/api/centrais/{id}/zonas/{z}/inibir|desinibir`, `GET ~/api/centrais/{id}/zonas/inibidas`).
- `Backend/CentralHub.Api/Program.cs` — registra `ArmService`/`ZoneInhibitService` (Scoped).
- `Frontend/src/types/index.ts` — `ArmCommandResult`, `ZoneInhibitResult`.
- `Frontend/src/pages/CentralDetailPage.tsx` — adiciona `ArmPanel` (entre o bloco de Status e o
  `PgmPanel`); extrai a renderização de zonas para `ZonasPanel` (chips agora clicáveis quando
  `permiteInibir=true`).
- `Documentation/Protocol/10_ARM.md`, `11_ZONES.md`, `14_PACKET_REFERENCE.md`,
  `Documentation/02_JFL_PROTOCOL_GUIDE.md`, `08_COMMANDS_GUIDE.md`, `01_PROJECT_OVERVIEW.md`,
  `05_SOURCE_CODE_GUIDE.md`, `09_WEB_GUIDE.md`, `14_ROADMAP.md`,
  `Documentation/Protocol/00_INDEX.md` — atualizados de "planejado"/stub para implementado.

**Motivação:** depois do painel de monitoramento de sessão (Fase 0.9), o próximo pedido foi
completar a operação remota real da central — hoje só PGM funcionava; Arme/Desarme e Zonas
continuavam como stubs reconhecidos mas sem lógica, apesar de já terem especificação completa e
confirmada contra o manual oficial (`10_ARM.md`/`11_ZONES.md`, escritos numa fase anterior de
homologação da ferramentaria).

**Descrição técnica:**
- **Arme** (CMD `0x4E`/`0x4F`/`0x53`/`0x54`, Tipo A): `ArmCommandService` espelha exatamente
  `PgmCommandService` — payload de 1 byte (partição), confirmação via
  `CentralStatusResponse.Particoes`/`Eletrificador` (mesmo parser 4.10 já homologado pelo PGM/
  Status). Caso especial: partição `99` opera o eletrificador (confirmado por captura real do
  manual, `7B 06 03 4E 63 53`) — `ArmarStayAsync` rejeita 99, pois o eletrificador não tem modo
  Stay documentado.
- **Zonas** (CMD `0x52`, Tipo A): `ZoneInhibitCommandService.InibirZonasAsync` recebe o conjunto
  **completo** de zonas que devem ficar inibidas (o comando substitui o estado inteiro, não soma —
  achado crítico documentado em `11_ZONES.md`) e empacota um bitmap de 13 bytes **MSB-first**, que
  é o **oposto** da convenção do campo `P-INIB` da resposta de status (LSB-first) — reaproveitar a
  lógica errada inibiria a zona errada silenciosamente. `Backend/ZoneInhibitService` é quem calcula
  esse conjunto completo (consulta o estado atual via `CentralStatusQueryService`, soma ou
  subtrai a zona alvo, reenvia o bitmap inteiro) — o SDK só expõe a primitiva "enviar o conjunto
  completo", exatamente como planejado em `11_ZONES.md`.

**Fluxo:** operador clica Armar/Inibir na Tela Central → diálogo de confirmação → `POST` no
Backend → `ArmService`/`ZoneInhibitService` resolve o `NumeroSerie` no banco → SDK usa a sessão TCP
já aberta (nunca disca para fora) → resposta confirmada byte a byte contra o parser 4.10 →
Frontend recarrega o status.

**Impacto:** nenhum em `Handshake`/`KeepAlive`/`Parser`/`Builder`/`SessionManager`/`JflSession`/
`Protocol`/`Handlers` (núcleo homologado — nenhum desses arquivos foi tocado). `PgmCommandService`/
`CentralStatusQueryService` também não foram alterados, só reaproveitados por leitura.
**Compatibilidade:** total — 7 endpoints novos, nenhum contrato existente mudou.
**Testes realizados:** 23 testes novos no SDK (`ArmCommandServiceTests` 14 +
`ZoneInhibitCommandServiceTests` 9, incluindo os 3 exemplos reais do manual para o bitmap de zonas:
zona 1 → `0x80`, zonas 1-4 → `0xF0`, zonas 1-9 → `0xFF 0x80`) + 13 testes novos no Backend
(`ArmServiceTests` 7 + `ZoneInhibitServiceTests` 6), todos contra um `JflTcpServer` real em porta
efêmera e o Central Simulator (sem mocks) + suíte completa do SDK (141 testes) e do
Backend.Api.Tests (24 testes) re-executadas para confirmar zero regressão + `npm run build`
(Frontend, 0 erros TypeScript).
**Resultado:** 141/141 testes do SDK, 24/24 testes do Backend, build completo (SDK + Backend +
Frontend) sem erros.
**Hardware utilizado:** nenhum (Central Simulator, que já implementava corretamente os comandos
0x4E/0x4F/0x52/0x53/0x54 desde a Fase 0.5 — inclusive a mesma convenção MSB-first do bitmap de
zonas, confirmando de forma independente que a implementação do serviço bate com o simulador).
**Firmware:** simulado como "6.5.0" (mesmo simulador das fases anteriores).

---

## Fase 4 — Tela Operação real (elimina simulação)

**Data:** 2026-07-21
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos alterados:** `Backend/CentralHub.Api/Controllers/OperationController.cs` (injeta
`PgmService` + `AppDbContext` no lugar de `OperationService`; mapeia `Comando` direto para
`LigarAsync`/`DesligarAsync`/`PulsoAsync`).
**Arquivos removidos:** `Backend/CentralHub.Api/Services/OperationService.cs` (sem consumidores
restantes); registro correspondente em `Program.cs`.

**Motivação:** a tela "Operação" chamava `OperationService` → `AdapterFactory`/`FakeAdapter`, que
**sempre retornava sucesso simulado**, nunca falando de verdade com uma central — confundido em
campo com o fluxo real (Tela Central), que usa `PgmService`/`SessionManager`/sessão TCP real.

**Resultado:** `POST /api/operation/enviar` (mesmo contrato, zero mudança no Frontend) agora
executa o mesmo caminho real da Tela Central. Testado ao vivo contra a central física conectada
(`10.0.250.21`): comando `Ligar` PGM 1 confirmado em 2261ms de round-trip real, gravado no
histórico. `AdapterFactory`/`JflAdapter`/`FakeAdapter` (SDK) ficaram sem nenhum consumidor real
como consequência (não removidos nesta fase — fora do escopo pedido).
**Testes:** SDK 141/141, Backend 24/24 (zero regressão) + validação manual contra hardware real.

---

## Fase 5 — Operação Dinâmica por Prédio (cadastro de PGMs/Zonas)

**Data:** 2026-07-21
**Autor:** Sessão de pair-programming com Claude (agente autônomo)

**Arquivos criados:**
- `Backend/CentralHub.Api/Models/PgmPredio.cs`, `ZonaPredio.cs`
- `Backend/CentralHub.Api/DTOs/PgmPredioDtos.cs`, `ZonaPredioDtos.cs`
- `Backend/CentralHub.Api/Services/PgmPredioService.cs`, `ZonaPredioService.cs`
- `Backend/CentralHub.Api/Controllers/PgmPredioController.cs`, `ZonaPredioController.cs`
- `Backend/CentralHub.Api.Tests/PgmPredioServiceTests.cs`, `ZonaPredioServiceTests.cs`
- `Frontend/src/components/CadastroPgmZonaPanel.tsx`

**Arquivos alterados:**
- `Backend/CentralHub.Api/Data/AppDbContext.cs` — `DbSet<PgmPredio>`/`DbSet<ZonaPredio>`, índice
  único `CentralId+Numero` em cada um (evita cadastro duplicado do mesmo número na mesma central).
- `Backend/CentralHub.Api/Program.cs` — registra `PgmPredioService`/`ZonaPredioService`.
- `Frontend/src/components/PgmPanel.tsx`/`ZonasPanel.tsx` — prop opcional `catalogo`: quando
  presente, filtra só os itens cadastrados/ativos e usa o nome do cadastro em vez de "PGM N"/
  "Z-N" genérico; sem a prop, comportamento idêntico ao de sempre (usado pela Tela Central, sem
  nenhuma mudança de comportamento lá).
- `Frontend/src/pages/OperationPage.tsx` — reescrita: seleção dinâmica Prédio → Central (auto-
  seleciona se só houver uma Central), carrega automaticamente Central/Status/PGMs cadastradas/
  Zonas cadastradas, monta o painel reaproveitando `StatusConexaoCard`, `ArmPanel`, `PgmPanel`
  (com catálogo), `ZonasPanel` (com catálogo), `LogCentralPanel` e `CadastroPgmZonaPanel` — **sem
  nenhum campo de digitação manual de número de PGM/Zona**.

**Motivação:** eliminar a digitação manual de números de PGM/Zona na tela Operação, substituindo
por um cadastro reaproveitável por Prédio/Central (nome, tipo, ícone), sem duplicar nenhuma
lógica de comando — todo envio continua passando pelos mesmos serviços reais já homologados
(`PgmService`, `ArmService`, `ZoneInhibitService`, `CentralStatusService`), só a apresentação
(nomes/filtragem) mudou.

**Impacto:** nenhum em `Handshake`/`KeepAlive`/`Parser`/`Builder`/`SessionManager`/`JflSession`/
`Protocol`/`Handlers`/`PgmCommandService`/`ArmCommandService`/`ZoneInhibitCommandService` — só
cadastro novo (Backend) + composição de componentes já existentes (Frontend).
**Compatibilidade:** total. `PgmPanel`/`ZonasPanel` continuam funcionando sem a prop `catalogo`
exatamente como antes (usado pela Tela Central).
**Testes:** 16 testes novos (`PgmPredioServiceTests` 8 + `ZonaPredioServiceTests` 8, EF Core
InMemory, cobrindo CRUD + validação de central pertencente ao prédio + número duplicado) — total
Backend.Api.Tests: 40/40. SDK: 141/141 (inalterado). `npm run build`: 0 erros TypeScript.
**Observação de schema:** como o projeto usa `EnsureCreated()` (sem EF Migrations formais — ver
[`06_DATABASE_GUIDE.md`](../06_DATABASE_GUIDE.md)), as tabelas `PgmPredios`/`ZonaPredios` só
aparecem automaticamente em um banco SQLite **novo**; um `centralhub.db` já existente precisa ser
recriado (ou as tabelas criadas manualmente) para os endpoints novos funcionarem.

---

**Índice:** [`00_INDEX.md`](00_INDEX.md)
