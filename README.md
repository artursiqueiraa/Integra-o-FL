# CentralHub — Integração JFL Active 100 Bus

CentralHub é um sistema de monitoramento e operação remota de centrais de alarme **JFL Active 100
Bus**, via protocolo proprietário JFL sobre TCP. Um Backend .NET recebe conexões das centrais
(elas são sempre o cliente TCP, nunca o servidor), mantém sessões vivas por central, e expõe uma
API + interface web para consultar status, armar/desarmar, acionar PGMs e inibir zonas em tempo
real, contra hardware físico homologado.

> Se você está chegando agora no projeto, este documento é o ponto de partida. Para os detalhes
> técnicos completos (protocolo, banco, arquitetura de sessão, etc.), veja o índice em
> [`Documentation/INDEX.md`](Documentation/INDEX.md).

---

## Índice

1. [Arquitetura atual](#1-arquitetura-atual)
2. [Fluxo de comunicação TCP](#2-fluxo-de-comunicação-tcp)
3. [Tecnologias](#3-tecnologias)
4. [Estrutura de pastas](#4-estrutura-de-pastas)
5. [Como rodar o projeto](#5-como-rodar-o-projeto)
6. [Como configurar](#6-como-configurar)
7. [Cadastrando um Prédio e uma Central](#7-cadastrando-um-prédio-e-uma-central)
8. [Cadastrando PGMs e Zonas de um Prédio](#8-cadastrando-pgms-e-zonas-de-um-prédio)
9. [Usando a Tela Central](#9-usando-a-tela-central)
10. [Usando a aba Operação](#10-usando-a-aba-operação)
11. [Histórico de evolução](#11-histórico-de-evolução)
12. [Documentação completa](#12-documentação-completa)

---

## 1. Arquitetura atual

**A central é sempre o cliente TCP. O CentralHub nunca disca para fora — ele só escuta.** A
central física (configurada via ActiveNet com o IP/porta do servidor) abre a conexão; o Backend
aceita, identifica a sessão pelo **Número de Série** enviado no handshake (nunca pelo IP — o IP
pode mudar, o Número de Série não) e mantém essa sessão viva enquanto o Keep-Alive continuar
chegando.

| Etapa do protocolo | Status |
|---|---|
| Handshake (0x21) | ✅ Homologado contra hardware real |
| Keep-Alive (0x40) | ✅ Homologado contra hardware real |
| Status (0x4D — partições, zonas, PGMs, eletrificador, bateria, AC) | ✅ Homologado contra hardware real |
| PGM (Ligar / Desligar / Pulso) | ✅ Homologado contra hardware real |
| Arme (Armar / Desarmar / Stay / Away) | ✅ Homologado contra hardware real |
| Desarme | ✅ Homologado contra hardware real |
| Zonas (Inibir / Desinibir / Consultar) | ✅ Homologado contra hardware real |

A identificação de sessão por Número de Série (não por IP) existe desde a reescrita da camada de
rede documentada em [`Documentation/ARQUITETURA_SESSION_MANAGER.md`](Documentation/ARQUITETURA_SESSION_MANAGER.md)
— não há, em nenhum lugar do fluxo atual, um botão "Testar Conexão" nem um cadastro de IP/Porta
que o Backend disque; esses conceitos pertencem a uma arquitetura anterior, já removida (ver
seção 11).

## 2. Fluxo de comunicação TCP

```
Central Active 100 Bus (cliente TCP, disca para o servidor)
        │
        ▼  TCP :8085
JflTcpServer            → aceita a conexão, delega o parsing de cada pacote
        │
        ▼
ConnectionCommandHandler → identifica o comando (Handshake, Keep-Alive, Status, PGM, Arme, Zonas...)
        │
        ▼
SessionManager           → resolve/mantém a sessão pelo Número de Série (JflSession)
        │
        ├──▶ PgmService            (Ligar / Desligar / Pulso de PGM)
        ├──▶ ArmService            (Armar / Desarmar / Stay / Away)
        └──▶ ZoneInhibitService    (Inibir / Desinibir zonas)
```

O mesmo caminho vale nos dois sentidos: comandos vindos da interface web (Tela Central **e** aba
Operação) descem por `PgmService`/`ArmService`/`ZoneInhibitService` → `SessionManager` →
`JflSession` até a sessão TCP já aberta pela central — nenhum comando abre uma conexão nova.

## 3. Tecnologias

| Camada | Stack |
|---|---|
| **Frontend** | React 18 + TypeScript + Vite + Material UI 5 + Axios + React Router (polling via `setInterval`, sem WebSocket/SignalR ainda — ver [`Documentation/14_ROADMAP.md`](Documentation/14_ROADMAP.md)) |
| **Backend** | ASP.NET Core 9 Web API + EF Core 9 + SQLite (`EnsureCreated()`, sem migrations formais ainda) + Swagger |
| **SDK** | `CentralHub.SDK` — implementação do protocolo JFL (framing 0x7B, handshake, keep-alive, parser/builder de comandos, `JflTcpServer`/`SessionManager`/`JflSession`), testada com 141+ testes unitários e validada contra hardware físico |
| **Banco** | SQLite local (`centralhub.db`), schema aplicado via `EnsureCreated()` |

## 4. Estrutura de pastas

```
central/
├── Backend/
│   ├── CentralHub.Api/            → Web API (Controllers, Services, Models, DTOs, Data)
│   └── CentralHub.Api.Tests/      → testes do Backend (EF Core InMemory)
├── SDK/
│   ├── CentralHub.SDK/            → protocolo JFL real (Jfl/), + Adapters/ legado ([Obsolete])
│   ├── CentralHub.SDK.Tests/      → suíte de testes do protocolo (a maior do projeto)
│   ├── CentralHub.SDK.Benchmarks/ → benchmarks de performance do parser/builder
│   └── CentralHub.SDK.Tools/      → utilitários de linha de comando para debug do protocolo
├── Frontend/
│   └── src/
│       ├── pages/                 → CentralsPage, CentralDetailPage, BuildingsPage, OperationPage
│       ├── components/            → ArmPanel, PgmPanel, ZonasPanel, CadastroPgmZonaPanel
│       ├── components/session/    → StatusConexaoCard, SessaoTcpPanel, LogCentralPanel, DiagnosticoPanel...
│       ├── services/api.ts        → cliente Axios
│       └── types/index.ts         → tipos TypeScript espelhando os DTOs do Backend
├── Simulator/
│   ├── CentralHub.Simulator/      → simulador de central JFL (cliente TCP fiel ao protocolo, para testes/dev)
│   └── CentralHub.StressTest/     → ferramenta de teste de carga
├── Documentation/                 → documentação técnica completa (ver seção 12)
└── CHANGELOG.md
```

**Backend — pastas internas de `CentralHub.Api/`:**

- `Controllers/` — `CentralController` (CRUD + status/PGM/arme/zonas/sessão/log/diagnóstico),
  `BuildingController` (CRUD de Prédios), `OperationController` (aba Operação, mesmo fluxo real de
  PGM), `PgmPredioController`/`ZonaPredioController` (cadastro dinâmico de PGMs/Zonas por Prédio).
- `Services/` — `PgmService`, `ArmService`, `ZoneInhibitService`, `CentralStatusService`,
  `CentralSessionService` (todos resolvem `Central.NumeroSerie` no banco e delegam ao SDK via
  `SessionManager`), além de `BuildingService`, `PgmPredioService`, `ZonaPredioService`.
  `JflServerHostedService` sobe o `JflTcpServer` junto com o Backend.
- `Models/` — `Central`, `Building`, `CentralSession`, `History`, `PgmPredio`, `ZonaPredio`.
- `Data/AppDbContext.cs` — contexto EF Core.

## 5. Como rodar o projeto

### Backend

```bash
cd Backend/CentralHub.Api
dotnet restore
dotnet run
```

- API/Swagger: `http://localhost:5000/swagger`
- Servidor TCP do protocolo JFL: porta `8085` (configurável, ver seção 6)
- O banco SQLite (`centralhub.db`) é criado automaticamente na primeira execução.

### Frontend

```bash
cd Frontend
npm install
npm run dev
```

- Aplicação web: `http://localhost:5173`

### Simulador (opcional, para testar sem hardware físico)

```bash
cd Simulator/CentralHub.Simulator
dotnet run
```

O simulador se conecta como cliente TCP no Backend (mesma porta `8085`), reproduzindo fielmente o
protocolo JFL — útil para desenvolvimento sem depender de uma central física disponível.

## 6. Como configurar

A porta do servidor TCP e o nível de log do protocolo ficam em
`Backend/CentralHub.Api/appsettings.json`:

```json
{
  "Jfl": {
    "Porta": 8085
  },
  "Logging": {
    "LogLevel": {
      "CentralHub.SDK.Jfl": "Debug"
    }
  }
}
```

As origens liberadas para CORS (o Frontend em desenvolvimento) ficam em
`appsettings.Development.json` (`http://localhost:5173`/`5174`).

**Na central física (via ActiveNet):** configure o IP e a porta do servidor CentralHub (`8085`)
como destino da conexão de saída da central — é a central que disca para o CentralHub, não o
contrário.

## 7. Cadastrando um Prédio e uma Central

1. Na tela **Prédios**, cadastre um Prédio (Nome).
2. Na tela **Centrais**, cadastre uma Central vinculada ao Prédio, informando **Nome** e
   **Número de Série** — é esse Número de Série que o `SessionManager` usa para casar a conexão
   TCP real (vinda da central física) com este cadastro. Não há campo de IP/Porta/Usuário/Senha a
   preencher nem qualquer teste de conexão bloqueando o salvamento: o cadastro é só metadado: a
   sessão de verdade só existe quando a central conecta.
3. Assim que a central física (configurada com o Número de Série correspondente) abrir a conexão
   TCP, o painel de monitoramento da central (**Tela Central**) passa a mostrar **Status: Online**,
   IP/porta da sessão real, horário de conexão, último Keep-Alive, e o log de atividade — tudo lido
   diretamente do `SessionManager`, nunca de um teste manual.

## 8. Cadastrando PGMs e Zonas de um Prédio

A aba **Operação** funciona de forma totalmente dinâmica: nenhum número de PGM ou Zona é digitado
na hora de operar — eles vêm de um catálogo cadastrado previamente.

1. Na aba Operação, abra a seção **Cadastro de PGMs e Zonas**.
2. Cadastre cada PGM informando: Número (1–16), Nome (ex.: "Portão da garagem"), Tipo e Ícone
   (opcionais).
3. Cadastre cada Zona informando: Número (1–99), Nome (ex.: "Sensor sala de reunião") e Tipo
   (opcional).
4. Esses cadastros (`PgmPredio`/`ZonaPredio`) são puro metadado — nome/tipo/ícone para exibição.
   Eles **não** alteram o protocolo: o número informado é exatamente o número físico da PGM/Zona
   na central, repassado para os mesmos `PgmService`/`ZoneInhibitService` reais.

## 9. Usando a Tela Central

A **Tela Central** é o painel de monitoramento e operação direta de uma central específica:

- **Status da Conexão** — Online/Offline/Aguardando, Número de Série, Modelo, Firmware, IP/porta
  da sessão real, horário de conexão, tempo conectado, último Keep-Alive, latência.
- **Detalhes da Sessão** / **Diagnóstico** — checklist (sessão ativa, handshake realizado,
  Keep-Alive em dia, central vinculada a um Prédio, etc.), sempre lido do `SessionManager` no
  momento da consulta.
- **Log da Central** — atividade recente (comandos, respostas, bytes trocados), capturada em
  tempo real a partir dos logs estruturados do SDK.
- **Painel de Arme** — Armar / Desarmar / Stay / Away.
- **Painel de PGM** e **Painel de Zonas** — Ligar/Desligar/Pulso de PGM e Inibir/Desinibir zona,
  com os números 1–16 (PGM) / 1–99 (Zona) fixos do protocolo.
- **Reconectar** — não abre uma conexão nova; apenas encerra a sessão registrada no
  `SessionManager`, deixando a central reconectar sozinha (comportamento real do protocolo).

## 10. Usando a aba Operação

A aba **Operação** é uma forma alternativa, orientada a Prédio, de acionar a mesma central:

1. Selecione o **Prédio** e a **Central** (se o prédio tiver só uma central, ela é selecionada
   automaticamente).
2. O painel mostra Status da Conexão, Armar/Desarmar, e os painéis de PGM/Zonas — mas em vez dos
   números fixos 1–16/1–99, mostra os **nomes cadastrados** no catálogo da seção 8 (ex.: "Portão
   da garagem" em vez de "PGM 3").
3. Cada ação (ligar PGM, armar partição, inibir zona) chama exatamente os mesmos endpoints reais
   (`PgmService`/`ArmService`/`ZoneInhibitService`) que a Tela Central usa — **não existe caminho
   simulado**: um clique aqui aciona o hardware físico do mesmo jeito que um clique na Tela
   Central.

## 11. Histórico de evolução

O projeto passou por três limpezas arquiteturais relevantes, documentadas em detalhe em
[`Documentation/Protocol/20_CHANGELOG.md`](Documentation/Protocol/20_CHANGELOG.md) e
[`Documentation/ARQUITETURA_SESSION_MANAGER.md`](Documentation/ARQUITETURA_SESSION_MANAGER.md):

1. **Remoção da arquitetura de discagem de saída.** A versão inicial do projeto tentava abrir uma
   conexão TCP de saída do Backend para IP/Porta cadastrados manualmente ("Testar Conexão") —
   modelo incompatível com o protocolo real da JFL, em que é a central quem disca para o servidor.
   `ConnectionService` e o endpoint `POST /api/central/testar-conexao` foram removidos por
   completo, substituídos pelo painel de monitoramento de sessão via `SessionManager`.
2. **Remoção da simulação da aba Operação.** A aba Operação originalmente enviava comandos por um
   caminho separado e sempre simulado (`OperationService` → `AdapterFactory` → `FakeAdapter`), que
   nunca tocava hardware real. `OperationService` foi removido por completo; hoje
   `OperationController` chama `PgmService`/`ArmService` diretamente — o mesmo caminho real da
   Tela Central.
3. **Operação Dinâmica por Prédio.** A aba Operação deixou de exigir a digitação manual de números
   de PGM/Zona: passou a existir um cadastro (`PgmPredio`/`ZonaPredio`) por Prédio/Central, puro
   metadado (nome/tipo/ícone), reaproveitando 100% dos serviços reais existentes.

O código de `SDK/CentralHub.SDK/Adapters/*` (`AdapterFactory`, `JflAdapter`, `IntelbrasAdapter`,
`FakeAdapter`, `TcpConnectionHelper`) permanece no repositório marcado como `[Obsolete]`, mas hoje
**sem nenhum consumidor real** — é um candidato à remoção total numa próxima faxina (ver
[`Documentation/14_ROADMAP.md`](Documentation/14_ROADMAP.md), item 13), mantido por precaução, não
porque ainda seja necessário.

## 12. Documentação completa

Este README cobre o essencial para começar. Para detalhes técnicos aprofundados — protocolo byte a
byte, arquitetura de sessão, guia de banco de dados, como adicionar um novo comando, FAQ, roadmap —
veja o índice completo em [`Documentation/INDEX.md`](Documentation/INDEX.md).
