# 05 — SOURCE CODE GUIDE

> **Público-alvo:** um desenvolvedor que vai efetivamente mexer no código. Este é o mapa completo
> de todo o código-fonte do projeto: cada pasta, cada arquivo, cada classe, o que ela faz, quem a
> chama, de quem ela depende, e — muito importante — o que é código **legado** que não deve ser
> usado como referência para código novo.

---

## Índice

1. [Visão geral dos três projetos](#1-visão-geral-dos-três-projetos)
2. [SDK — CentralHub.SDK (a biblioteca de protocolo)](#2-sdk--centralhubsdk-a-biblioteca-de-protocolo)
3. [Backend — CentralHub.Api (a API REST)](#3-backend--centralhubapi-a-api-rest)
4. [Frontend — a interface web](#4-frontend--a-interface-web)
5. [SDK.Tests — os testes automatizados](#5-sdktests--os-testes-automatizados)
6. [Código legado — o que existe mas NÃO é usado no fluxo real](#6-código-legado--o-que-existe-mas-não-é-usado-no-fluxo-real)
7. [Mapa de dependências entre classes](#7-mapa-de-dependências-entre-classes)
8. [Boas práticas ao navegar no código](#8-boas-práticas-ao-navegar-no-código)
9. [Problemas comuns](#9-problemas-comuns)
10. [Como testar cada camada](#10-como-testar-cada-camada)
11. [Como depurar cada camada](#11-como-depurar-cada-camada)
12. [FAQ](#12-faq)
13. [Checklist](#13-checklist)

---

## 1. Visão geral dos três projetos

```
central/
├── CentralHub.sln                    → arquivo de "solução" .NET, agrupa os projetos C#
├── Backend/CentralHub.Api/           → API REST + servidor TCP + banco de dados
├── SDK/CentralHub.SDK/               → biblioteca de protocolo JFL (sem banco, sem HTTP)
├── SDK/CentralHub.SDK.Tests/         → testes automatizados do SDK
└── Frontend/                         → interface web (React)
```

A regra de dependência entre eles é sempre numa direção só:

```
Frontend  ──chama via HTTP──►  Backend  ──referencia (ProjectReference)──►  SDK
```

O SDK **nunca** depende do Backend, nem sabe que existe um Frontend — ele é uma biblioteca pura de
protocolo, testável isoladamente. Essa separação é intencional: o SDK poderia, teoricamente, ser
reaproveitado numa ferramenta de linha de comando ou noutro tipo de aplicação sem levar consigo
Entity Framework, SQLite, ASP.NET Core, etc.

## 2. SDK — CentralHub.SDK (a biblioteca de protocolo)

Todo o código relevante do SDK vive dentro de `SDK/CentralHub.SDK/Jfl/`, organizado em três
subpastas por responsabilidade:

```
Jfl/
├── JflServiceCollectionExtensions.cs   → registra tudo isso na injeção de dependência
├── Protocol/                            → framing, checksum, parsing — nível "bytes crus"
├── Messages/                            → estruturas de dados tipadas por comando
└── Server/                              → TcpListener, sessões, dispatcher — nível "aplicação"
```

### 2.1 `Jfl/Protocol/` — a camada mais baixa (bytes)

| Arquivo | Classe(s) | Responsabilidade |
|---|---|---|
| `JflProtocol.cs` | `JflProtocol` | Constantes: byte de cabeçalho (`0x7B`), tamanho mínimo/máximo de pacote. |
| `JflCommand.cs` | `JflCommand` (enum) | Todos os valores de `CMD` conhecidos (0x21, 0x40, 0x4D, 0x50, 0x51, etc), nomeados. |
| `JflModel.cs` | `JflModel` (enum) + extensões | Mapeia o byte `MOD` do handshake para o nome do modelo (`0xA4` → "Active 100 Bus"). |
| `ChecksumCalculator.cs` | `ChecksumCalculator` | `Calculate` (gera o K) e `IsValid` (confere um pacote completo). Ver [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), seção 10. |
| `JflPacket.cs` | `JflPacket` | Um pacote já decodificado: `Seq`, `Cmd`, `Dados` (não guarda CAB/QDE — são só framing). |
| `PacketBuilder.cs` | `PacketBuilder` | Monta bytes prontos para transmitir, a partir de `Seq`+`Cmd`+`Dados`, calculando `QDE` e `K` automaticamente. |
| `PacketParser.cs` | `PacketParser`, `JflParseResult`, `JflParseStatus` | Função **pura** (sem I/O) que tenta extrair um `JflPacket` de um buffer de bytes — devolve "sucesso", "preciso de mais bytes", "cabeçalho inválido" ou "checksum inválido". |
| `JflFrameReader.cs` | `JflFrameReader` | Usa o `PacketParser` repetidamente sobre um `Stream` real, acumulando bytes até formar pacotes completos — lida com fragmentação de TCP. |
| `JflProtocolException.cs` | `JflProtocolException` | Exceção lançada quando algo no protocolo está incorreto (ex.: resposta curta demais para ser decodificada). |

**Quem chama quem, dentro desta pasta:** `JflFrameReader` usa `PacketParser`, que usa
`ChecksumCalculator`. `PacketBuilder` usa `ChecksumCalculator`. Nenhuma classe aqui conhece
`JflSession` ou qualquer coisa de rede real (`TcpClient`) — só `Stream` genérico, o que torna tudo
aqui **100% testável sem sockets de verdade** (ver os testes em
`SDK/CentralHub.SDK.Tests/Protocol/`).

### 2.2 `Jfl/Messages/` — estruturas de dados por comando

| Arquivo | Classe(s) | Responsabilidade |
|---|---|---|
| `ConnectionRequest.cs` | `ConnectionRequest` | Decodifica o payload do comando 0x21 (NS, IMEI, MAC, Modelo, Versão...). |
| `ConnectionResponse.cs` | `ConnectionResponse`, `ConnectionResult` | Monta o payload da resposta ao 0x21 (RESULT + KEEP). |
| `JflBcd.cs` | `JflBcd` | Converte um byte "BCD" (ex.: `0x46` → 46) — usado para datas/horas. |
| `JflText.cs` | `JflText` | Lê campos de texto ASCII, tratando `0xFF` repetido como "campo vazio". |
| `JflVersion.cs` | `JflVersion` | Formata os 3 bytes de versão de firmware (ex.: `"650"` → `"6.5"`). |
| `Status/CentralStatusResponse.cs` | `CentralStatusResponse` | O parser mais importante do projeto — decodifica a resposta completa do comando 0x4D: partições, zonas, PGMs, eletrificador, bateria, problemas. Reaproveitado também pelos comandos de PGM (0x50/0x51), porque a resposta é o mesmo formato. |
| `Status/PartitionState.cs` | `PartitionState` (enum), `PartitionStatus` | Estado de uma partição e suas permissões. |
| `Status/ZoneState.cs` | `ZoneState` (enum), `ZoneStatus` | Estado de uma zona (nibble) e se pode ser inibida. |
| `Status/PgmStatus.cs` | `PgmStatus` | Estado (ligada/desligada) e permissão de uma PGM. |
| `Status/ElectrifierStatus.cs` | `ElectrifierState` (enum), `ElectrifierStatus` | Estado do eletrificador (choque). |
| `Status/BatteryStatus.cs` | `BatteryType` (enum), `BatteryStatus` | Tipo de bateria (lítio/chumbo) e nível/tensão. |
| `Status/ProblemFlags.cs` | `ProblemFlags` | Os 40 bits de problemas (5 bytes), cada um virando uma propriedade booleana nomeada. |

Todo o detalhamento campo a campo destas classes está em
[`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md).

### 2.3 `Jfl/Server/` — a camada de aplicação (sessões, TCP, dispatcher)

| Arquivo | Classe(s) | Responsabilidade |
|---|---|---|
| `JflTcpServer.cs` | `JflTcpServer` | O `TcpListener`. Aceita conexões, cria `JflSession`s, dispara a leitura contínua de pacotes de cada uma. |
| `JflSession.cs` | `JflSession` | Representa **uma conexão viva** com uma central. Sabe enviar (`SendAsync`), responder (`ReplyAsync`), e — o mecanismo mais sofisticado do projeto — enviar e **esperar uma resposta correlacionada por SEQ** (`SendAndWaitAsync`). Detalhado em [`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md). |
| `JflSessionState.cs` | `JflSessionState` (enum) | `Conectando` / `Ativa` / `Encerrada`. |
| `SessionManager.cs` | `SessionManager` | O "catálogo" de todas as sessões ativas, indexado por número de série. Dispara eventos (`SessaoRegistrada`, `SessaoRemovida`, `AtividadeAtualizada`) que o Backend escuta para persistir no banco. |
| `JflServerOptions.cs` | `JflServerOptions` | Configuração: porta, intervalo de keep-alive. |
| `ICentralAuthorizationProvider.cs` | `ICentralAuthorizationProvider`, `LiberarTodasCentraisAuthorizationProvider` | Ponto de extensão para decidir se um número de série pode conectar. Hoje, a implementação padrão libera todo mundo. |
| `CentralStatusQueryService.cs` | `CentralStatusQueryService`, `CentralStatusQueryResult`, `CentralStatusQueryFailureReason` | Orquestra o envio do comando 0x4D usando a sessão certa do `SessionManager`. |
| `PgmCommandService.cs` | `PgmCommandService`, `PgmCommandResult`, `PgmCommandFailureReason` | Orquestra os comandos 0x50/0x51 (e o "Pulso" = os dois em sequência). |
| `Handlers/IJflCommandHandler.cs` | `IJflCommandHandler` | Interface que todo tratador de comando implementa (`CanHandle(cmd)`, `HandleAsync(...)`). |
| `Handlers/JflCommandDispatcher.cs` | `JflCommandDispatcher` | Recebe um pacote, encontra o handler certo (o que retorna `true` para `CanHandle`) e delega. |
| `Handlers/ConnectionCommandHandler.cs` | `ConnectionCommandHandler` | Trata o 0x21 de verdade (handshake). |
| `Handlers/KeepAliveCommandHandler.cs` | `KeepAliveCommandHandler` | Trata o 0x40 de verdade. |
| `Handlers/Stubs/*.cs` | 8 classes `*CommandHandlerStub` | Reconhecem comandos ainda não implementados (evento, status opcional, arme, PGM — nota: o *handler* de PGM é stub, mas o *envio* de PGM é feito via `PgmCommandService`, que intercepta a resposta antes dela chegar ao dispatcher; ver [`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md)), zonas, data/hora, senha) e só logam. |

**Fluxo de dependência dentro de `Server/`:**

```
JflTcpServer
   depende de → JflCommandDispatcher, SessionManager, JflServerOptions
JflCommandDispatcher
   depende de → todos os IJflCommandHandler (injetados via DI)
ConnectionCommandHandler
   depende de → SessionManager, ICentralAuthorizationProvider, JflServerOptions
CentralStatusQueryService / PgmCommandService
   dependem de → SessionManager (para achar a sessão) e classes de Jfl/Messages/Status (para
                  interpretar a resposta)
```

## 3. Backend — CentralHub.Api (a API REST)

```
Backend/CentralHub.Api/
├── Program.cs                → ponto de entrada; registra tudo na injeção de dependência
├── appsettings.json           → configuração (porta do Jfl, connection string do banco)
├── Controllers/                → recebem requisições HTTP
├── Services/                   → lógica de negócio
├── DTOs/                       → formato dos dados que entram/saem pela API
├── Models/                     → as tabelas do banco de dados (entidades do EF Core)
├── Data/                       → o `DbContext` (configuração do EF Core)
└── Middleware/                 → tratamento global de exceções
```

### 3.1 `Controllers/`

| Arquivo | Rotas | Chama |
|---|---|---|
| `BuildingController.cs` | `api/Building` (CRUD de Prédios) | `BuildingService` |
| `CentralController.cs` | `api/Central` (CRUD de Centrais) **+** `~/api/centrais/{id}/status` **+** `~/api/centrais/{id}/pgm/{pgm}/{ligar\|desligar\|pulso}` **+** `~/api/centrais/{id}/particoes/{p}/{armar\|desarmar\|armar-stay\|armar-away}` **+** `~/api/centrais/{id}/zonas/{z}/{inibir\|desinibir}` **+** `~/api/centrais/{id}/zonas/inibidas` **+** `~/api/centrais/{id}/sessao\|log\|reconectar\|diagnostico` | `CentralService`, `CentralStatusService`, `PgmService`, `ArmService`, `ZoneInhibitService`, `CentralSessionService` |
| `OperationController.cs` | `api/Operation/enviar`, `api/Operation/historico` | `OperationService` — **legado, ver seção 6** |

> Note que `CentralController` mistura rotas com dois estilos diferentes (`api/Central/...` para
> CRUD tradicional, e `~/api/centrais/...` para as operações "ao vivo"). Isso é intencional e está
> documentado no próprio código: as rotas `~/api/centrais/...` foram desenhadas para bater
> exatamente com o que foi pedido em cada tarefa de implementação, usando o prefixo absoluto
> (`~/`) do ASP.NET Core para escapar do prefixo padrão do controller.

### 3.2 `Services/`

| Arquivo | Classe | Responsabilidade | Depende de |
|---|---|---|---|
| `BuildingService.cs` | `BuildingService` | CRUD de Prédios | `AppDbContext` |
| `CentralService.cs` | `CentralService` | CRUD de Centrais (IP/Porta/Usuário/Senha agora são campos opcionais legados, sem uso real) | `AppDbContext` |
| `CentralStatusService.cs` | `CentralStatusService` | Consulta o status ao vivo de uma Central | `AppDbContext`, `CentralStatusQueryService` (SDK) |
| `PgmService.cs` | `PgmService` | Envia comandos de PGM | `AppDbContext`, `PgmCommandService` (SDK) |
| `ArmService.cs` | `ArmService` | Envia comandos de Arme (Armar/Desarmar/Stay/Away, inclui partição especial 99 = eletrificador) | `AppDbContext`, `ArmCommandService` (SDK) |
| `ZoneInhibitService.cs` | `ZoneInhibitService` | Inibe/desinibe uma zona: consulta o estado atual (0x4D) antes de enviar, já que o comando real (0x52) substitui o conjunto inteiro, não soma | `AppDbContext`, `CentralStatusQueryService` (SDK), `ZoneInhibitCommandService` (SDK) |
| `CentralSessionService.cs` | `CentralSessionService` | Monta o snapshot da sessão real (`/sessao`), o log de atividade (`/log`), o diagnóstico (`/diagnostico`) e processa `/reconectar` — **nunca abre conexão nenhuma**, só lê o `SessionManager` e limpa a sessão registrada | `AppDbContext`, `SessionManager` (SDK), `SessionActivityLogService` |
| `JflServerHostedService.cs` | `JflServerHostedService` | Sobe/desce o `JflTcpServer` junto com o ciclo de vida da aplicação | `JflTcpServer` (SDK) |
| `JflSessionPersistenceService.cs` | `JflSessionPersistenceService` | Ouve os eventos do `SessionManager` e grava no banco (Status, IP, KeepAlive, ConectadoDesde) | `SessionManager` (SDK), `AppDbContext` |
| `Logging/SessionActivityLogService.cs` | `SessionActivityLogService` | `IHostedService` — buffer em memória (últimas 500 entradas) dos logs estruturados do SDK, resolvendo `NumeroSerie` via `RemoteEndPoint` quando necessário; alimenta o painel "Log da Central" e os campos SEQ/bytes/latência/último comando de `/sessao` | `SessionManager` (SDK, só o evento `SessaoRegistrada`) |
| `Logging/SdkActivityLoggerProvider.cs` + `SdkActivityLogger.cs` | `SdkActivityLoggerProvider`/`SdkActivityLogger` | `ILoggerProvider`/`ILogger` customizados, 100% aditivos — capturam as propriedades nomeadas (`Cmd`, `Seq`, `BytesRecebidos`, `BytesEnviados`, `TempoRespostaMs`...) que as classes `CentralHub.SDK.Jfl.*` já logam hoje via `_logger.LogInformation("...{Prop}...", valor)`, sem alterar nenhum arquivo do SDK | `SessionActivityLogService` (resolvido de forma preguiçosa/`Lazy<T>` para evitar dependência circular com `ILoggerFactory`) |
| `KeepAliveService.cs` | `KeepAliveService` | **Legado, desregistrado** — não roda mais (ver seção 6) | `AdapterFactory` (SDK legado) |
| `OperationService.cs` | `OperationService` | **Legado** — envio de PGM simulado, tela "Operação" antiga | `AdapterFactory` (SDK legado) |

> `ConnectionService.cs` e o endpoint `POST /api/central/testar-conexao` **foram removidos**
> (não apenas marcados como legado) nesta limpeza — ver seção 6 e
> [`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md).

### 3.3 `DTOs/` (Data Transfer Objects — formato dos dados na API)

Cada arquivo agrupa os DTOs de um recurso: `BuildingDtos.cs`, `CentralDtos.cs`,
`CentralStatusDtos.cs` (todo o retorno de `/status`), `OperationDtos.cs` (legado), `PgmDtos.cs`,
`ArmDtos.cs`, `ZoneInhibitDtos.cs`. DTOs existem para **nunca expor as entidades do banco
diretamente pela API** — por exemplo, `CentralDto` nunca inclui o campo `Senha`, mesmo a entidade
`Central` tendo esse campo.

### 3.4 `Models/` (entidades do banco)

`Building.cs`, `Central.cs`, `CentralSession.cs`, `History.cs` — detalhados campo a campo em
[`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md).

### 3.5 `Data/AppDbContext.cs`

A classe que representa "a conexão com o banco" no estilo Entity Framework Core — define quais
tabelas existem (`DbSet<T>`) e como cada campo é configurado (tamanho máximo, obrigatoriedade,
relacionamentos entre tabelas).

### 3.6 `Middleware/ExceptionHandlingMiddleware.cs`

Captura qualquer exceção não tratada em qualquer Controller e devolve uma resposta JSON
padronizada, em vez de vazar detalhes internos (stack trace) para quem chamou a API.

## 4. Frontend — a interface web

```
Frontend/src/
├── main.tsx                    → ponto de entrada do React
├── App.tsx                      → define as rotas (React Router)
├── services/api.ts               → configuração do cliente HTTP (Axios)
├── types/index.ts                → todos os tipos TypeScript espelhando os DTOs do Backend
├── utils/formatters.ts           → formatação de data/hora/duração compartilhada entre telas
├── pages/
│   ├── BuildingsPage.tsx          → tela de cadastro de Prédios
│   ├── CentralsPage.tsx           → tela de cadastro/lista de Centrais (Nome, Número de Série, Prédio — sem IP/Porta/Usuário/Senha)
│   ├── CentralDetailPage.tsx      → "Tela Central" — painel de monitoramento de sessão + status ao vivo + PGM
│   └── OperationPage.tsx          → tela legada de operação de PGM (caminho antigo)
└── components/
    ├── PgmPanel.tsx               → painel reutilizável dos 16 botões de PGM
    ├── ArmPanel.tsx               → painel de Arme: 16 partições (Armar/Desarmar/Stay/Away) + card do eletrificador
    ├── ZonasPanel.tsx             → chips de zona clicáveis (inibir/desinibir quando permiteInibir=true)
    └── session/                   → painel de monitoramento de sessão (ver 09_WEB_GUIDE.md)
        ├── StatusConexaoCard.tsx      → card 🟢/🔴/🟡 com os campos da sessão + botão "Atualizar Status"
        ├── SessaoTcpPanel.tsx         → detalhe da sessão TCP (socket, handshake, keep-alive, SEQ...)
        ├── IndicadoresChips.tsx       → chips 🟢/🔴 (sessão ativa, handshake, keep-alive, cadastro...)
        ├── LogCentralPanel.tsx        → lista rolável do log de atividade, com polling próprio
        ├── DetalhesSessaoModal.tsx    → modal com todos os campos da sessão
        └── DiagnosticoPanel.tsx       → checklist de diagnóstico
```

Detalhado tela a tela em [`09_WEB_GUIDE.md`](09_WEB_GUIDE.md).

## 5. SDK.Tests — os testes automatizados

```
SDK/CentralHub.SDK.Tests/
├── Protocol/            → testes de ChecksumCalculator, PacketBuilder, PacketParser, JflFrameReader
├── Messages/             → testes de ConnectionRequest, JflVersion, e da pasta Status/
├── Server/                → testes de SessionManager, CentralStatusQueryService, PgmCommandService,
│                            correlação SEQ (JflSessionSendAndWaitTests), e o teste de integração
│                            ponta a ponta com sockets reais (JflTcpServerIntegrationTests)
└── TestUtilities/          → duplos de teste (DuplexMemoryStream, TrickleStream) usados para simular
                              conexões sem precisar de sockets de verdade
```

141 testes no SDK.Tests (inclui `ArmCommandServiceTests`/`ZoneInhibitCommandServiceTests`), mais
um projeto separado `Backend/CentralHub.Api.Tests` (24 testes, cobrindo `SessionActivityLogService`,
`CentralSessionService`, `ArmService` e `ZoneInhibitService` contra um `JflTcpServer` real e o
Central Simulator — sem mocks) no momento em que este documento foi escrito — ver
[`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md) para como rodá-los.

## 6. Código legado — o que existe mas NÃO é usado no fluxo real

> ⚠️ Esta seção é uma das mais importantes deste documento. Um desenvolvedor novo que copiar
> padrões desses arquivos como referência vai reintroduzir a arquitetura invertida que a
> auditoria original do projeto identificou como incorreta.

| Arquivo/Pasta | O que faz | Por que existe ainda |
|---|---|---|
| `SDK/CentralHub.SDK/Adapters/*.cs` (`AdapterFactory`, `ICentralAdapter`, `JflAdapter`, `IntelbrasAdapter`, `FakeAdapter`, `TcpConnectionHelper`) | Simula conexão **de saída** (o Backend discando para o IP cadastrado da central) — modelo invertido em relação ao protocolo real. `JflAdapter`/`IntelbrasAdapter` são 100% mock: nunca implementaram nenhum protocolo de verdade. Marcados com `[Obsolete]` (comportamento idêntico, só emite warning de compilação). | Ainda é a base do fluxo simulado de `OperationService`/`OperationPage` (tela "Operação" antiga, fora do escopo desta limpeza); remover exige reescrever esse fluxo também. |
| `Backend/.../Services/KeepAliveService.cs` | Um `BackgroundService` que rodava a cada 30s testando TCP de saída | **Desregistrado** em `Program.cs` (linha comentada) porque conflitava com o `JflSessionPersistenceService` real, sobrescrevendo `Status`. A classe continua existindo no projeto, só não roda mais. |
| `Backend/.../Services/OperationService.cs` + `Controllers/OperationController.cs` + `Frontend/.../OperationPage.tsx` | Tela "Operação" original do MVP, para enviar PGM — **sempre retornou sucesso simulado**, nunca falou com uma central de verdade. | Mantida por compatibilidade com o fluxo antigo de cadastro; o caminho real de PGM hoje é `PgmService`/`PgmCommandService`, acessado pela Tela Central, não por esta tela. |

> **`ConnectionService.cs` e o endpoint `POST /api/central/testar-conexao` foram removidos**
> completamente (não marcados como legado — apagados de verdade), porque não tinham nenhum
> consumidor fora da tela "Testar Conexão" que também foi removida. Ver
> [`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md) para a arquitetura que os
> substituiu (`CentralSessionService` + `SessionManager`).

## 7. Mapa de dependências entre classes

```
                         ┌───────────────────┐
                         │     Frontend        │
                         │  (React/TS/Axios)   │
                         └──────────┬───────────┘
                                    │ HTTP/JSON
                         ┌──────────▼───────────┐
                         │    Controllers        │
                         └──────────┬───────────┘
                                    │
                         ┌──────────▼───────────┐
                         │      Services          │
                         │ (CentralStatusService, │
                         │  PgmService, etc.)      │
                         └───┬──────────────┬────┘
                             │              │
                  ┌──────────▼───┐   ┌──────▼────────────┐
                  │ AppDbContext  │   │  SDK (CentralHub.  │
                  │ (EF Core /    │   │  SDK.Jfl.Server)   │
                  │  SQLite)      │   │                     │
                  └──────────────┘   └──────┬─────────────┘
                                             │
                                  ┌──────────▼─────────────┐
                                  │  SessionManager           │
                                  │  (sessões TCP em memória) │
                                  └──────────┬─────────────┘
                                             │
                                  ┌──────────▼─────────────┐
                                  │  JflSession               │
                                  │  (1 por central conectada) │
                                  └──────────┬─────────────┘
                                             │ TCP real
                                  ┌──────────▼─────────────┐
                                  │  Active 100 Bus           │
                                  │  (hardware físico)         │
                                  └───────────────────────────┘
```

## 8. Boas práticas ao navegar no código

- Ao procurar "como um comando é implementado", comece sempre pelo handler em
  `Jfl/Server/Handlers/` (para comandos que a central envia primeiro) ou pelo Service
  correspondente em `Jfl/Server/*.cs` (para comandos que o servidor envia primeiro, como Status e
  PGM).
- Nunca copie um padrão de `Adapters/` para código novo — copie os padrões de `Jfl/Server/` em vez
  disso.
- Todo comando novo deveria seguir exatamente o padrão de `CentralStatusQueryService`/
  `PgmCommandService` — ver [`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md).

## 9. Problemas comuns

- **"Alterei o Adapter e nada mudou"** — porque o Adapter não está no caminho real. Ver seção 6.
- **"Meu novo Service não recebe o `SessionManager`"** — verifique se ele foi registrado em
  `JflServiceCollectionExtensions.AddJflServer` (SDK) e não só em `Program.cs` (Backend).

## 10. Como testar cada camada

Ver [`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md) para os comandos exatos (`dotnet build`,
`dotnet test`, `npm run build`).

## 11. Como depurar cada camada

- **SDK**: os testes de integração (`JflTcpServerIntegrationTests`) sobem um servidor real numa
  porta efêmera — ótimo ponto de partida para depurar com breakpoints sem precisar de hardware.
- **Backend**: rodar `dotnet run` dentro de `Backend/CentralHub.Api` e observar os logs no
  console — o nível `Debug` já está habilitado para o namespace `CentralHub.SDK.Jfl`.
- **Frontend**: `npm run dev` dentro de `Frontend/`, e usar as ferramentas de desenvolvedor do
  navegador (aba Network, para ver as chamadas à API; aba Console, para erros JS).

## 12. FAQ

**P: Por que `Jfl/Messages/Status/CentralStatusResponse.cs` é usado tanto pelo Status quanto pelo
PGM?**
R: Porque, no protocolo JFL, a resposta a qualquer comando da "tela monitorar" (armar, desarmar,
PGM, status) usa exatamente o mesmo formato — reaproveitar o parser evita duplicar ~300 linhas de
lógica de decodificação. Ver [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), seção 16-17.

**P: Onde fica a lógica de "o que fazer quando o checksum está errado"?**
R: Dentro de `PacketParser.TryParse`, que devolve `JflParseStatus.ChecksumMismatch`; quem consome
isso (`JflFrameReader`) decide descartar o pacote e ressincronizar.

## 13. Checklist

- [ ] Sei localizar, sem buscar, onde fica o parser de status.
- [ ] Sei explicar por que o SDK não depende do Backend.
- [ ] Sei listar os 4 arquivos/serviços legados e por que ainda existem.
- [ ] Sei onde adicionar um Service novo e como registrá-lo.

---

**Documento anterior:** [`04_PROTOCOL_FLOW.md`](04_PROTOCOL_FLOW.md)
**Próximo documento:** [`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
