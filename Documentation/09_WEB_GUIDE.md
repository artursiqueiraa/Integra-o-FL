# 09 — WEB GUIDE

> **Público-alvo:** qualquer pessoa que vai operar ou dar manutenção na interface web — inclui
> tanto o "manual do usuário" (como usar cada tela) quanto o "manual do desenvolvedor" (como cada
> tela é construída por baixo).

---

## Índice

1. [Visão geral das telas](#1-visão-geral-das-telas)
2. [Tela: Prédios (BuildingsPage)](#2-tela-prédios-buildingspage)
3. [Tela: Centrais (CentralsPage) — cadastro](#3-tela-centrais-centralspage--cadastro)
4. [Tela: Central (CentralDetailPage) — a tela operacional principal](#4-tela-central-centraldetailpage--a-tela-operacional-principal)
5. [Componentes: Painéis de comando (PGM, Arme, Zonas)](#5-componentes-painéis-de-comando-pgm-arme-zonas)
6. [Tela: Operação (legada)](#6-tela-operação-legada)
7. [Como o Frontend fala com o Backend (fluxo REST)](#7-como-o-frontend-fala-com-o-backend-fluxo-rest)
8. [Como funciona a atualização automática](#8-como-funciona-a-atualização-automática)
9. [Mapa de rotas](#9-mapa-de-rotas)
10. [Casos de uso reais](#10-casos-de-uso-reais)
11. [Boas práticas](#11-boas-práticas)
12. [Problemas comuns](#12-problemas-comuns)
13. [Como testar](#13-como-testar)
14. [Como depurar](#14-como-depurar)
15. [FAQ](#15-faq)
16. [Checklist](#16-checklist)

---

## 1. Visão geral das telas

```
┌──────────────────────────────────────────────────────────────┐
│  Barra superior:  CentralHub   [Prédios] [Centrais] [Operação]│
└──────────────────────────────────────────────────────────────┘
        │                  │                       │
        ▼                  ▼                       ▼
   BuildingsPage      CentralsPage             OperationPage
   (rota "/")         (rota "/centrais")        (rota "/operacao")
                            │                    [LEGADA — não usar
                            │ clique no ícone      para operação real]
                            │ de "olho" numa linha
                            ▼
                     CentralDetailPage
                     (rota "/centrais/:id")
                     ["Tela Central" — onde
                      a operação real acontece]
                            │
                            │ renderiza dentro de si
                            ▼
                       PgmPanel (componente)
```

## 2. Tela: Prédios (BuildingsPage)

**Arquivo:** [`Frontend/src/pages/BuildingsPage.tsx`](../Frontend/src/pages/BuildingsPage.tsx).
**Rota:** `/`.

Cadastro simples (CRUD — Criar, Ler, Atualizar, Apagar) de prédios: nome e descrição. Um prédio
precisa existir antes de qualquer Central poder ser cadastrada (relação obrigatória, ver
[`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md)). Não tem nada de especial em termos de protocolo —
é um formulário + tabela padrão, chamando `GET/POST/PUT/DELETE /api/building`.

## 3. Tela: Centrais (CentralsPage) — cadastro

**Arquivo:** [`Frontend/src/pages/CentralsPage.tsx`](../Frontend/src/pages/CentralsPage.tsx).
**Rota:** `/centrais`.

Formulário para cadastrar uma Central: **Nome**, **Número de Série** (o campo que realmente importa
— é a chave que o `SessionManager` usa para casar uma conexão real com este cadastro) e **Prédio**.
Salvar não exige mais nenhum teste de conexão — o botão **"Testar Conexão"** e os campos
IP/Porta/Usuário/Senha **foram removidos da tela** (continuam existindo como campos legados
opcionais no Backend, para não descartar cadastros antigos — ver
[`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md) e
[`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md)). Não é necessário (nem existe forma) de "testar" a
conexão a partir daqui — o que importa é o `NumeroSerie` estar cadastrado corretamente e a central
estar configurada (via ActiveNet) para discar para o CentralHub; a tela Central mostra o status
real da conexão assim que ela acontecer.

A tabela lista Id, Nome, **Número de Série** (no lugar da antiga coluna "IP"), Fabricante, Modelo e
Status. Cada linha tem um ícone de "olho" (👁) que leva para a Tela Central daquela linha
específica.

## 4. Tela: Central (CentralDetailPage) — a tela operacional principal

**Arquivo:** [`Frontend/src/pages/CentralDetailPage.tsx`](../Frontend/src/pages/CentralDetailPage.tsx).
**Rota:** `/centrais/:id` (onde `:id` é o identificador numérico da Central).

Esta é a tela onde a operação real do dia a dia acontece — foi reescrita para refletir a
arquitetura real de sessão (ver [`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md)),
e é dividida nos seguintes blocos, todos alimentados só por consultas — **nenhuma ação nesta tela
abre uma conexão de saída**:

### 4.1 Status da Conexão (`StatusConexaoCard`)

O primeiro bloco da tela. Mostra o indicador **🟢 Online / 🔴 Offline / 🟡 Aguardando conexão**
(este último é só um estado visual transitório do Frontend, mostrado por alguns segundos logo após
clicar em "Reconectar" — o Backend nunca calcula um terceiro estado, só Online/Offline, porque o
`SessionManager` só registra uma sessão depois do handshake concluído) e os campos: Status, Número
de Série, Modelo, Firmware, IP da Sessão, Porta Remota, Data/Hora da Conexão, Último KeepAlive,
Tempo Conectado, Latência, Tempo desde o Último KeepAlive, Sessão Ativa (Sim/Não), além dos chips
de indicadores (`IndicadoresChips`: 🟢/🔴 sessão ativa, handshake, keep-alive, status sincronizado,
central cadastrada, número de série divergente). Tudo vem de `GET /api/centrais/{id}/sessao`,
atualizado a cada 5 segundos, com um botão **"Atualizar Status"** para forçar uma nova consulta
imediata (nunca abre nada, só repete a mesma consulta).

### 4.2 Botões de ação

- **Detalhes da Sessão** — abre o modal `DetalhesSessaoModal` com o snapshot completo da sessão
  (Número Série, Modelo, Firmware, MAC, IP, Porta, horários, Último Comando, Último Pacote, Bytes
  Recebidos/Enviados, SEQ), reaproveitando o mesmo payload de `/sessao`.
- **Solicitar Status** — chama `GET /api/centrais/{id}/status` imediatamente (a mesma consulta que
  já roda no polling do bloco 4.5), sem recarregar a página.
- **Reconectar** — abre um diálogo de confirmação deixando explícito que **isso não abre nenhuma
  conexão**: só chama `POST /api/centrais/{id}/reconectar`, que limpa a sessão registrada no
  `SessionManager` (mesmo efeito de a central cair sozinha). A mensagem devolvida pela API ("A
  central deverá iniciar uma nova conexão automaticamente.") é exibida num alerta na tela.

### 4.3 Sessão TCP (`SessaoTcpPanel`)

Só aparece quando existe uma sessão consultada. Mostra: IP remoto, porta remota, socket conectado,
handshake realizado, keep-alive ativo, tempo conectado, último pacote recebido, último comando
recebido, SEQ atual, último erro — mesmos dados de `/sessao`, numa vista mais técnica/detalhada que
o card principal.

### 4.4 Diagnóstico (`DiagnosticoPanel`)

Checklist automático (`GET /api/centrais/{id}/diagnostico`), atualizado junto com a sessão: sessão
ativa, handshake realizado, keep-alive dentro do prazo, número de série cadastrado, central
vinculada a um prédio, entre outros — cada item mostra ✅/❌/❔ (❔ quando ainda não há dado
suficiente para decidir, o que é esperado e não é bug).

### 4.5 Log da Central (`LogCentralPanel`)

Lista rolável, mais recente primeiro, com o log de atividade capturado da sessão (conexão,
handshake, keep-alive, comandos enviados/recebidos...). Tem seu próprio polling (a cada ~4
segundos), independente do resto da tela, consultando `GET /api/centrais/{id}/log`.

### 4.6 Bloco de Status (partições/zonas/PGMs/bateria)

Mostra: Bateria (tipo + percentual ou tensão), Alimentação AC (Normal/Problema), Eletrificador,
Data/Hora da própria central, as 16 Partições (como "chips" coloridos, só leitura), e os Problemas
reportados (só os que estão `true`, para não listar 40 itens negativos). Vem de
`GET /api/centrais/{id}/status`, atualizado a cada **5 segundos** (polling — ver seção 8). As
Zonas deste bloco viraram o componente `ZonasPanel` (interativo — ver seção 5.3).

### 4.7 Bloco de Arme, PGM e Zonas

Os componentes `ArmPanel` (seção 5.2) e `PgmPanel` (seção 5.1), nessa ordem, seguidos do
`ZonasPanel` (seção 5.3, embutido dentro do bloco de Status descrito em 4.6).

## 5. Componentes: Painéis de comando (PGM, Arme, Zonas)

### 5.1 Painel de PGM (PgmPanel)

**Arquivo:** [`Frontend/src/components/PgmPanel.tsx`](../Frontend/src/components/PgmPanel.tsx).

Mostra um cartão para cada uma das 16 PGMs, com:
- O número da PGM.
- Um indicador de estado ("Ligada"/"Desligada", com cor).
- Um aviso se a PGM não tem permissão configurada.
- Três botões: **Ligar**, **Desligar**, **Pulso**.

Fluxo de clique num botão:

```
1. Operador clica "Ligar" na PGM 3.
2. Abre um diálogo de CONFIRMAÇÃO ("Enviar o comando Ligar para a PGM 3?").
   (Se for Pulso, o diálogo também pede a duração em milissegundos.)
3. Operador confirma.
4. O card daquela PGM mostra um indicador de execução (spinner) — os botões daquela
   PGM ficam desabilitados enquanto isso.
5. POST /api/centrais/{id}/pgm/3/ligar é chamado.
6. Se sucesso: mensagem de sucesso aparece, e o status é recarregado (para refletir
   o novo estado real, confirmado pela central).
7. Se erro: mensagem de erro aparece, explicando o motivo (offline, timeout, sem
   confirmação, etc — ver 08_COMMANDS_GUIDE.md).
```

Esse fluxo de confirmação existe **de propósito** — PGMs podem estar ligadas a coisas físicas reais
(portões, luzes, sirenes), e um clique acidental não deve disparar uma ação irreversível sem
confirmação.

### 5.2 Painel de Arme (ArmPanel)

**Arquivo:** [`Frontend/src/components/ArmPanel.tsx`](../Frontend/src/components/ArmPanel.tsx).

Mesmo padrão visual do `PgmPanel`: um cartão por partição (1 a 16, ocultando as desabilitadas),
com o estado atual (Desarmada/Armada/ArmadaStay/EmDisparo, colorido) e quatro botões — **Armar**,
**Desarmar**, **Stay**, **Away** — cada um desabilitado quando `status.particoes[i].permiteArmar`/
`permiteDesarmar`/`permiteArmarStay`/`permiteArmarAway` vier `false` na última consulta de status.
Um cartão extra especial, **"Eletrificador"**, opera a partição `99` (Armar/Desarmar/Away — sem
Stay, que não existe para o eletrificador). Mesmo fluxo de confirmação do PGM: diálogo antes de
qualquer comando, spinner durante a execução, mensagem de sucesso/erro, recarrega o status ao
final. Chama `POST /api/centrais/{id}/particoes/{p}/{armar|desarmar|armar-stay|armar-away}`.

### 5.3 Painel de Zonas (ZonasPanel)

**Arquivo:** [`Frontend/src/components/ZonasPanel.tsx`](../Frontend/src/components/ZonasPanel.tsx).

Diferente de PGM/Arme (16 posições, cabem em cartões), uma central pode ter até 99 zonas — por
isso este painel não usa cartões, e sim os mesmos chips compactos que a tela já mostrava (um por
zona ativa, cor conforme o estado). A mudança: cada chip cuja zona tem `permiteInibir=true` agora
é **clicável** — clicar abre um diálogo de confirmação ("Inibir a zona 5?" ou "Desinibir a zona 5?",
decidido automaticamente pelo estado atual do chip) e, ao confirmar, chama
`POST /api/centrais/{id}/zonas/{zona}/inibir` ou `.../desinibir`. Zonas sem permissão de inibição
remota continuam como chips normais, não clicáveis.

## 6. Tela: Operação (legada)

**Arquivo:** [`Frontend/src/pages/OperationPage.tsx`](../Frontend/src/pages/OperationPage.tsx).
**Rota:** `/operacao`.

> ⚠️ **Não usar para operação real.** Esta tela chama `POST /api/operation/enviar`, que usa o
> `OperationService` legado — **sempre retorna sucesso simulado**, nunca fala de verdade com uma
> central. Existe só por compatibilidade com o MVP original. A operação real de PGM é feita pela
> Tela Central (seção 4/5).

## 7. Como o Frontend fala com o Backend (fluxo REST)

Toda comunicação usa **HTTP + JSON**, através da biblioteca **Axios**, configurada em
[`Frontend/src/services/api.ts`](../Frontend/src/services/api.ts):

```typescript
const api = axios.create({ baseURL: 'http://localhost:5000/api' })
```

Cada página importa esse `api` e faz chamadas como:

```typescript
const response = await api.get<CentralStatus>(`/centrais/${centralId}/status`)
```

Não existe estado global compartilhado (como Redux) — cada página gerencia seu próprio estado local
com `useState`/`useEffect`, buscando os dados de que precisa diretamente da API quando é montada
(e, em alguns casos, periodicamente — seção 8).

## 8. Como funciona a atualização automática

Não existe **push em tempo real** (WebSocket/SignalR) neste projeto ainda — ver
[`14_ROADMAP.md`](14_ROADMAP.md). A "atualização automática" é implementada por **polling**:
`setInterval` disparando uma nova requisição HTTP a cada X segundos.

Na `CentralDetailPage`:
```typescript
const idCentral = setInterval(carregarCentral, 15000)  // dados de cadastro, a cada 15s
const idStatus = setInterval(carregarStatus, 5000)     // status ao vivo, a cada 5s
const idRelogio = setInterval(() => forcarRenderizacao(n => n + 1), 1000)  // "tempo conectado" tique-taque visual
```

Isso significa que a informação na tela pode ter até 5 segundos de atraso em relação ao estado real
da central — aceitável para este caso de uso, mas é uma limitação consciente, documentada no
roadmap como candidata a melhoria futura.

## 9. Mapa de rotas

| Rota (Frontend) | Componente | Endpoints da API usados |
|---|---|---|
| `/` | `BuildingsPage` | `GET/POST/PUT/DELETE /api/building` |
| `/centrais` | `CentralsPage` | `GET/POST/PUT/DELETE /api/central` |
| `/centrais/:id` | `CentralDetailPage` + `PgmPanel` + `ArmPanel` + `ZonasPanel` + `components/session/*` | `GET /api/central/{id}`, `GET /api/centrais/{id}/status`, `GET /api/centrais/{id}/sessao`, `GET /api/centrais/{id}/log`, `GET /api/centrais/{id}/diagnostico`, `POST /api/centrais/{id}/reconectar`, `POST /api/centrais/{id}/pgm/{n}/{ligar\|desligar\|pulso}`, `POST /api/centrais/{id}/particoes/{p}/{armar\|desarmar\|armar-stay\|armar-away}`, `POST /api/centrais/{id}/zonas/{z}/{inibir\|desinibir}` |
| `/operacao` | `OperationPage` (legada) | `POST /api/operation/enviar` (legado), `GET /api/operation/historico` (legado) |

## 10. Casos de uso reais

**"Quero saber o status geral de todas as centrais de um prédio"** — hoje, é preciso entrar em
cada Central individualmente (não existe uma tela de "painel geral" ainda — ver
[`14_ROADMAP.md`](14_ROADMAP.md)).

**"Preciso acionar a PGM 5 por 2 segundos"** — na Tela Central, clicar "Pulso" na PGM 5, informar
`2000` no campo de duração, confirmar.

## 11. Boas práticas

- Sempre confiar no `estadoConfirmado` devolvido pela API depois de um comando de PGM, não assumir
  sucesso só pelo HTTP 200 vir sem erro (ver [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md)).
- Ao adicionar uma tela nova, seguir o padrão de `CentralDetailPage`: estado local com `useState`,
  busca de dados em `useEffect` com `useCallback`, tratamento de erro explícito por chamada.

## 12. Problemas comuns

- **"Cadê o botão 'Testar Conexão'?"** — foi removido de propósito (ver seção 3 e
  [`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md)); para validar se uma central
  real está operacional, use o card "Status da Conexão" na Tela Central.
- **"Cliquei em Reconectar e a central sumiu, isso é normal?"** — sim: "Reconectar" só limpa a
  sessão registrada (nunca abre uma conexão); a central deve reconectar sozinha em poucos segundos,
  seguindo o próprio ciclo de reconexão dela. Se ela nunca voltar, o problema está na configuração
  de rede da central (ActiveNet), não no CentralHub.
- **"A tela não atualiza sozinha"** — verificar se o `setInterval` está mesmo ativo (não foi
  cancelado por um erro de renderização) — abrir o console do navegador para checar erros JS.

## 13. Como testar

O Frontend não tem testes automatizados neste momento (ver
[`14_ROADMAP.md`](14_ROADMAP.md)) — a validação é feita via `npm run build` (garante que o
TypeScript compila sem erros) e testes manuais no navegador.

## 14. Como depurar

Ferramentas de desenvolvedor do navegador (F12): aba **Network** para ver cada requisição à API
(status HTTP, corpo da resposta); aba **Console** para erros JavaScript/React.

## 15. FAQ

**P: Por que existem duas telas diferentes de operação de PGM (Operação e Tela Central)?**
R: Histórico do projeto — a tela "Operação" é do MVP original (mock); a Tela Central foi construída
depois, já com a implementação real. Ver [`01_PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md).

**P: A interface funciona em celular?**
R: Usa Material UI com layout responsivo básico (`Grid`), mas não foi otimizada/testada
especificamente para telas pequenas.

## 16. Checklist

- [ ] Sei navegar até a Tela Central de uma Central específica.
- [ ] Sei explicar por que a tela "Operação" não deve ser usada para comandos reais.
- [ ] Sei explicar como a atualização automática funciona (polling, não push).
- [ ] Sei onde cada tela busca seus dados (quais endpoints).

---

**Documento anterior:** [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md)
**Próximo documento:** [`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
