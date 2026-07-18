# ARQUITETURA_SESSION_MANAGER — Da discagem de saída ao painel de monitoramento de sessão

> **Público-alvo deste documento:** qualquer pessoa que precise entender por que a tela "Centrais"
> não tem mais um botão "Testar Conexão", por que os campos IP/Porta/Usuário/Senha sumiram do
> formulário de cadastro, e como a tela hoje sabe se uma central está online sem nunca abrir uma
> conexão de saída. Complementa [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md),
> [`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md) e [`09_WEB_GUIDE.md`](09_WEB_GUIDE.md).

---

## Índice

1. [Resumo em uma frase](#1-resumo-em-uma-frase)
2. [A arquitetura antiga (discagem de saída)](#2-a-arquitetura-antiga-discagem-de-saída)
3. [Os problemas que a arquitetura antiga causava](#3-os-problemas-que-a-arquitetura-antiga-causava)
4. [A arquitetura real (TcpListener + SessionManager)](#4-a-arquitetura-real-tcplistener--sessionmanager)
5. [O que foi removido, o que foi marcado como obsoleto, o que é novo](#5-o-que-foi-removido-o-que-foi-marcado-como-obsoleto-o-que-é-novo)
6. [Fluxogramas](#6-fluxogramas)
7. [Referência dos novos endpoints](#7-referência-dos-novos-endpoints)
8. [Exemplos reais de payload](#8-exemplos-reais-de-payload)
9. [A tela "Centrais" e a Tela Central, antes e depois](#9-a-tela-centrais-e-a-tela-central-antes-e-depois)
10. [FAQ](#10-faq)
11. [Checklist de entendimento](#11-checklist-de-entendimento)

---

## 1. Resumo em uma frase

A tela "Centrais" e a API por trás dela **paravam de refletir a arquitetura real do projeto** desde
que o `SessionManager`/`JflTcpServer` foram construídos (ver histórico completo em
[`01_PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md), seção 4): a tela continuava tentando **discar
para fora** (abrir um `TcpClient` para o IP/Porta cadastrados) para "testar" uma central, mesmo
sabendo que a central real nunca escuta como servidor — o modelo correto é o oposto, **a central
disca para o CentralHub**. Este documento explica a limpeza que corrigiu isso: removeu o teste de
conexão de saída e substituiu a tela por um painel que só **consulta** a sessão TCP real, sem nunca
abrir um socket.

## 2. A arquitetura antiga (discagem de saída)

```
┌─────────────────────┐                         ┌──────────────────────────┐
│   Backend/API        │  TCP de SAÍDA           │   Central de alarme       │
│   (CentralHub)        │ ───────────────────▶   │   (IP/Porta cadastrados   │
│                        │  "Testar Conexão"       │    manualmente)           │
│   ConnectionService    │  discava para IP:Porta   │                          │
│   AdapterFactory        │  do cadastro no banco    │   Nunca escuta nessa    │
│   JflAdapter             │                          │   porta como servidor  │
└─────────────────────┘                         └──────────────────────────┘
```

Nesse modelo, cadastrar uma central exigia: Nome, **IP**, **Porta**, **Usuário**, **Senha** — e o
formulário só liberava o botão "Salvar" depois que "Testar Conexão" retornasse sucesso. O botão
chamava `POST /api/central/testar-conexao`, que usava `ConnectionService` →
`AdapterFactory.DetectarEConectar` → `TcpConnectionHelper.TestarConexao`, todos no pacote
`SDK/CentralHub.SDK/Adapters/` — um `TcpClient` de saída de verdade, no mesmo modelo que um
navegador abre uma conexão para um site.

## 3. Os problemas que a arquitetura antiga causava

- **O teste nunca podia funcionar contra uma central real.** O protocolo oficial da JFL define que
  é a central quem disca para fora, contra o endereço configurado nela via ActiveNet — não o
  contrário. Um `TcpClient` de saída do Backend não tinha ninguém do outro lado para aceitar.
- **Confusão direta com a arquitetura real.** Uma central podia estar com uma sessão TCP **ativa e
  saudável** no `SessionManager` (handshake feito, keep-alive em dia) e mesmo assim "falhar" no
  teste de conexão — porque o teste testava uma coisa que não tem relação com o que realmente
  importa (a sessão real).
- **Cadastro bloqueado por um requisito artificial.** Salvar uma central exigia sucesso no teste,
  então na prática era preciso digitar um IP/Porta que respondesse a *alguma coisa* (mesmo que
  fosse a própria porta do `JflTcpServer`, respondendo com um handshake que o teste nem sabia
  interpretar como sucesso) só para conseguir cadastrar.
- **Nenhuma visibilidade real da sessão.** Mesmo com o `SessionManager` guardando tudo que importa
  (IP real observado, handshake, keep-alive, SEQ, últimos comandos), a tela não mostrava nada disso
  — só o resultado artificial do teste de conexão de saída.

## 4. A arquitetura real (TcpListener + SessionManager)

```
┌──────────────────────┐   TCP de SAÍDA (a central é o CLIENTE)   ┌────────────────────────────┐
│  Central de alarme     │ ────────────────────────────────────▶  │  JflTcpServer (TcpListener)  │
│  JFL Active 100 Bus    │   Ela disca para o IP/porta configu-    │  Fica ESCUTANDO conexões     │
│  (configurada via      │   rados nela mesma via ActiveNet.        │  de entrada, porta 8085.     │
│   ActiveNet)            │                                          │                             │
└──────────────────────┘                                          └──────────────┬─────────────┘
                                                                                    │
                                                                                    ▼
                                                                    ┌────────────────────────────┐
                                                                    │  SessionManager              │
                                                                    │  Guarda a sessão viva,        │
                                                                    │  indexada por NumeroSerie.    │
                                                                    └──────────────┬─────────────┘
                                                                                    │ só leitura
                                                                                    ▼
                                                        ┌───────────────────────────────────────┐
                                                        │  CentralSessionService (NOVO)            │
                                                        │  Monta o snapshot da sessão, o log de    │
                                                        │  atividade e o diagnóstico — NUNCA abre  │
                                                        │  conexão nenhuma, só consulta.            │
                                                        └───────────────────┬───────────────────┘
                                                                             ▼
                                                        GET  /api/centrais/{id}/sessao
                                                        GET  /api/centrais/{id}/log
                                                        GET  /api/centrais/{id}/diagnostico
                                                        POST /api/centrais/{id}/reconectar
                                                                             │
                                                                             ▼
                                                            ┌──────────────────────────────┐
                                                            │  Tela Central (Frontend)        │
                                                            │  Status da Conexão, Sessão TCP, │
                                                            │  Log da Central, Diagnóstico     │
                                                            └──────────────────────────────┘
```

A peça nova é o `CentralSessionService`: ele nunca fala com a central diretamente — só lê o que o
`SessionManager` (SDK, não alterado) já sabe, mais um log de atividade capturado dos logs
estruturados que o SDK já emite hoje (ver seção 5). **Handshake, KeepAlive, Parser, Builder, PGM,
Status, `SessionManager`, `JflSession`, `Protocol` e os `Handlers` não foram tocados** — a mudança
inteira aconteceu na camada Backend (Controllers/Services/DTOs/Logging) e Frontend.

### 4.1 De onde vêm os dados que a tela mostra

| Dado pedido | De onde vem |
|---|---|
| Status (Online/Offline), IP da sessão, Porta Remota, Data/Hora da conexão, Handshake, Modelo, Firmware, MAC, IMEI | `SessionManager.TryGet(numeroSerie)` → `JflSession` (propriedades públicas já existentes) |
| Último KeepAlive, Último IP conectado (histórico) | Colunas já existentes em `Central` (gravadas por `JflSessionPersistenceService`, que já existia) |
| SEQ atual, bytes recebidos/enviados, último comando, latência, último erro | **Novo:** `SessionActivityLogService`, que captura os logs estruturados que `JflTcpServer`/`PgmCommandService`/`CentralStatusQueryService` **já emitiam** (nível Debug/Information), via um `ILoggerProvider` customizado — nenhum arquivo do SDK foi alterado para isso |
| Log da Central (lista de eventos) | Mesmo `SessionActivityLogService`, exposto via `GET .../log` |
| Diagnóstico (checklist) | `CentralSessionService.ObterDiagnosticoAsync`, combinando os dados acima |

## 5. O que foi removido, o que foi marcado como obsoleto, o que é novo

### Removido de verdade (não existe mais)

- `Backend/CentralHub.Api/Services/ConnectionService.cs`
- A action `TestarConexao` (`POST /api/central/testar-conexao`) em `CentralController.cs`
- `TestarConexaoDto`/`ConexaoResultDto`
- O botão "Testar Conexão" e os campos IP/Porta/Usuário/Senha do formulário visível em
  `CentralsPage.tsx`
- O tipo `ConexaoResult` no Frontend

### Marcado `[Obsolete]` (continua existindo, comportamento idêntico)

`SDK/CentralHub.SDK/Adapters/TcpConnectionHelper.cs`, `JflAdapter.cs`, `IntelbrasAdapter.cs`,
`AdapterFactory.cs` — só os métodos de conexão de saída (`TestarConexao`,
`VerificarConectividade`, `DetectarEConectar`) ganharam `[Obsolete]`; `AcionarPGM`/`DesligarPGM`/
`PulsoPGM`/`Criar`/`ResolverPorNome` continuam sem anotação porque ainda alimentam
`OperationService` (a tela "Operação" legada, fora do escopo desta limpeza — ver
[`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md), seção 6). Não é dead code: remover exigiria
reescrever `OperationService` primeiro.

### Novo

- `CentralSessionService` + `SessaoDtos.cs` (Backend)
- `SessionActivityLogService` + `SdkActivityLogger`/`SdkActivityLoggerProvider` (Backend, captura de
  log)
- 4 endpoints novos: `GET .../sessao`, `GET .../log`, `POST .../reconectar`, `GET .../diagnostico`
- Campo `NumeroSerie` no formulário de cadastro (Frontend)
- `components/session/*` (Frontend): `StatusConexaoCard`, `SessaoTcpPanel`, `IndicadoresChips`,
  `LogCentralPanel`, `DetalhesSessaoModal`, `DiagnosticoPanel`
- `Backend/CentralHub.Api.Tests` (primeiro projeto de testes do Backend)

## 6. Fluxogramas

### 6.1 Ciclo de vida de uma sessão (visto pela tela)

```
1. Central liga/reconecta → disca para o CentralHub (IP:8085 configurado nela via ActiveNet).
2. JflTcpServer aceita a conexão → handshake (0x21) → SessionManager registra a sessão.
3. Tela Central (polling a cada 5s) chama GET /sessao → StatusConexao = "Online".
4. Central manda KeepAlive (0x40) periodicamente → UltimoKeepAliveEmUtc atualiza.
5. Operador clica "Solicitar Status" (ou o polling de 5s do bloco de Status dispara sozinho)
   → GET /api/centrais/{id}/status → 0x4D real na sessão → partições/zonas/PGMs atualizam
   na tela SEM recarregar a página.
6. Operador clica "Ligar PGM 3" → confirma → POST /pgm/3/ligar → 0x50 real na sessão.
7. Central cai (energia, rede) ou operador clica "Reconectar":
   a. Se caiu sozinha: SessionManager detecta e remove a sessão → próximo GET /sessao
      já mostra "Offline".
   b. Se foi "Reconectar": POST /reconectar fecha e remove a sessão registrada
      (sem abrir nada) → central detecta a queda e reconecta sozinha, do zero,
      caindo de novo no passo 1.
```

### 6.2 O que "Reconectar" faz, de verdade

```
Operador clica "Reconectar"
        │
        ▼
Diálogo de confirmação explica: "isso NÃO abre conexão nenhuma"
        │
        ▼ (confirma)
POST /api/centrais/{id}/reconectar
        │
        ▼
CentralSessionService.ReconectarAsync:
  SessionManager.TryGet(numeroSerie) → achou sessão?
        │                                   │
       não                                 sim
        │                                   │
        ▼                                   ▼
  devolve SessaoEncontrada=false    session.Close() + sessionManager.Remover(session)
  ("a central já não tinha           (nenhum TcpClient é aberto em NENHUM momento
   sessão ativa")                     deste fluxo)
        │                                   │
        └───────────────┬───────────────────┘
                         ▼
        Frontend mostra a mensagem devolvida pela API e entra em
        "Aguardando conexão" (🟡) por alguns segundos, até o próximo
        polling confirmar Online/Offline de verdade.
```

## 7. Referência dos novos endpoints

| Método e rota | O que faz | Nunca faz |
|---|---|---|
| `GET ~/api/centrais/{id}/sessao` | Snapshot completo (Status da Conexão + Sessão TCP + Detalhes) | Abrir conexão |
| `GET ~/api/centrais/{id}/log?max=N` | Últimas N entradas do log de atividade, mais recente primeiro | Abrir conexão |
| `POST ~/api/centrais/{id}/reconectar` | Fecha e remove a sessão registrada no `SessionManager` | Abrir conexão |
| `GET ~/api/centrais/{id}/diagnostico` | Checklist (sessão ativa, handshake, keep-alive, cadastro, vínculo com Prédio...) | Abrir conexão |

Todas as quatro seguem o mesmo padrão de rota absoluta (`~/api/centrais/...`) já usado por
`GET .../status` e `POST .../pgm/{pgm}/{ligar\|desligar\|pulso}` — ver
[`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md), seção 3.1.

## 8. Exemplos reais de payload

**`GET /api/centrais/5/sessao` — central online:**
```json
{
  "centralId": 5,
  "statusConexao": "Online",
  "numeroSerie": "3000000123",
  "modelo": "Active 100 Bus",
  "firmware": "6.5",
  "ipSessao": "192.168.1.50",
  "portaRemota": 51422,
  "dataHoraConexaoUtc": "2026-07-17T13:02:11Z",
  "ultimoPacoteRecebidoEmUtc": "2026-07-17T13:06:41Z",
  "socketConectado": true,
  "handshakeRealizado": true,
  "keepAliveAtivo": true,
  "ultimoKeepAliveEmUtc": "2026-07-17T13:06:11Z",
  "ultimoComando": "Consulta de Status (0x4D)",
  "ultimoSeq": 7,
  "bytesRecebidos": 42,
  "bytesEnviados": 12,
  "latenciaMs": 38.4,
  "sessaoAtiva": true,
  "centralCadastrada": true,
  "numeroSerieDivergente": false
}
```

**`GET /api/centrais/5/sessao` — central offline:**
```json
{
  "centralId": 5,
  "statusConexao": "Offline",
  "numeroSerie": "3000000123",
  "socketConectado": false,
  "handshakeRealizado": false,
  "keepAliveAtivo": false,
  "ultimoKeepAliveEmUtc": "2026-07-17T11:40:02Z",
  "ultimoIpConectado": "192.168.1.50",
  "sessaoAtiva": false,
  "centralCadastrada": true,
  "numeroSerieDivergente": false
}
```

**`POST /api/centrais/5/reconectar` — resposta:**
```json
{ "sessaoEncontrada": true, "mensagem": "A central deverá iniciar uma nova conexão automaticamente." }
```

**`GET /api/centrais/5/diagnostico` — trecho:**
```json
{
  "centralId": 5,
  "itens": [
    { "descricao": "Sessão ativa", "ok": true },
    { "descricao": "Handshake realizado", "ok": true },
    { "descricao": "KeepAlive dentro do prazo", "ok": true },
    { "descricao": "Número de Série cadastrado", "ok": true },
    { "descricao": "Central vinculada a um Prédio", "ok": true }
  ]
}
```

## 9. A tela "Centrais" e a Tela Central, antes e depois

| Antes | Depois |
|---|---|
| Formulário: Nome, IP, Porta, Usuário, Senha, Prédio | Formulário: Nome, **Número de Série**, Prédio |
| Botão "Testar Conexão" bloqueando o Salvar | Salvar exige só Nome + Prédio |
| Coluna "IP" na tabela | Coluna "Número de Série" na tabela |
| Bloco de identificação simples (Nome, Online/Offline, Número Série, Modelo, Firmware, IP Atual, Último KeepAlive, Tempo Conectado) | **Status da Conexão** (🟢/🔴/🟡 + todos os campos acima + Latência + Tempo desde o último KeepAlive + Sessão Ativa) com botão "Atualizar Status" |
| — | **Sessão TCP**: socket conectado, handshake, keep-alive ativo, último pacote, último comando, SEQ, último erro |
| — | **Diagnóstico**: checklist automático |
| — | **Log da Central**: lista rolável, atualização própria |
| — | Botões **Detalhes da Sessão** (modal) e **Reconectar** (nunca abre conexão) |

## 10. FAQ

**P: Por que não simplesmente "consertar" o teste de conexão em vez de removê-lo?**
R: Porque não existe uma versão "consertada" que faça sentido — o modelo de discagem de saída está
architeturalmente invertido em relação ao protocolo real (ver seção 3). Qualquer central real
sempre "falharia" nesse teste, então mantê-lo (mesmo corrigido) continuaria confundindo o operador.

**P: Cadastros antigos com IP/Porta/Usuário/Senha preenchidos vão perder esses dados?**
R: Não. As colunas continuam existindo no banco (nenhuma migração foi feita); só a API parou de
exigi-las e o formulário parou de mostrá-las. Ver [`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md).

**P: "Reconectar" desliga a central de verdade?**
R: Não. Ele só apaga o registro da sessão no `SessionManager` do lado do servidor — o equivalente
lógico a "esquecer" a sessão. A central continua com seu próprio socket TCP aberto até o próximo
keep-alive falhar ou ela decidir reconectar por conta própria (o comportamento real de reconexão é
inteiramente decidido pelo firmware da central, fora do controle do CentralHub).

**P: Por que o estado "Aguardando conexão" (🟡) só existe no Frontend?**
R: Porque o `SessionManager` só registra uma sessão **depois** que o handshake termina — não existe
hoje uma forma pública de observar "socket TCP aberto, handshake ainda não concluído" sem alterar o
SDK (fora do escopo desta limpeza). O Frontend mostra esse estado por alguns segundos, de forma
otimista, logo após o clique em "Reconectar", até o próximo polling confirmar Online ou Offline de
verdade.

**P: De onde vêm o SEQ, os bytes e a latência, se o `SessionManager` não expõe isso publicamente?**
R: De um `ILoggerProvider` customizado (`SdkActivityLoggerProvider`) que captura os logs
estruturados que o SDK **já emite hoje** (`_logger.LogInformation("...{Seq}...", valor)`) — sem
alterar nenhum arquivo do SDK. Ver seção 4.1.

**P: Isso quebra a tela "Operação" ou o fluxo de PGM da Tela Central?**
R: Não. `PgmService`/`PgmCommandService` (o caminho real de PGM) não foram tocados.
`OperationService`/`OperationPage` (a tela legada) continuam funcionando, porque
`AdapterFactory`/`JflAdapter`/`IntelbrasAdapter` só ganharam `[Obsolete]` — comportamento idêntico.

## 11. Checklist de entendimento

- [ ] Eu sei explicar por que o teste de conexão antigo nunca podia funcionar contra uma central real.
- [ ] Eu sei que "Reconectar" nunca abre uma conexão de saída — só limpa a sessão registrada.
- [ ] Eu sei de onde vêm os campos SEQ/bytes/latência mostrados na tela (captura de log, não o SessionManager).
- [ ] Eu sei que os campos legados IP/Porta/Usuário/Senha continuam no banco, só não aparecem mais na tela.
- [ ] Eu sei que Handshake/KeepAlive/Parser/Builder/PGM/Status/SessionManager/JflSession/Protocol/Handlers não foram alterados nesta mudança.

---

**Ver também:** [`01_PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md),
[`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md), [`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md),
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md), [`09_WEB_GUIDE.md`](09_WEB_GUIDE.md),
[`12_FAQ.md`](12_FAQ.md), [`14_ROADMAP.md`](14_ROADMAP.md),
[`Protocol/20_CHANGELOG.md`](Protocol/20_CHANGELOG.md) (entrada "Fase 0.9").
**Índice geral:** [`INDEX.md`](INDEX.md)
