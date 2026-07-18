# HANDOFF — Continuação de sessão (Claude)

> Este arquivo existe para retomar o trabalho em uma nova conversa. Cole/aponte para este
> arquivo no início da nova sessão e peça para continuar a partir daqui.

## Como retomar

Na nova conversa, diga algo como: *"Leia Documentation/HANDOFF_SESSAO_ATUAL.md e continue de
onde parei — o plano já está aprovado, implemente."* O plano completo e aprovado está salvo em
`C:\Users\STI- NTBK\.claude\plans\agile-squishing-ripple.md` (fora do repositório, no perfil do
Claude Code) — se esse arquivo não existir mais/não for acessível na nova sessão, **todo o
conteúdo do plano está reproduzido na íntegra logo abaixo**, então não é estritamente necessário.

---

## Contexto do projeto

**CentralHub** — sistema de monitoramento de centrais de alarme JFL (Active 100 Bus), em
`c:\Users\STI- NTBK\Documents\central`. Solução .NET 9 (`Backend/CentralHub.Api`,
`SDK/CentralHub.SDK`, `SDK/CentralHub.SDK.Tests`) + Frontend React/TS/MUI (`Frontend/`) +
ferramentas de homologação (`Simulator/`, `SDK/CentralHub.SDK.Benchmarks`,
`SDK/CentralHub.SDK.Tools/ReplayCli`) + documentação extensa em `Documentation/` e
`Documentation/Protocol/`.

**Regra de ouro em todo o projeto**: nunca alterar o núcleo homologado contra hardware real —
`SDK/CentralHub.SDK/Jfl/**` (Handshake, KeepAlive, Parser, Builder, PGM, Status, SessionManager,
JflSession, JflTcpServer, Protocol, Handlers). Qualquer mudança nessa área é motivo de rejeição
imediata pelo usuário — já aconteceu 2x nesta sessão histórica com planos anteriores.

## O que já foi feito em sessões anteriores (não mexer, está pronto e testado)

1. **Correção do PATH do `dotnet`** no Windows (ordem de entradas conflitantes, instalação x86
   legada removida).
2. **Correção de CORS** — origens configuráveis via `appsettings.Development.json`/
   `appsettings.Production.json` (`Cors:AllowedOrigins`), lidas dinamicamente em `Program.cs`.
3. **"Fase 0" completa** — plataforma de homologação do protocolo JFL (10 itens, todos entregues
   e testados):
   - `SDK/CentralHub.SDK/Jfl/Diagnostics/PacketAnalyzer.cs` — decompõe pacotes 0x7B.
   - Packet Inspector web (`Controllers/Dev/PacketInspectorController.cs` +
     `Frontend/src/pages/dev/PacketInspectorPage.tsx`, rota `/ferramentas/inspetor-pacotes`).
   - `Documentation/RealCaptures/*.bin` — capturas reais do manual oficial.
   - `SDK/CentralHub.SDK/Jfl/Diagnostics/ReplayEngine.cs` + CLI (`SDK/CentralHub.SDK.Tools/ReplayCli`).
   - `Simulator/CentralHub.Simulator/` — simula uma Active 100 Bus completa do lado cliente
     (reaproveita só `PacketBuilder`/`JflFrameReader`/`ChecksumCalculator`/`JflCommand`/`JflModel`
     do SDK, nunca `SessionManager`/`JflSession`). Testado **contra um `JflTcpServer` real**
     (porta efêmera) em `Simulator/CentralHub.Simulator.Tests/`.
   - `Simulator/CentralHub.StressTest/` — gera carga usando o simulador.
   - `SDK/CentralHub.SDK.Benchmarks/` — BenchmarkDotNet.
   - `SDK/CentralHub.SDK/Jfl/Diagnostics/HexLoggingStream.cs` — decorator de `Stream`
     transparente, opt-in via `Jfl:LogHexAtivado` (default `false`). Único arquivo de
     infraestrutura de conexão tocado nessa fase: `JflTcpServer.cs`, uma troca de poucas linhas
     (`JflSession.FromTcpClient` → método local `CriarSessao` que envolve o stream
     condicionalmente) — **`JflSession.cs` nunca foi alterado**, usa um construtor público que já
     existia.
   - Documentação completa em `Documentation/Protocol/` (00_INDEX até 20_CHANGELOG, mais
     09-13_*.md com a especificação já pesquisada e pronta para as Fases 1-5 do protocolo —
     Eventos/Arme/Zonas/Usuários/Data-Hora — que ainda **não foram implementadas**, só
     documentadas/planejadas).
   - **Achado importante já pesquisado e documentado**: bitmap do comando Inibir Zonas (0x52) é
     **MSB-first por byte** (bit7 = zona menor), diferente do campo `P-INIB` da resposta de status
     (LSB-first) — confirmado com 3 capturas reais do manual. Ver
     `Documentation/Protocol/11_ZONES.md`.
   - Todos os testes: `dotnet test SDK\CentralHub.SDK.Tests\...` (116 testes) e
     `Simulator\CentralHub.Simulator.Tests\...` (5 testes) — 100% verdes na última execução.

**Nota de ambiente**: em algum momento desta sessão havia uma central real (hardware físico)
conectada ao processo `CentralHub.Api` em execução (IP remoto `10.0.250.21` na porta 8085) — por
isso builds do Backend foram feitos com `-o <pasta temporária>` para não travar em arquivo
bloqueado, e o processo em execução nunca foi encerrado. **Verificar se ainda há uma instância
rodando antes de reiniciar o Backend** (`Get-Process -Name "CentralHub.Api"`,
`Get-NetTCPConnection -LocalPort 8085`) — se houver sessão real conectada, não derrubar sem avisar
o usuário.

---

## TAREFA ATUAL (em andamento — plano aprovado, implementação NÃO começou ainda)

### Pedido do usuário

Transformar a tela "Centrais" (lista + detalhe) de um fluxo baseado em `TcpClient` de saída
(testar IP/Porta manualmente) para um painel de monitoramento real baseado no `SessionManager`
(a central disca para o servidor, nunca o contrário — arquitetura já real no Backend, só a tela
Web ainda reflete a arquitetura antiga).

### Estado: só planejamento feito, ZERO código escrito ainda

Fui interrompido bem no início da implementação (só tinha checado se o processo do Backend ainda
estava rodando). **Nenhum arquivo foi criado ou alterado para esta tarefa ainda.** A todo list
(TodoWrite) tinha estes itens, todos `pending`:

1. Marcar Adapters legados como `[Obsolete]`
2. Remover `ConnectionService`/testar-conexao/DTOs relacionados
3. Tornar IP/Porta/Usuario/Senha opcionais nos DTOs + defaults no `CentralService`
4. Criar captura de atividade (`AtividadeLogEntry`, `SessionActivityLogService`,
   `SdkActivityLoggerProvider`/`Logger`)
5. Criar `CentralSessionService` + `SessaoDtos` (sessao/log/reconectar/diagnostico)
6. Adicionar actions novas no `CentralController` + registrar tudo em `Program.cs`
7. Criar projeto `Backend.Api.Tests` + testes (log service + `CentralSessionService` via Simulator)
8. Reescrever `CentralsPage.tsx` (remover teste de conexão, adicionar NumeroSerie)
9. Atualizar `types/index.ts`
10. Reescrever `CentralDetailPage.tsx` + componentes novos de sessão
11. Criar `Documentation/ARQUITETURA_SESSION_MANAGER.md` + atualizar docs existentes + changelog
12. Validação final: build completo, testes, checagem manual

**Comece pelo item 1, na ordem listada** — é a ordem lógica de dependências (limpar legado antes
de construir o novo; Backend antes de Frontend; Frontend antes de docs/validação final).

### Pesquisa já feita (não precisa re-explorar, os fatos abaixo já foram verificados linha a linha)

**Frontend atual:**
- `Frontend/src/pages/CentralsPage.tsx` (240 linhas) — botão "Testar Conexão" faz
  `POST /central/testar-conexao` com `{ip, porta, usuario, senha}`; **o botão Salvar fica
  desabilitado até o teste ter sucesso** (`disabled={!conexaoInfo?.sucesso}`, linha 194); form
  tem Nome/IP/Porta/Usuário/Senha/Prédio — **não tem campo NumeroSerie** (apesar de o Backend já
  suportar).
- `Frontend/src/pages/CentralDetailPage.tsx` (245 linhas) — polling: status a cada 5s, central a
  cada 15s, relógio a cada 1s. Já mostra Número Série/Modelo/Firmware/IP Atual/Último
  KeepAlive/Tempo Conectado num Paper no topo, e um segundo Paper com bateria/AC/eletrificador/
  partições/zonas/problemas (do `CentralStatus`). `PgmPanel` no final, inalterado.
- `Frontend/src/types/index.ts` — `Central` já tem `numeroSerie?: string` (só falta no formulário
  visível). `ConexaoResult` existe e será removido.
- `Frontend/src/services/api.ts` — axios simples, `baseURL: 'http://localhost:5000/api'`, sem
  interceptors.
- Sem SignalR/websocket — tudo via `setInterval` polling. Manter esse padrão para o "LOG DA
  CENTRAL" (polling a cada 3-5s), não introduzir SignalR.

**Backend atual:**
- `Backend/CentralHub.Api/Controllers/CentralController.cs` — remover só a action `TestarConexao`
  (linha ~102-114, rota `POST api/central/testar-conexao`); manter GetAll/GetById/Create/Update/
  Delete e as rotas absolutas de status/PGM (`~/api/centrais/{id}/status`, `.../pgm/{pgm}/...`).
- `Backend/CentralHub.Api/Services/ConnectionService.cs` — **apagar inteiro** (único consumidor é
  `CentralService.TestarConexaoAsync`).
- `Backend/CentralHub.Api/Services/CentralService.cs` — remover `ConnectionService` do construtor
  e o método `TestarConexaoAsync`.
- `Backend/CentralHub.Api/DTOs/CentralDtos.cs` — remover `TestarConexaoDto`/`ConexaoResultDto`;
  em `CreateCentralDto`/`UpdateCentralDto`, tirar `[Required]`/`[Range]` de `IP`/`Porta`/
  `Usuario`/`Senha` (viram opcionais — `CentralService` aplica `string.Empty`/`0` como default ao
  gravar no `Central`, que **continua com colunas não-anuláveis no banco**, sem migração).
- `Backend/CentralHub.Api/Program.cs` — remover `builder.Services.AddScoped<ConnectionService>();`
  (linha 34); registrar os novos serviços (ver abaixo).
- `SDK/CentralHub.SDK/Adapters/` (`AdapterFactory.cs`, `TcpConnectionHelper.cs`, `JflAdapter.cs`,
  `IntelbrasAdapter.cs`, `ICentralAdapter.cs`, `FakeAdapter.cs`) — **não apagar** (ainda usado por
  `OperationService.cs`, fluxo legado separado e já simulado, fora de escopo). Só marcar
  `[Obsolete]` em `TcpConnectionHelper.TestarConexao`, `JflAdapter.VerificarConectividade`,
  `IntelbrasAdapter.VerificarConectividade`, e comentário XML em `AdapterFactory`.
- `Backend/CentralHub.Api/Services/JflSessionPersistenceService.cs` (182 linhas, já lido por
  completo) — é o padrão de referência para o novo `SessionActivityLogService`: `IHostedService`
  que assina `SessionManager.SessaoRegistrada`/`SessaoRemovida`/`AtividadeAtualizada` (eventos
  públicos, SDK não alterado).
- `Backend/CentralHub.Api/Services/CentralStatusService.cs` / `PgmService.cs` — padrão de
  referência para `CentralSessionService`: resolve `Central` por Id no banco, valida
  `NumeroSerie`, delega a um serviço do SDK.

**SDK — superfície pública já confirmada (releitura completa de `SessionManager.cs` e
`JflSession.cs` nesta sessão):**
- `SessionManager`: `TryGet(numeroSerie, out session)`, `Registrar`, `Remover`,
  `NotificarAtividade`, eventos `SessaoRegistrada`/`SessaoRemovida`/`AtividadeAtualizada`,
  `QuantidadeAtiva`, `Sessoes` (IReadOnlyCollection).
- `JflSession`: `Id`, `RemoteEndPoint`, `RemoteIp`, `State` (enum `Conectando`/`Ativa`/
  `Encerrada`), `NumeroSerie`, `Imei`, `Mac`, `Modelo` (byte?), `VersaoFirmware`, `ConectadoEmUtc`,
  `UltimaAtividadeUtc`, `Close()`. **Não expõe** SEQ atual, bytes enviados/recebidos, último
  comando, nem latência — por isso a solução usa captura de log estruturado (ver abaixo) em vez
  de pedir novos campos no SDK.
- **Achado-chave**: `appsettings.json` já tem `"Logging":{"LogLevel":{"CentralHub.SDK.Jfl":
  "Debug"}}` configurado — logs estruturados por pacote (`Cmd`, `Seq`, `BytesRecebidos` em
  `JflTcpServer`; `Seq`, `BytesEnviados/Recebidos`, `TempoRespostaMs` em `PgmCommandService`/
  `CentralStatusQueryService`) **já fluem hoje**, sem mudança de config. Só falta capturá-los via
  um `ILoggerProvider` customizado (100% Backend, zero SDK).

---

## PLANO COMPLETO APROVADO (reproduzido na íntegra)

O plano abaixo já foi aprovado pelo usuário via `ExitPlanMode`. Implemente exatamente como
descrito, seguindo a ordem da todo list acima.

<!-- BEGIN PLANO APROVADO -->

# Tela "Centrais" → Painel de Monitoramento de Sessão (SessionManager)

## Contexto

O CentralHub já migrou de arquitetura (central disca para o servidor, nunca o contrário — via
`TcpListener` + `SessionManager`, homologado contra hardware real), mas a tela "Centrais" e o
endpoint `POST /api/central/testar-conexao` continuam da era anterior: abrem um `TcpClient` de
saída (`SDK/CentralHub.SDK/Adapters/TcpConnectionHelper.cs`) tentando discar para IP/Porta
digitados manualmente, e **bloqueiam o salvamento do cadastro** até esse teste ter sucesso — algo
que hoje é sempre artificial (a central real nunca escuta nessa porta como servidor) e gera
confusão exatamente como descrito: uma central com sessão ativa de verdade pode ainda assim
"falhar no teste".

O objetivo é reescrever a camada Web/API dessa tela para refletir a arquitetura real: toda
informação vem do `SessionManager`/`JflSession` (via `NumeroSerie`, nunca IP/Porta), e nenhuma
ação da tela abre um socket de saída. **Handshake, KeepAlive, Parser, Builder, PGM, Status e o
restante do núcleo do protocolo (SDK) não são alterados** — a mudança é inteiramente na camada
Backend (Controllers/Services/DTOs) e Frontend.

## Achado que define a arquitetura da solução

`JflSession`/`SessionManager` (SDK, não alterados) já expõem publicamente: `Id`, `RemoteEndPoint`,
`RemoteIp`, `State` (Conectando/Ativa/Encerrada), `NumeroSerie`, `Imei`, `Mac`, `Modelo`,
`VersaoFirmware`, `ConectadoEmUtc`, `UltimaAtividadeUtc`, e `SessionManager.Sessoes`/`TryGet`. Isso
cobre a maior parte dos campos pedidos — mas **não expõe** SEQ atual, bytes enviados/recebidos,
último comando recebido, nem latência por comando (não existem como propriedades públicas hoje, e
não posso adicioná-las sem alterar arquivos do SDK).

**A solução, sem tocar no SDK**: esses dados **já são logados hoje**, estruturadamente, pelas
classes existentes (`JflTcpServer` loga `Cmd`, `Seq`, `BytesRecebidos` por pacote em nível Debug;
`PgmCommandService`/`CentralStatusQueryService` logam `Seq`, `BytesEnviados`/`BytesRecebidos`,
`TempoRespostaMs` por comando) — e `appsettings.json` **já tem** `"CentralHub.SDK.Jfl": "Debug"`
configurado, então esses logs já fluem em qualquer ambiente, sem mudança de configuração. A peça
que falta é só **capturar** esses logs estruturados (não o texto, as propriedades nomeadas) num
buffer em memória, no Backend — um `ILoggerProvider` customizado, 100% aditivo, zero alteração em
qualquer arquivo do SDK. Isso alimenta o "LOG DA CENTRAL", o "SEQ atual", "bytes recebidos/
enviados" e "latência" pedidos, com dados reais, nunca inventados.

## Decisões de design (documentadas aqui para revisão, não são perguntas em aberto)

1. **`SDK/CentralHub.SDK/Adapters/*`** (`AdapterFactory`, `TcpConnectionHelper`, `JflAdapter`,
   `IntelbrasAdapter`, `ICentralAdapter`, `FakeAdapter`) é código legado autocontido, sem relação
   com `SessionManager`/`JflSession`/Handshake/KeepAlive/Parser/Builder — já documentado em vários
   lugares (`Documentation/01_PROJECT_OVERVIEW.md`, `12_FAQ.md` Q43/Q44, `14_ROADMAP.md` item 13)
   como candidato à remoção. Ainda é usado por `OperationService`/`OperationPage` (fluxo separado,
   já simulado/mockado, não citado neste pedido). Seguindo sua própria instrução ("caso ainda seja
   utilizado em outro lugar, marcar como `[Obsoleto]` e documentar"): **não apago os arquivos**,
   só adiciono `[Obsolete]` + comentário XML explicando o porquê em `TcpConnectionHelper.cs`
   (o único ponto que de fato abre um `TcpClient` de saída) e nas classes que o chamam
   (`JflAdapter.VerificarConectividade`, `IntelbrasAdapter.VerificarConectividade`,
   `AdapterFactory.DetectarEConectar`) — anotação pura, comportamento idêntico, não quebra
   `OperationService`.
2. **`Central.IP`/`Porta`/`Usuario`/`Senha` deixam de ser obrigatórios** no cadastro (fazia sentido
   só para alimentar o teste de conexão removido). Em vez de tornar as colunas do banco anuláveis
   (migração de schema + risco de quebrar `OperationService`, que ainda lê esses campos como
   `string`/`int` não-anuláveis para o fluxo simulado de PGM), a solução mais simples e segura:
   `CreateCentralDto`/`UpdateCentralDto` passam a aceitar esses campos como opcionais
   (`string?`/`int?`, sem `[Required]`/`[Range]`), e `CentralService` aplica um valor padrão
   (`string.Empty`/`0`) ao gravar no `Central` (que continua com as colunas como estão — zero
   migração de banco). Os campos somem do formulário visível da tela (sem uso real na arquitetura
   atual, manter visível só geraria mais confusão) — os dados antigos de centrais já cadastradas
   continuam intactos no banco.
3. **`NumeroSerie` já é suportado de ponta a ponta no Backend** (`CreateCentralDto`/
   `UpdateCentralDto`/`CentralDto` já têm o campo) — só falta no formulário do Frontend. Adicioná-lo
   é pré-requisito para a tela fazer sentido (é a chave que o `SessionManager` usa para casar uma
   conexão real com o cadastro).

## Arquivos que NUNCA são alterados

```
SDK/CentralHub.SDK/Jfl/**  (Handshake, KeepAlive, Parser, Builder, PGM, Status, SessionManager,
                             JflSession, JflTcpServer, Protocol, Handlers — todo o núcleo homologado)
```
(`SDK/CentralHub.SDK/Adapters/*` recebe só anotações `[Obsolete]`, ver decisão 1 acima — nenhuma
lógica de negócio muda ali.)

---

## Backend — remover

- `Backend/CentralHub.Api/Services/ConnectionService.cs` — **apagar** (único consumidor é
  `CentralService.TestarConexaoAsync`, que também é removido; sem outras referências).
- `Backend/CentralHub.Api/Controllers/CentralController.cs` — remover a action `TestarConexao`
  (rota `POST api/central/testar-conexao`).
- `Backend/CentralHub.Api/Services/CentralService.cs` — remover `ConnectionService` do
  construtor e `TestarConexaoAsync`.
- `Backend/CentralHub.Api/DTOs/CentralDtos.cs` — remover `TestarConexaoDto`/`ConexaoResultDto`
  (sem outros consumidores).
- `Backend/CentralHub.Api/Program.cs` — remover `builder.Services.AddScoped<ConnectionService>();`.

## Backend — marcar como obsoleto (não remover, ainda usado por `OperationService`)

- `SDK/CentralHub.SDK/Adapters/TcpConnectionHelper.cs` — `[Obsolete("Discagem de saída não é mais
  usada pela arquitetura real (SessionManager). Mantido só para OperationService/legado.")]` no
  método `TestarConexao`, com comentário XML explicando a migração de arquitetura.
- `SDK/CentralHub.SDK/Adapters/JflAdapter.cs`/`IntelbrasAdapter.cs` — mesma anotação em
  `VerificarConectividade` (o método que chama `TcpConnectionHelper`).
- `SDK/CentralHub.SDK/Adapters/AdapterFactory.cs` — comentário XML no topo da classe e em
  `DetectarEConectar` explicando que só sobrevive para o fluxo simulado de `OperationService`.

## Backend — novo: captura de atividade da sessão (sem tocar no SDK)

```csharp
// Backend/CentralHub.Api/Logging/AtividadeLogEntry.cs (NOVO)
public sealed class AtividadeLogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public LogLevel Nivel { get; init; }
    public required string Categoria { get; init; }   // nome da classe SDK que gerou o log
    public required string Mensagem { get; init; }     // mensagem formatada, pronta pra exibir
    public string? RemoteEndPoint { get; init; }
    public string? NumeroSerie { get; init; }
    public byte? Cmd { get; init; }
    public byte? Seq { get; init; }
    public int? BytesRecebidos { get; init; }
    public int? BytesEnviados { get; init; }
    public double? TempoRespostaMs { get; init; }
}

// Backend/CentralHub.Api/Logging/SessionActivityLogService.cs (NOVO)
// Buffer em memória (ConcurrentQueue limitado, ex.: 500 entradas globais), com
// Registrar(entry) e ObterPara(numeroSerie, max). Resolve NumeroSerie a partir de
// RemoteEndPoint quando o log não o carrega diretamente, usando um mapa interno
// RemoteEndPoint->NumeroSerie alimentado pelo evento publico SessionManager.SessaoRegistrada
// (leitura de evento já existente, zero alteração no SDK).

// Backend/CentralHub.Api/Logging/SdkActivityLoggerProvider.cs (NOVO) : ILoggerProvider
// Backend/CentralHub.Api/Logging/SdkActivityLogger.cs (NOVO) : ILogger
// CreateLogger(categoria) só captura de verdade quando categoria começa com
// "CentralHub.SDK.Jfl" (ignora ASP.NET/EF/etc.). Log<TState> extrai as propriedades
// nomeadas do `state` estruturado (IReadOnlyList<KeyValuePair<string,object>>, já é
// assim que toda chamada _logger.LogInformation("... {X} ...", x) funciona) — sem
// parsing de texto/regex. Repassa cada entrada pro SessionActivityLogService.
```

Registro em `Program.cs` (aditivo):
```csharp
builder.Services.AddSingleton<SessionActivityLogService>();
builder.Services.AddSingleton<ILoggerProvider>(sp =>
    new SdkActivityLoggerProvider(sp.GetRequiredService<SessionActivityLogService>()));
```
(`SessionActivityLogService` também assina `SessionManager.SessaoRegistrada` para popular o mapa
RemoteEndPoint→NumeroSerie — via `IHostedService` leve, mesmo padrão de
`JflSessionPersistenceService`, só que sem tocar no banco.)

## Backend — novo: serviço e DTOs de sessão

```csharp
// Backend/CentralHub.Api/Services/CentralSessionService.cs (NOVO)
public class CentralSessionService
{
    // ctor: AppDbContext, SessionManager (SDK, so leitura publica), SessionActivityLogService

    // GET-equivalente: monta o snapshot completo (Status da Conexão + Sessão TCP +
    // Detalhes da Sessão vêm todos deste mesmo método/DTO — um recurso rico, várias
    // vistas no Frontend).
    public Task<SessaoDto> ObterSessaoAsync(int centralId, CancellationToken ct);

    // Log da Central: repassa SessionActivityLogService.ObterPara(numeroSerie).
    public Task<IReadOnlyList<AtividadeLogEntryDto>> ObterLogAsync(int centralId, int max, CancellationToken ct);

    // Reconectar: NAO abre socket. SessionManager.TryGet -> se achou, session.Close() +
    // sessionManager.Remover(session) (ambos metodos publicos ja existentes no SDK).
    // Central.Status volta pra "Offline"/"Aguardando conexão" via o proprio
    // JflSessionPersistenceService (que já escuta SessaoRemovida) -- não precisa duplicar
    // essa escrita aqui.
    public Task<ReconectarResultDto> ReconectarAsync(int centralId, CancellationToken ct);

    // Diagnóstico: checklist combinando Central (DB) + SessionManager (live) +
    // SessionActivityLogService (última consulta de status bem-sucedida).
    public Task<DiagnosticoDto> ObterDiagnosticoAsync(int centralId, CancellationToken ct);
}
```

`SessaoDto` (novo, `DTOs/SessaoDtos.cs`) — status calculado (não a coluna `Central.Status`, que
pode ficar defasada; a fonte de verdade é `SessionManager.TryGet` no momento da chamada):
- `StatusConexao`: `"Online"` (sessão encontrada, `State=Ativa`) / `"AguardandoConexao"` (sessão
  encontrada, `State=Conectando`) / `"Offline"` (nenhuma sessão encontrada).
- Campos do cadastro: `NumeroSerie`, `Modelo`, `Firmware`.
- Campos da sessão viva (quando houver): `IpSessao` (`RemoteIp`), `PortaRemota` (extraída de
  `RemoteEndPoint`), `DataHoraConexao` (`ConectadoEmUtc`), `UltimoPacoteRecebidoEmUtc`
  (`UltimaAtividadeUtc`), `TempoConectadoSegundos` (calculado), `SocketConectado`/
  `HandshakeRealizado`/`KeepAliveAtivo` (derivados de `State` + tempo desde `UltimaAtividadeUtc`
  vs. o intervalo de keep-alive configurado).
- Campos do banco (sempre, mesmo offline): `UltimoKeepAliveEmUtc`, `UltimoIpConectado` (histórico).
- `Latencia`/`UltimoComando`/`UltimoSeq`/`BytesRecebidos`/`BytesEnviados`: preenchidos a partir da
  entrada mais recente relevante do `SessionActivityLogService` — `null`/"não disponível" quando
  não houver nenhum comando registrado ainda nesta sessão (isso é esperado e normal, não é bug).

`DiagnosticoDto` — checklist adaptado à realidade do banco atual (sem inventar dados):
Sessão ativa?, Handshake realizado?, KeepAlive dentro do prazo (usa a mesma regra do manual
oficial já citada no protocolo: 1,5x o intervalo configurado antes de considerar atrasado)?,
Número de Série cadastrado?, Central vinculada a um Prédio?, Última consulta de Status bem-
sucedida (via log de atividade)? — o item "PGMs cadastradas?" do pedido original não tem uma
entidade real para checar hoje (não existe configuração de PGM persistida neste projeto) — vira
"Permissão de PGM detectada na última consulta de status?" quando houver uma consulta recente, ou
fica marcado como "sem dados ainda" — não finjo uma checagem que não existe.

## Backend — Controller

`Backend/CentralHub.Api/Controllers/CentralController.cs` (modificado, aditivo — remove só
`TestarConexao`, mantém tudo mais):
```
GET  ~/api/centrais/{id}/sessao        → CentralSessionService.ObterSessaoAsync
GET  ~/api/centrais/{id}/log           → CentralSessionService.ObterLogAsync
POST ~/api/centrais/{id}/reconectar    → CentralSessionService.ReconectarAsync
GET  ~/api/centrais/{id}/diagnostico   → CentralSessionService.ObterDiagnosticoAsync
GET  ~/api/centrais/{id}/status        → já existe (CentralStatusService), reusado sem mudança
```

`Program.cs`: registra `CentralSessionService` (Scoped, como os demais serviços existentes).

---

## Frontend

### `CentralsPage.tsx` (lista + cadastro)
- Remove `testarConexao`, `conexaoInfo`, o card de resultado do teste, e o gate de
  `disabled={!conexaoInfo?.sucesso}` no botão Salvar — salvar passa a exigir só Nome + Prédio
  (como já era antes de qualquer coisa de conexão existir).
- Remove os campos IP/Porta/Usuário/Senha do formulário visível (dados legados, sem uso real —
  decisão 2 acima). Adiciona campo **Número de Série** (opcional, com texto de ajuda: "necessário
  para a central conectar automaticamente ao servidor").
- Tabela: troca a coluna "IP" por "Número de Série" (mais relevante na arquitetura atual); mantém
  Fabricante/Modelo/Status/Ações como estão.

### `CentralDetailPage.tsx` (painel de monitoramento — reescrita central deste pedido)
Decomposto em componentes novos (arquivo principal fica só orquestrando):
- `components/session/StatusConexaoCard.tsx` — 🟢/🔴/🟡 + todos os campos pedidos (Status, Número
  de Série, Modelo, Firmware, IP da sessão, Porta Remota, Data/Hora da conexão, Último KeepAlive,
  Tempo conectado, Latência, Tempo desde o último KeepAlive, Sessão ativa Sim/Não) + botão
  **Atualizar Status** (chama `GET .../sessao` de novo, não abre nada).
- `components/session/SessaoTcpPanel.tsx` — IP remoto, porta remota, socket conectado, handshake
  realizado, keep-alive ativo, tempo conectado, último pacote recebido, último comando recebido,
  SEQ atual, último erro.
- `components/session/IndicadoresChips.tsx` — 🟢/🔴 para sessão ativa, handshake, keep-alive,
  status sincronizado, central cadastrada / sem sessão, keep-alive expirado, número de série
  divergente.
- `components/session/LogCentralPanel.tsx` — lista rolável, mais recente primeiro, polling a cada
  ~3-5s de `GET .../log`.
- `components/session/DetalhesSessaoModal.tsx` — `Dialog` com todos os campos pedidos, aberto pelo
  botão **Detalhes da Sessão**, reaproveitando o mesmo payload de `.../sessao`.
- `components/session/DiagnosticoPanel.tsx` — checklist, polling junto com o card principal.
- Botão **Reconectar**: `Dialog` de confirmação deixando explícito "isso não abre conexão, só
  limpa a sessão registrada; a central deve reconectar sozinha" → `POST .../reconectar` → mostra a
  instrução devolvida pela API.
- Botão **Solicitar Status**: chama a função `carregarStatus` já existente (reaproveita o polling
  atual de 5s como está — o botão só antecipa uma atualização manual, não substitui o polling).
- Bloco de identidade/Status atuais da página (partições/zonas/PGMs/bateria/AC/eletrificador)
  continuam exatamente como estão hoje — não fazem parte deste pedido.

### `types/index.ts`
- Remove `ConexaoResult`.
- Adiciona `SessaoInfo`, `AtividadeLogEntry`, `DiagnosticoResultado`, `ReconectarResultado`
  (espelhando os DTOs novos do Backend).
- `Central`: `numeroSerie` já existe no tipo — sem mudança aqui.

---

## Documentação

- **Novo** `Documentation/ARQUITETURA_SESSION_MANAGER.md` — arquitetura antiga (TcpClient) →
  problemas → arquitetura nova (TcpListener + SessionManager, central conecta no servidor) →
  diagramas ASCII → fluxogramas → exemplos → FAQ, como pedido.
- Atualizar `Documentation/01_PROJECT_OVERVIEW.md`, `05_SOURCE_CODE_GUIDE.md`,
  `06_DATABASE_GUIDE.md`, `09_WEB_GUIDE.md`, `12_FAQ.md` (Q43/Q44), `14_ROADMAP.md` (item 13) —
  cada um já tem uma seção específica marcando este código como legado; atualizar para refletir
  que `testar-conexao`/`ConnectionService` foram **removidos** (não só "legado") e que o restante
  do fluxo de Adapters está `[Obsolete]`, com link para o novo documento de arquitetura.
- `Documentation/Protocol/20_CHANGELOG.md` (já existe, da leva anterior) ganha uma entrada nova
  para esta mudança, no mesmo formato já estabelecido.

## Testes

- **Novo projeto** `Backend/CentralHub.Api.Tests` (primeiro projeto de teste do Backend) —
  `SessionActivityLogServiceTests` (captura e resolve NumeroSerie via RemoteEndPoint corretamente),
  `CentralSessionServiceTests` — reaproveitando o **Central Simulator** (`Simulator/
  CentralHub.Simulator`, já existente) contra um `JflTcpServer` efêmero real: conecta um
  simulador, confirma `StatusConexao="Online"`; desconecta, confirma `"Offline"`; testa
  `ReconectarAsync` derrubando uma sessão real e confirmando que some do `SessionManager`.
- SDK: nenhum teste novo necessário (nada no SDK muda de comportamento — as anotações `[Obsolete]`
  não alteram lógica, só emitem warning de compilação).
- Gate obrigatório: `dotnet test SDK\CentralHub.SDK.Tests\...` continua 100% verde (nenhum arquivo
  do núcleo tocado).

## Verificação

```
1. dotnet build CentralHub.sln
2. dotnet test SDK\CentralHub.SDK.Tests\...        → 100% verde, zero regressão
3. dotnet test Backend\CentralHub.Api.Tests\...     → novos testes verdes
4. npm run build (Frontend)                          → 0 erros TypeScript
5. Rodar o Backend + Frontend, conectar o Central Simulator (Simulator/CentralHub.Simulator)
   contra ele, e confirmar na tela: Status muda pra Online, Log da Central mostra a conexão/
   handshake/keep-alive, Detalhes da Sessão abre com dados reais, Reconectar derruba a sessão
   (simulador percebe a desconexão), Solicitar Status atualiza partições/zonas/PGMs sem reload.
6. Confirmar que criar uma Central nova não exige mais nenhum teste de conexão nem IP/Porta.
```

## Arquivos críticos (referência rápida)

- `SDK/CentralHub.SDK/Jfl/Server/SessionManager.cs` / `JflSession.cs` — só leitura, superfície
  pública já suficiente para tudo isso (documentada acima).
- `Backend/CentralHub.Api/Services/JflSessionPersistenceService.cs` — padrão de referência para
  `SessionActivityLogService` (IHostedService assinando eventos do SessionManager).
- `Backend/CentralHub.Api/Services/CentralStatusService.cs` / `PgmService.cs` — padrão de
  referência para `CentralSessionService` (resolve NumeroSerie no banco, delega ao SDK).
- `Simulator/CentralHub.Simulator/` — reaproveitado para os novos testes de integração.
- `appsettings.json` — já tem `Logging:LogLevel:CentralHub.SDK.Jfl=Debug`, pré-requisito do
  `SdkActivityLoggerProvider`, nenhuma mudança de configuração necessária.

<!-- END PLANO APROVADO -->

---

## Lembretes de comportamento/estilo já validados com o usuário nesta sessão

- **Nunca** propor/implementar mudança em `SDK/CentralHub.SDK/Jfl/**` sem antes conferir contra o
  manual oficial (`Documentação JFL Alarmes\Protocolo comunicação softwares monitoramento -
  publico.pdf` — se o caminho com acentos der erro ao ler diretamente, copiar para um caminho sem
  acentos no scratchpad primeiro, é um bug de normalização Unicode NFC/NFD do ambiente).
- Usuário rejeita planos que assumem coisas "de memória" — sempre verificar bytes/captura real
  antes de codificar parsers de protocolo.
- Usuário prefere um plano único, bem pesquisado, a várias rodadas de perguntas — só perguntar
  quando a ambiguidade é genuinamente dele resolver (ex.: trade-off de arquitetura), não para
  destravar pesquisa que dá pra fazer sozinho.
- Sempre rodar `dotnet test SDK\CentralHub.SDK.Tests\...` depois de qualquer mudança e confirmar
  100% verde antes de considerar uma fase concluída — é o gate que prova zero regressão no
  hardware homologado.
- Ao compilar o Backend, checar primeiro se há um processo `CentralHub.Api` rodando (pode ter
  hardware real conectado) — se sim, buildar com `-o <pasta temporária>` em vez de derrubar o
  processo.
- Este projeto é um **laboratório de homologação** (MVP técnico) que será incorporado depois a um
  sistema de Portaria Virtual maior — não é para adicionar funcionalidade de negócio além do que
  foi pedido.
