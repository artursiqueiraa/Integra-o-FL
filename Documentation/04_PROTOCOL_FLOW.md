# 04 — PROTOCOL FLOW

> **Público-alvo:** alguém que quer entender, passo a passo, cronologicamente, tudo que acontece
> desde o momento em que uma central de alarme é ligada até o momento em que um operador vê o
> status dela numa tela e manda um comando de PGM. Este documento é o "roteiro completo" —
> complementa o [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md) (que explica *o que* cada
> comando significa) focando em *quando* cada coisa acontece, em que ordem, e o que dispara o quê.

---

## Índice

1. [Visão geral da linha do tempo](#1-visão-geral-da-linha-do-tempo)
2. [Fase 0 — Antes de tudo: pré-requisitos](#fase-0--antes-de-tudo-pré-requisitos)
3. [Fase 1 — Ligar a central](#fase-1--ligar-a-central)
4. [Fase 2 — Abrir o TCP](#fase-2--abrir-o-tcp)
5. [Fase 3 — Enviar 0x21 (handshake)](#fase-3--enviar-0x21-handshake)
6. [Fase 4 — Servidor recebe](#fase-4--servidor-recebe)
7. [Fase 5 — Parser](#fase-5--parser)
8. [Fase 6 — SessionManager](#fase-6--sessionmanager)
9. [Fase 7 — Resposta do handshake](#fase-7--resposta-do-handshake)
10. [Fase 8 — Ciclo de KeepAlive](#fase-8--ciclo-de-keepalive)
11. [Fase 9 — Eventos (não implementado)](#fase-9--eventos-não-implementado)
12. [Fase 10 — Consulta de Status, sob demanda](#fase-10--consulta-de-status-sob-demanda)
13. [Fase 11 — Comando de PGM, sob demanda](#fase-11--comando-de-pgm-sob-demanda)
14. [Fase 12 — Desconexão](#fase-12--desconexão)
15. [Fluxograma consolidado (todas as fases juntas)](#15-fluxograma-consolidado-todas-as-fases-juntas)
16. [Casos de uso reais](#16-casos-de-uso-reais)
17. [Boas práticas](#17-boas-práticas)
18. [Problemas comuns](#18-problemas-comuns)
19. [Como testar cada fase isoladamente](#19-como-testar-cada-fase-isoladamente)
20. [Como depurar cada fase](#20-como-depurar-cada-fase)
21. [FAQ](#21-faq)
22. [Checklist](#22-checklist)

---

## 1. Visão geral da linha do tempo

```
Central liga.
   ↓
Abre TCP.
   ↓
Envia 0x21.
   ↓
Servidor recebe.
   ↓
Parser.
   ↓
SessionManager.
   ↓
Resposta.
   ↓
KeepAlive (repete a cada N minutos, para sempre, enquanto conectada)
   ↓
Eventos (a qualquer momento, não implementado ainda)
   ↓
Status (sob demanda, quando alguém pede pela API)
   ↓
PGM (sob demanda, quando alguém pede pela API)
   ↓
(desconexão, eventualmente)
```

## Fase 0 — Antes de tudo: pré-requisitos

Antes de qualquer coisa acontecer na linha do tempo acima, duas coisas precisam já estar prontas:

1. **O CentralHub precisa já estar rodando**, com o `JflTcpServer` escutando na porta configurada
   (por padrão, 8085). Se o servidor não estiver de pé, a central vai tentar conectar e falhar
   silenciosamente (do ponto de vista dela) — ela vai tentar de novo mais tarde, mas nada
   acontece do lado do CentralHub porque não há processo nenhum escutando.
2. **A central precisa já estar configurada** (via ActiveNet) com o IP e a porta corretos do
   CentralHub, e com o "Reporte via rede Ethernet/Wi-Fi" habilitado. Sem isso, a central nunca vai
   sequer tentar conectar — ela simplesmente não sabe para onde ligar.

## Fase 1 — Ligar a central

A central de alarme é ligada (ou reiniciada, ou simplesmente já estava ligada e o módulo de rede
decide, sozinho, iniciar/renovar sua tentativa de conexão). Este é um evento **inteiramente
interno à central** — o CentralHub não participa dele e não tem visibilidade sobre ele até a
próxima fase.

## Fase 2 — Abrir o TCP

O módulo Ethernet da central inicia uma conexão TCP de saída para o IP e porta configurados. Esta
é a etapa "TCP" explicada em detalhe em
[`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md). Do lado do CentralHub, o `TcpListener`
dentro do `JflTcpServer` estava bloqueado, esperando, dentro de um laço, na chamada:

```csharp
client = await _listener.AcceptTcpClientAsync(cancellationToken);
```

Assim que o pacote de abertura de conexão (SYN) chega e o sistema operacional completa o
"aperto de mão" de nível TCP (que é uma coisa diferente do "handshake" do protocolo JFL — aqui
estamos falando só da camada de transporte), essa chamada retorna com um objeto `TcpClient`
representando a conexão recém-aberta.

**No log, isso aparece como:**
```
info: CentralHub.SDK.Jfl.Server.JflTcpServer[0]
      Conexao TCP aceita: IP remoto=10.0.250.21 Porta remota=64883
```

## Fase 3 — Enviar 0x21 (handshake)

Assim que a conexão TCP está aberta, **a central, e só a central, toma a iniciativa** de enviar o
primeiro pacote — o comando `0x21`, explicado byte a byte em
[`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md) e
[`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md). O CentralHub, neste momento, ainda não sabe *qual*
central é essa — só sabe que "alguém" conectou, de um IP específico.

## Fase 4 — Servidor recebe

Do lado do CentralHub, uma tarefa assíncrona dedicada àquela conexão específica (criada dentro de
`HandleClientAsync`, uma por conexão) está, num laço, chamando:

```csharp
var pacote = await session.ReceiveAsync(serverCancellationToken);
```

Essa chamada "dorme" até que bytes suficientes cheguem do socket para formar um pacote completo
(ver Fase 5). O primeiro pacote que chega, nesta fase da linha do tempo, é sempre o `0x21`.

## Fase 5 — Parser

Os bytes brutos que chegam do socket não vêm necessariamente "no tamanho certo" — TCP entrega
bytes, não mensagens (ver [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), seção 4). O
`JflFrameReader` acumula bytes num buffer interno e usa o `PacketParser` para tentar identificar um
pacote completo e válido dentro desse buffer:

```
┌─────────────────────────────────────────────────────────────┐
│ PacketParser.TryParse(buffer)                                 │
│                                                                │
│  1. O primeiro byte é 0x7B? Se não, descarta 1 byte           │
│     (ressincroniza) e tenta de novo.                           │
│  2. Há bytes suficientes para saber o tamanho total (QDE)?    │
│     Se não, pede mais dados ao socket (NeedMoreData).          │
│  3. Há bytes suficientes para completar o pacote inteiro       │
│     (do tamanho que o QDE diz)? Se não, pede mais dados.       │
│  4. O checksum bate? Se não, descarta o pacote inteiro e       │
│     tenta ressincronizar a partir do próximo byte.             │
│  5. Se tudo bateu: devolve um JflPacket (Seq, Cmd, Dados)       │
│     pronto para uso.                                           │
└─────────────────────────────────────────────────────────────┘
```

Uma vez que um `JflPacket` válido é obtido, ele sobe para o próximo nível.

## Fase 6 — SessionManager

Antes de ser roteado para qualquer lógica de negócio, todo pacote passa primeiro por uma checagem:
"isto é a resposta de algum comando que **nós** enviamos e estamos esperando?" (ver
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md), sobre `SendAndWaitAsync`). Para o
primeiro pacote (`0x21`), a resposta é não — ninguém pediu nada ainda, então o pacote segue para o
**dispatcher de comandos**, que olha o byte `CMD` e decide qual "handler" (tratador) deve processar
aquele pacote. Para `CMD = 0x21`, quem trata é o `ConnectionCommandHandler`, que:

1. Decodifica os campos (`ConnectionRequest.Parse`).
2. Preenche os dados da sessão (`session.NumeroSerie = ...`, etc).
3. Chama `_sessionManager.Registrar(session)` — **é neste exato momento que a sessão passa a
   existir**, do ponto de vista do resto do sistema.

**No log, isso aparece como:**
```
info: CentralHub.SDK.Jfl.Server.Handlers.ConnectionCommandHandler[0]
      Conexao recebida (cmd 0x21) de 10.0.250.21:64883: NS=2751484124 Modelo=Active 100 Bus
      Versao=6.5 MAC=8C4F000A7348 Via=Ethernet
info: CentralHub.SDK.Jfl.Server.SessionManager[0]
      Sessao registrada: central 2751484124 (10.0.250.21:64883)
```

Em paralelo, no Backend (fora do SDK), o `JflSessionPersistenceService` está "ouvindo" o evento
`SessaoRegistrada` do `SessionManager` — quando ele dispara, este serviço vai ao banco de dados,
procura uma `Central` cadastrada com aquele `NumeroSerie`, e (se achar) atualiza
`Status = "Online"`, `UltimoIpConectado`, `UltimoKeepAliveEmUtc`, `ConectadoDesdeUtc`, `Firmware` e
`Modelo`. Ver [`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md).

## Fase 7 — Resposta do handshake

O `ConnectionCommandHandler` monta a resposta (`RESULT` + `KEEP`) e a envia de volta, ecoando o
mesmo `SEQ` recebido — isso é feito através de `session.ReplyAsync(...)`. A central, ao receber
essa resposta com `RESULT = 0x01`, considera-se "conectada e liberada", e passa a operar
normalmente (aguardando comandos, mandando keep-alive periodicamente).

## Fase 8 — Ciclo de KeepAlive

A partir daqui, a cada N minutos (o valor combinado no handshake — tipicamente 5), a central manda
um `0x40` (sem dados), e o CentralHub responde com o intervalo. Este ciclo se repete
**indefinidamente**, enquanto a conexão estiver viva — é o "batimento cardíaco" da sessão. Cada vez
que um keep-alive é recebido, o evento `AtividadeAtualizada` do `SessionManager` dispara, e o
`JflSessionPersistenceService` atualiza `UltimoKeepAliveEmUtc` no banco.

```
   Central                              CentralHub
     │                                       │
     │──── 0x40 (keep-alive, SEQ=N) ────────►│
     │◄─── 0x40 (resposta, SEQ=N) ────────────│
     │                                       │
     │      ... 5 minutos depois ...          │
     │                                       │
     │──── 0x40 (keep-alive, SEQ=N+1) ───────►│
     │◄─── 0x40 (resposta, SEQ=N+1) ──────────│
     │                                       │
     │              (repete...)               │
```

Se a central não conseguir mandar (ou não receber resposta a) um keep-alive por 3 tentativas
seguidas (com 15 segundos entre elas, conforme o manual oficial), ela **derruba a conexão sozinha
e tenta reabrir do zero** (volta para a Fase 2). Do lado do CentralHub, se a conexão cair, a tarefa
`HandleClientAsync` detecta isso (o `ReceiveAsync` retorna `null` ou lança uma exceção de I/O), e o
bloco `finally` remove a sessão do `SessionManager` e fecha os recursos — ver
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md).

## Fase 9 — Eventos (não implementado)

A qualquer momento durante o ciclo de keep-alive, a central *poderia* interromper para mandar um
evento (`0x24`) — por exemplo, se uma zona disparar. **Hoje, o CentralHub reconhece esse pacote
mas não faz nada com ele além de logar** (`EventoCommandHandlerStub`). Isso significa que, se você
estiver olhando os logs em tempo real e um evento chegar, você vai ver uma linha de log dizendo
"comando 0x24 recebido, ainda não implementado" — mas nenhuma ação de negócio acontece, e o banco
de dados não é atualizado com esse evento. Ver [`14_ROADMAP.md`](14_ROADMAP.md).

## Fase 10 — Consulta de Status, sob demanda

Esta fase só acontece quando **um humano, através do Frontend, ou qualquer outro chamador da API**
pede. Não é automática, não acontece sozinha. O gatilho é uma requisição HTTP:

```
GET /api/centrais/5/status
```

Isso desce até o `CentralStatusService`, que chama `CentralStatusQueryService.ConsultarAsync`
(no SDK), que:

1. Procura a sessão da central no `SessionManager`, pelo `NumeroSerie`.
2. Se não achar → devolve erro "offline" (a API traduz isso para HTTP 409).
3. Se achar → chama `session.SendAndWaitAsync(0x4D, ...)`, que:
   a. Gera um novo `SEQ`.
   b. Monta e envia o pacote `0x4D`.
   c. Registra uma "promessa" esperando a resposta com aquele `SEQ`.
   d. **Fica esperando** (até 10 segundos, por padrão) até a resposta chegar, ou até estourar o
      tempo.
4. Quando a resposta chega (interceptada na Fase 6, antes do dispatcher normal, porque tem um
   `SEQ` correspondente a uma promessa pendente), ela é decodificada por
   `CentralStatusResponse.Parse` e devolvida como um objeto C# tipado.
5. O Service converte esse objeto para JSON e devolve pela conexão HTTP.

```
   Frontend         API/Service        SessionManager        Central (via TCP já aberto)
     │                   │                    │                        │
     │─ GET /status ────►│                    │                        │
     │                   │─ TryGet(NS) ──────►│                        │
     │                   │◄─ sessão achada ───│                        │
     │                   │─────────── SendAndWaitAsync(0x4D) ─────────►│
     │                   │                    │      (aguardando...)    │
     │                   │                    │◄──── resposta 0x4D ─────│
     │                   │◄── JflPacket ──────│                        │
     │◄── JSON ──────────│                    │                        │
```

## Fase 11 — Comando de PGM, sob demanda

Idêntico em estrutura à Fase 10, mas com `0x50` (Acionar) ou `0x51` (Desacionar), disparado por:

```
POST /api/centrais/5/pgm/3/ligar
POST /api/centrais/5/pgm/3/desligar
POST /api/centrais/5/pgm/3/pulso        (Body: {"duracaoMs": 1000})
```

Para "Pulso", o `PgmCommandService` executa a sequência **Acionar → `Task.Delay` →
Desacionar**, dois ciclos completos como o da Fase 10, um atrás do outro, na mesma sessão.

## Fase 12 — Desconexão

Pode acontecer por vários motivos: falha de keep-alive (a central desiste e some), a central foi
desligada/reiniciada, problema de rede, ou o próprio CentralHub sendo reiniciado (nesse caso, é o
CentralHub que "esquece" de todas as sessões, não a central). Em qualquer caso, do lado do
CentralHub:

1. `session.ReceiveAsync` detecta o fim da conexão (retorna `null` ou lança exceção).
2. O bloco `finally` de `HandleClientAsync` chama `_sessionManager.Remover(session)`.
3. O evento `SessaoRemovida` dispara, e `JflSessionPersistenceService` marca `Status = "Offline"`
   no banco e zera `ConectadoDesdeUtc`.

## 15. Fluxograma consolidado (todas as fases juntas)

```
┌─────────────┐
│ Central liga │
└──────┬──────┘
       ▼
┌─────────────────────┐
│ Abre conexão TCP      │◄─────────────────────────────┐
└──────┬───────────────┘                                │
       ▼                                                │
┌─────────────────────┐                                 │
│ Envia 0x21            │                                 │
└──────┬───────────────┘                                │
       ▼                                                │
┌─────────────────────┐                                 │
│ Servidor recebe        │                                 │
│ (JflTcpServer)         │                                 │
└──────┬───────────────┘                                │
       ▼                                                │
┌─────────────────────┐                                 │
│ Parser                 │                                 │
│ (JflFrameReader/       │                                 │
│  PacketParser)         │                                 │
└──────┬───────────────┘                                │
       ▼                                                │
┌─────────────────────┐                                 │
│ SessionManager          │                                 │
│ (registra a sessão)     │                                 │
└──────┬───────────────┘                                │
       ▼                                                │
┌─────────────────────┐                                 │
│ Resposta (RESULT+KEEP) │                                 │
└──────┬───────────────┘                                │
       ▼                                                │
┌────────────────────────────────────┐                 │
│  Enquanto a sessão estiver viva:      │                 │
│                                        │                 │
│   ┌──────────────┐  ┌───────────────┐ │                 │
│   │  KeepAlive    │  │  Eventos       │ │                 │
│   │  (cíclico,    │  │  (a qualquer   │ │                 │
│   │  automático)  │  │  momento, NÃO  │ │                 │
│   └──────────────┘  │  implementado) │ │                 │
│                      └───────────────┘ │                 │
│   ┌──────────────┐  ┌───────────────┐ │                 │
│   │  Status        │  │  PGM           │ │                 │
│   │  (sob demanda, │  │  (sob demanda, │ │                 │
│   │  via API)      │  │  via API)      │ │                 │
│   └──────────────┘  └───────────────┘ │                 │
└──────┬─────────────────────────────────┘                 │
       ▼                                                    │
┌─────────────────────┐                                     │
│  Desconexão            │──── central tenta reconectar ─────┘
│  (Status → Offline)    │      automaticamente
└─────────────────────┘
```

## 16. Casos de uso reais

**Cenário: operador quer saber se uma loja está armada agora mesmo.** Isso dispara a Fase 10 uma
única vez, sob demanda — não existe "monitoramento contínuo automático" empurrado pelo servidor
para o navegador (não há WebSocket/SignalR ainda — ver [`14_ROADMAP.md`](14_ROADMAP.md)); o
Frontend faz *polling* (pergunta de novo a cada alguns segundos) para simular atualização em tempo
real.

**Cenário: a central perde energia e depois volta.** Ela passa pela Fase 1 de novo (reboot), abre
uma nova conexão (Fase 2), manda um novo handshake (Fase 3) — o `SessionManager` detecta que já
existe uma sessão antiga para aquele número de série e a substitui pela nova, sem duplicar.

## 17. Boas práticas

- Nunca assuma que Status/PGM vão funcionar instantaneamente — sempre trate o caso de timeout
  (a central pode estar processando outra coisa, ou a rede pode estar lenta).
- Sempre correlacione logs pelo `NumeroSerie` da central ao investigar um problema — é o
  identificador estável, ao contrário do IP.

## 18. Problemas comuns

- **PGM "trava" por alguns segundos antes de dar erro** → comportamento esperado quando a central
  não responde; o timeout padrão é de 10 segundos (ver
  [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md)).
- **Status funciona mas PGM não** → verifique se a PGM pedida realmente existe/está configurada
  naquele modelo/instalação — a central pode responder sem confirmar a mudança de estado se não
  houver permissão.

## 19. Como testar cada fase isoladamente

O projeto tem testes automatizados que cobrem cada fase separadamente:
- Fase 5 (Parser): `SDK/CentralHub.SDK.Tests/Protocol/PacketParserTests.cs`
- Fase 6 (SessionManager): `SDK/CentralHub.SDK.Tests/Server/SessionManagerTests.cs`
- Fase 10 (Status): `SDK/CentralHub.SDK.Tests/Server/CentralStatusQueryServiceTests.cs`
- Fase 11 (PGM): `SDK/CentralHub.SDK.Tests/Server/PgmCommandServiceTests.cs`
- Fluxo completo, ponta a ponta, com socket real: `JflTcpServerIntegrationTests.cs`

## 20. Como depurar cada fase

Todo log relevante usa o `NumeroSerie` e/ou o `RemoteEndPoint` como contexto — filtrar os logs por
esses valores é a forma mais rápida de acompanhar uma sessão específica do início ao fim.

## 21. FAQ

**P: O Status e o PGM podem ser pedidos ao mesmo tempo, para a mesma central?**
R: Sim — cada pedido gera um `SEQ` diferente, e o mecanismo de correlação (ver
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md)) permite várias "promessas" pendentes
simultâneas na mesma sessão, desde que a própria central consiga processar/responder cada uma.

**P: O que acontece se eu pedir Status de uma central que já desconectou um segundo atrás?**
R: `SessionManager.TryGet` não vai achar a sessão, e o erro "offline" (HTTP 409) é devolvido
imediatamente, sem esperar nenhum timeout.

## 22. Checklist

- [ ] Sei listar as 12 fases na ordem certa.
- [ ] Sei diferenciar o que acontece automaticamente (keep-alive) do que só acontece sob demanda
      (status, PGM).
- [ ] Sei onde, no código, cada fase é implementada.

---

**Documento anterior:** [`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md)
**Próximo documento:** [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
