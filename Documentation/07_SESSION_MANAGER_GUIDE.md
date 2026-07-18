# 07 — SESSION MANAGER GUIDE

> **Público-alvo:** um desenvolvedor que vai mexer na camada de sessão/rede do SDK, ou que
> precisa entender profundamente como o sistema lida com múltiplas centrais conectadas ao mesmo
> tempo. Este é o documento mais técnico da documentação — assume que você já leu
> [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md) e
> [`04_PROTOCOL_FLOW.md`](04_PROTOCOL_FLOW.md).

---

## Índice

1. [O que é o SessionManager, em uma frase](#1-o-que-é-o-sessionmanager-em-uma-frase)
2. [Como nasce uma sessão](#2-como-nasce-uma-sessão)
3. [Como uma sessão é registrada](#3-como-uma-sessão-é-registrada)
4. [Como o SessionManager encontra uma central](#4-como-o-sessionmanager-encontra-uma-central)
5. [Como uma sessão morre](#5-como-uma-sessão-morre)
6. [O que acontece numa reconexão (substituição de sessão)](#6-o-que-acontece-numa-reconexão-substituição-de-sessão)
7. [Os três eventos do SessionManager](#7-os-três-eventos-do-sessionmanager)
8. [Como funciona o SendAndWaitAsync — o coração do sistema](#8-como-funciona-o-sendandwaitasync--o-coração-do-sistema)
9. [Como funciona a correlação por SEQ](#9-como-funciona-a-correlação-por-seq)
10. [Como funciona o timeout](#10-como-funciona-o-timeout)
11. [Como funciona a concorrência](#11-como-funciona-a-concorrência)
12. [Diagrama de estados de uma sessão](#12-diagrama-de-estados-de-uma-sessão)
13. [Diagrama de sequência completo (pedido + resposta correlacionada)](#13-diagrama-de-sequência-completo-pedido--resposta-correlacionada)
14. [Casos de uso reais](#14-casos-de-uso-reais)
15. [Boas práticas](#15-boas-práticas)
16. [Problemas comuns](#16-problemas-comuns)
17. [Como testar](#17-como-testar)
18. [Como depurar](#18-como-depurar)
19. [FAQ](#19-faq)
20. [Checklist](#20-checklist)

---

## 1. O que é o SessionManager, em uma frase

O `SessionManager` (arquivo
[`SessionManager.cs`](../SDK/CentralHub.SDK/Jfl/Server/SessionManager.cs)) é um **catálogo em
memória** (não no banco de dados) de todas as centrais atualmente conectadas, permitindo que
qualquer parte do sistema pergunte "existe uma sessão viva para o número de série X?" e, se sim,
obtenha o objeto que representa essa conexão para poder enviar comandos nela.

## 2. Como nasce uma sessão

Uma sessão (objeto `JflSession`) nasce fisicamente no momento em que o `JflTcpServer` aceita uma
conexão TCP nova (`TcpListener.AcceptTcpClientAsync()`), através de
`JflSession.FromTcpClient(client)`. Nesse momento, porém, **ela ainda não está no
`SessionManager`** — ela existe apenas como um objeto solto, em estado `Conectando`, porque ainda
não se sabe qual central é essa (o handshake 0x21 ainda não chegou).

## 3. Como uma sessão é registrada

Só depois que o comando `0x21` é recebido e decodificado, e o `NumeroSerie` é conhecido, o
`ConnectionCommandHandler` chama:

```csharp
_sessionManager.Registrar(session);
```

É só a partir desse momento que a sessão passa a existir do ponto de vista de qualquer outro
componente do sistema (a API, por exemplo, nunca vê sessões em estado `Conectando` — só vê as que
já passaram pelo handshake completo).

Internamente, `SessionManager` guarda as sessões num
[`ConcurrentDictionary<string, JflSession>`](#11-como-funciona-a-concorrência), indexado pelo
`NumeroSerie`.

## 4. Como o SessionManager encontra uma central

Qualquer código (tipicamente um Service do SDK, como `CentralStatusQueryService` ou
`PgmCommandService`) que precise falar com uma central específica faz:

```csharp
if (!_sessionManager.TryGet(numeroSerie, out var session) || session is null)
{
    // não há sessão ativa — a central está offline
}
```

`TryGet` é uma busca direta no dicionário por chave — extremamente rápida (não percorre uma lista),
e segura para ser chamada concorrentemente de várias threads ao mesmo tempo (propriedade de
`ConcurrentDictionary`).

## 5. Como uma sessão morre

Uma sessão pode morrer por três motivos:

1. **A central fecha a conexão** (voluntariamente, ou porque perdeu energia/rede) — o
   `session.ReceiveAsync()` (dentro do laço de leitura em `JflTcpServer.HandleClientAsync`) retorna
   `null`, sinalizando fim de stream.
2. **Ocorre um erro de I/O** (rede caiu abruptamente) — uma `IOException` é lançada e capturada.
3. **O próprio servidor está sendo desligado** — o `CancellationToken` é cancelado, e o laço de
   leitura para.

Em qualquer um dos três casos, o bloco `finally` de `HandleClientAsync` executa:

```csharp
_sessionManager.Remover(session);
session.Close();
```

`Remover` tira a sessão do dicionário (só se ela ainda for a sessão "atual" para aquele número de
série — ver seção 6) e dispara o evento `SessaoRemovida`. `session.Close()` libera os recursos de
rede (fecha o `Stream`/`TcpClient`) e **cancela qualquer comando que estivesse pendente de resposta
naquela sessão** (lançando uma `IOException` para quem estava esperando, ao invés de deixar a
espera pendurada para sempre).

## 6. O que acontece numa reconexão (substituição de sessão)

Cenário real, observado durante a homologação: a central perde energia, volta, e reconecta — tudo
isso pode acontecer **antes** do CentralHub perceber que a sessão antiga morreu (por exemplo, se a
sessão antiga ainda não teve uma falha de keep-alive detectada, mas a central já abriu uma conexão
nova). Nesse caso, `SessionManager.Registrar` detecta que já existe uma sessão diferente com o
mesmo `NumeroSerie`, e:

```csharp
if (_sessoesPorNumeroSerie.TryGetValue(session.NumeroSerie, out var existente) && existente.Id != session.Id)
{
    existente.Close();      // fecha a sessão antiga imediatamente
    Remover(existente);      // remove do catálogo e dispara SessaoRemovida para ela
}

_sessoesPorNumeroSerie[session.NumeroSerie] = session;   // registra a nova
SessaoRegistrada?.Invoke(session);
```

Ou seja: **nunca existem duas sessões vivas para o mesmo número de série ao mesmo tempo** — a nova
sempre "vence" e a antiga é derrubada de propósito.

## 7. Os três eventos do SessionManager

```csharp
public event Action<JflSession>? SessaoRegistrada;
public event Action<JflSession>? SessaoRemovida;
public event Action<JflSession>? AtividadeAtualizada;
```

| Evento | Quando dispara | Quem escuta hoje |
|---|---|---|
| `SessaoRegistrada` | Handshake concluído com sucesso | `JflSessionPersistenceService` (Backend) — grava `CentralSession` nova, atualiza `Central.Status = "Online"` |
| `SessaoRemovida` | Sessão morreu (qualquer motivo) | `JflSessionPersistenceService` — marca `CentralSession.Status = Desconectada`, `Central.Status = "Offline"` |
| `AtividadeAtualizada` | Um keep-alive real (0x40) foi processado | `JflSessionPersistenceService` — atualiza `UltimoKeepAliveEmUtc`/`UltimoIpConectado` |

Esse é um padrão de projeto chamado **observador (observer)**: o SDK não sabe nada sobre banco de
dados — ele só "avisa" que algo aconteceu, e quem quiser reagir (o Backend, neste caso) se
inscreve. Isso mantém o SDK livre de dependências de EF Core/SQLite (ver
[`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md), seção 1).

## 8. Como funciona o SendAndWaitAsync — o coração do sistema

Este é o mecanismo mais sofisticado (e mais importante de entender) de todo o projeto, porque é
ele que permite ao CentralHub **enviar um comando e aguardar a resposta específica daquele
comando**, mesmo que outras mensagens (keep-alives, eventos) cheguem no meio do caminho.

Localização: [`JflSession.cs`](../SDK/CentralHub.SDK/Jfl/Server/JflSession.cs), método
`SendAndWaitAsync`.

```csharp
public async Task<JflPacket> SendAndWaitAsync(byte cmd, ReadOnlyMemory<byte> dados, TimeSpan timeout, CancellationToken cancellationToken)
{
    var seq = NextSeq();
    var tcs = new TaskCompletionSource<JflPacket>(TaskCreationOptions.RunContinuationsAsynchronously);

    _requisicoesPendentes.TryAdd(seq, tcs);

    try
    {
        await SendAsync(seq, cmd, dados, cancellationToken);   // 1. envia o comando

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var registro = linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token));

        return await tcs.Task;    // 2. espera a "promessa" ser cumprida (ou dar timeout)
    }
    finally
    {
        _requisicoesPendentes.TryRemove(seq, out _);   // 3. limpa, aconteça o que acontecer
    }
}
```

**Explicando cada peça para quem nunca viu isso antes:**

- **`TaskCompletionSource<JflPacket>`**: é como uma "caixa vazia" que representa "algo vai
  acontecer no futuro, ainda não sei o quê". Quem chama `SendAndWaitAsync` fica esperando essa
  caixa ser preenchida. Quem preenche (`tcs.TrySetResult(...)`) é outra parte do código,
  completamente diferente, rodando em outro momento (quando a resposta chega pela rede).
- **`_requisicoesPendentes`**: um dicionário (`ConcurrentDictionary<byte, TaskCompletionSource<JflPacket>>`)
  que guarda, para cada `SEQ` que enviamos e ainda não recebemos resposta, a "caixa" correspondente.
- **`CancellationTokenSource(timeout)`**: um mecanismo do .NET para "desistir depois de X tempo" —
  se a caixa não for preenchida dentro do prazo, a espera é cancelada automaticamente.

## 9. Como funciona a correlação por SEQ

O outro lado da moeda de `SendAndWaitAsync` é `TryCompletePendingRequest`, chamado pelo laço de
leitura de `JflTcpServer` **antes** de qualquer pacote ser passado ao dispatcher de comandos
normal:

```csharp
public bool TryCompletePendingRequest(JflPacket packet)
{
    if (_requisicoesPendentes.TryRemove(packet.Seq, out var tcs))
    {
        return tcs.TrySetResult(packet);   // "preenche a caixa" — SendAndWaitAsync acorda aqui
    }
    return false;   // não era resposta de nada que estávamos esperando
}
```

```
        session.SendAndWaitAsync(0x4D, ...)              JflTcpServer.HandleClientAsync
              │                                                    │ (laço de leitura contínuo)
              │ 1. seq = NextSeq() = 0x07                          │
              │ 2. guarda TaskCompletionSource em                  │
              │    _requisicoesPendentes[0x07]                     │
              │ 3. envia pacote CMD=0x4D SEQ=0x07 ─────────────────►│  (sai pelo socket)
              │ 4. await tcs.Task  (BLOQUEADO aqui)                 │
              │                                                    │
              │         ... a central processa e responde ...       │
              │                                                    │
              │                              pacote chega, SEQ=0x07◄┤
              │                                                    │
              │◄──── TryCompletePendingRequest(pacote) ─────────────┤
              │      acha 0x07 em _requisicoesPendentes,            │
              │      chama tcs.TrySetResult(pacote)                 │
              │                                                    │
              │ 5. await tcs.Task DESBLOQUEIA, devolve o pacote     │  (loop continua para o
              ▼                                                    ▼   próximo pacote)
      pacote decodificado e devolvido                      (não passa pelo dispatcher —
      para quem chamou SendAndWaitAsync                      já foi "consumido" aqui)
```

Se um pacote chegar com um `SEQ` que **não** está em `_requisicoesPendentes` (por exemplo, um
evento espontâneo, ou um keep-alive), `TryCompletePendingRequest` devolve `false`, e o pacote segue
normalmente para o `JflCommandDispatcher` (que decide, pelo `CMD`, qual handler deve tratá-lo).

## 10. Como funciona o timeout

Cada chamada a `SendAndWaitAsync` recebe um `TimeSpan timeout` (por padrão, 10 segundos, tanto em
`CentralStatusQueryService` quanto em `PgmCommandService`). Se a resposta não chegar dentro desse
prazo, a "caixa" (`TaskCompletionSource`) é cancelada automaticamente pelo `CancellationTokenSource`
interno, e uma `OperationCanceledException` é lançada de volta para quem chamou — que a captura e
traduz para um resultado de negócio (`PgmCommandFailureReason.Timeout`,
`CentralStatusQueryFailureReason.Timeout`), nunca deixando essa exceção "vazar" crua até a API.

## 11. Como funciona a concorrência

Duas estruturas de dados no código são escolhidas especificamente para serem seguras quando
acessadas por múltiplas threads simultaneamente (várias centrais conectadas ao mesmo tempo, cada
uma processada por sua própria tarefa assíncrona):

- `SessionManager` usa `ConcurrentDictionary<string, JflSession>` — permite que a thread A registre
  uma sessão enquanto a thread B consulta outra, sem interferência ou necessidade de travas
  (`lock`) manuais.
- `JflSession` usa `ConcurrentDictionary<byte, TaskCompletionSource<JflPacket>>` para
  `_requisicoesPendentes`, pelo mesmo motivo: dentro de uma mesma sessão, poderiam existir vários
  comandos pendentes simultâneos (ex.: um pedido de Status e um comando de PGM disparados quase ao
  mesmo tempo).

Além disso, `JflSession.SendAsync` usa um `SemaphoreSlim` (uma tranca que só deixa um "escritor" no
socket por vez):

```csharp
await _travaEscrita.WaitAsync(cancellationToken);
try { await _stream.WriteAsync(pacote, cancellationToken); }
finally { _travaEscrita.Release(); }
```

Isso evita que dois comandos enviados quase ao mesmo tempo tenham seus bytes **intercalados** no
mesmo socket (o que corromperia os dois pacotes) — cada `WriteAsync` completa inteiro antes do
próximo começar.

## 12. Diagrama de estados de uma sessão

```
        ┌─────────────┐
        │  Conectando   │   (TCP aceito, aguardando o handshake 0x21)
        └──────┬───────┘
               │ 0x21 processado com sucesso
               ▼
        ┌─────────────┐
        │    Ativa      │   (registrada no SessionManager, pronta para
        └──────┬───────┘    receber/enviar comandos)
               │ conexão cai (qualquer motivo) OU servidor desliga
               ▼
        ┌─────────────┐
        │  Encerrada    │   (removida do SessionManager, recursos liberados)
        └─────────────┘
```

## 13. Diagrama de sequência completo (pedido + resposta correlacionada)

Já mostrado na Fase 10 de [`04_PROTOCOL_FLOW.md`](04_PROTOCOL_FLOW.md) — este documento detalha o
*mecanismo interno* daquele fluxo.

## 14. Casos de uso reais

- Duas centrais diferentes conectadas simultaneamente, cada uma com sua própria entrada no
  `SessionManager`, totalmente independentes uma da outra.
- Um operador pedindo Status de uma central bem no momento em que ela está mandando um keep-alive
  — os dois pacotes (o keep-alive espontâneo e a resposta ao Status) são corretamente separados
  pelo mecanismo de SEQ, mesmo chegando muito próximos no tempo.

## 15. Boas práticas

- Sempre que implementar um novo comando "servidor pergunta, central responde" (ver
  [`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md)), use `SendAndWaitAsync` — nunca
  implemente um mecanismo de espera manual paralelo.
- Sempre passe um `CancellationToken` de verdade (vindo da requisição HTTP, por exemplo) para que,
  se o operador cancelar a operação no navegador, o comando pendente também seja cancelado, não
  fique "esquecido" na sessão.

## 16. Problemas comuns

- **"Meu comando novo nunca recebe resposta"** — verifique se o handler que trataria aquele `CMD`
  como comando *espontâneo* (um stub, por exemplo) não está "roubando" o pacote antes da
  correlação por SEQ acontecer — na prática isso não deveria ocorrer, porque
  `TryCompletePendingRequest` sempre roda primeiro, mas é a primeira coisa a verificar.
- **"Timeout constante mesmo com a central online"** — pode ser que a central simplesmente não
  suporte aquele comando específico (retorna outra coisa, ou nada) — comparar com o manual oficial
  em [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md).

## 17. Como testar

`SDK/CentralHub.SDK.Tests/Server/JflSessionSendAndWaitTests.cs` testa o mecanismo isoladamente
(sem sockets reais, usando um `DuplexMemoryStream`). `SDK/CentralHub.SDK.Tests/Server/SessionManagerTests.cs`
testa registro/remoção/substituição de sessões.

## 18. Como depurar

Toda transição de estado do `SessionManager` é logada (`Sessao registrada`, `Sessao removida`,
`... reconectou ... encerrando sessao anterior`) — acompanhar esses logs é o jeito mais rápido de
entender o que está acontecendo com uma central específica em produção.

## 19. FAQ

**P: Por que não usar `lock` (trava manual) em vez de `ConcurrentDictionary`?**
R: `ConcurrentDictionary` é mais eficiente para o padrão de acesso deste projeto (muitas leituras,
poucas escritas concorrentes) e evita erros comuns de esquecer de liberar uma trava.

**P: O que acontece se eu chamar `SendAndWaitAsync` duas vezes seguidas, rapidamente, na mesma
sessão?**
R: Cada chamada gera um `SEQ` novo (`NextSeq()` incrementa sempre), então as duas coexistem sem
conflito — a central deveria responder às duas, cada resposta sendo casada com o pedido certo.

## 20. Checklist

- [ ] Sei explicar, sem olhar o código, o que `SendAndWaitAsync` faz.
- [ ] Sei explicar o que acontece quando duas sessões do mesmo número de série colidem.
- [ ] Sei quais das três estruturas de dados usam `ConcurrentDictionary` e por quê.
- [ ] Sei onde o timeout é configurado e o que acontece quando ele estoura.

---

**Documento anterior:** [`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md)
**Próximo documento:** [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
