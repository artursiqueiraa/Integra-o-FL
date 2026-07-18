# 10 — HOW TO ADD A NEW COMMAND

> **Público-alvo:** um desenvolvedor que precisa implementar um comando novo (Armar, Desarmar,
> Eventos, Usuários, ou qualquer outro). Este é um tutorial passo a passo, seguindo exatamente o
> mesmo padrão usado para implementar Status (0x4D) e PGM (0x50/0x51) — os dois exemplos reais já
> existentes no projeto.

---

## Índice

1. [Antes de começar: dois tipos de comando](#1-antes-de-começar-dois-tipos-de-comando)
2. [Passo a passo — comando "servidor pergunta, central responde" (ex.: Armar)](#2-passo-a-passo--comando-servidor-pergunta-central-responde-ex-armar)
3. [Passo a passo — comando "central avisa, servidor reage" (ex.: Eventos)](#3-passo-a-passo--comando-central-avisa-servidor-reage-ex-eventos)
4. [Exemplo completo: implementando Armar/Desarmar](#4-exemplo-completo-implementando-armardesarmar)
5. [Exemplo completo: implementando Eventos](#5-exemplo-completo-implementando-eventos)
6. [Exemplo completo: implementando Usuários](#6-exemplo-completo-implementando-usuários)
7. [Ordem correta das alterações](#7-ordem-correta-das-alterações)
8. [Cuidados e armadilhas](#8-cuidados-e-armadilhas)
9. [Checklist de implementação de um comando novo](#9-checklist-de-implementação-de-um-comando-novo)
10. [Casos de uso reais](#10-casos-de-uso-reais)
11. [Boas práticas](#11-boas-práticas)
12. [Problemas comuns](#12-problemas-comuns)
13. [Como testar](#13-como-testar)
14. [Como depurar](#14-como-depurar)
15. [FAQ](#15-faq)
16. [Checklist final](#16-checklist-final)

---

## 1. Antes de começar: dois tipos de comando

Todo comando do protocolo JFL se encaixa em um destes dois padrões:

**Tipo A — "Servidor pergunta, central responde"** (como Status e PGM): o CentralHub decide, a
qualquer momento, enviar um comando, e espera ativamente a resposta correlacionada por SEQ. Armar,
Desarmar, Inibir Zonas e Consultar/Programar Usuário se encaixam aqui.

**Tipo B — "Central avisa, servidor reage"** (como o Handshake e o Keep-Alive): a central toma a
iniciativa de mandar algo, sem que o servidor tenha pedido, e o servidor só precisa responder (ou
simplesmente registrar). Eventos (`0x24`) se encaixa aqui.

## 2. Passo a passo — comando "servidor pergunta, central responde" (ex.: Armar)

Siga exatamente o padrão de `PgmCommandService` (o exemplo mais recente e mais simples do
projeto):

1. **Confirmar o formato exato do comando no manual oficial da JFL** (pasta `Documentação JFL
   Alarmes/`) — nunca implemente "de memória" ou por suposição.
2. **Adicionar o `CMD` no enum `JflCommand`** (se ainda não existir) —
   [`JflCommand.cs`](../SDK/CentralHub.SDK/Jfl/Protocol/JflCommand.cs). (Armar/Desarmar já
   existem lá: `Armar = 0x4E`, `Desarmar = 0x4F`.)
3. **Criar um novo Service no SDK**, em `SDK/CentralHub.SDK/Jfl/Server/`, seguindo o modelo de
   `PgmCommandService.cs`: recebe `SessionManager` e `ILogger` no construtor, tem um método
   público por operação, que:
   - Valida a entrada (ex.: número de partição entre 1 e 16).
   - Chama `_sessionManager.TryGet(numeroSerie, ...)` — se não achar, devolve "offline".
   - Chama `session.SendAndWaitAsync(cmd, dados, timeout, cancellationToken)`.
   - Decodifica a resposta reaproveitando `CentralStatusResponse.Parse` (se o comando responder no
     formato "tela monitorar" — a maioria dos comandos de superusuário responde assim).
   - Confirma sucesso comparando o estado esperado com o estado observado na resposta.
   - Loga cada etapa (comando enviado, SEQ, bytes, tempo de resposta, resultado).
4. **Registrar o Service novo** em
   [`JflServiceCollectionExtensions.cs`](../SDK/CentralHub.SDK/Jfl/JflServiceCollectionExtensions.cs):
   `services.AddSingleton<SeuServicoNovo>();`
5. **Criar o Service correspondente no Backend**, em `Backend/CentralHub.Api/Services/`, seguindo
   o modelo de `PgmService.cs`: resolve a `Central` pelo `Id`, valida `NumeroSerie`, chama o
   Service do SDK, mapeia o resultado para um DTO, traduz falhas em `BusinessException` com o
   código HTTP certo (409 para offline, 502 para timeout/resposta inválida, 400 para entrada
   inválida).
6. **Criar os DTOs** em `Backend/CentralHub.Api/DTOs/` (formato de entrada/saída da API).
7. **Adicionar o endpoint** em `CentralController.cs`, seguindo o padrão das rotas absolutas
   `~/api/centrais/{id}/...` já usadas para status e PGM.
8. **Registrar o Service novo do Backend** em `Program.cs`:
   `builder.Services.AddScoped<SeuServicoNovoDoBackend>();`
9. **Escrever testes** (ver seção 13) antes de considerar a implementação concluída.
10. **Atualizar a Interface Web**, se aplicável (nova tela ou novo controle numa tela existente).
11. **Atualizar esta documentação** (`Documentation/`) refletindo o comando novo — incluindo,
    obrigatoriamente, no [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md) e no
    [`14_ROADMAP.md`](14_ROADMAP.md).

## 3. Passo a passo — comando "central avisa, servidor reage" (ex.: Eventos)

1. Confirmar o formato no manual oficial.
2. Se já existe um handler *stub* para esse comando (verifique em
   `SDK/CentralHub.SDK/Jfl/Server/Handlers/Stubs/`), **substitua o stub por uma implementação
   real** — não crie um handler duplicado.
3. O novo handler deve implementar `IJflCommandHandler` de verdade (`CanHandle` + `HandleAsync`),
   decodificando o payload, executando a lógica de negócio (ex.: gravar o evento no banco), e —
   se o protocolo exigir uma resposta (o comando de Evento exige: OK/ERRO + contador) — chamar
   `session.ReplyAsync(...)` ecoando o SEQ recebido.
4. **Remova o registro do stub antigo** e registre o handler novo em
   `JflServiceCollectionExtensions.cs`.
5. Se a lógica de negócio precisa gravar algo no banco (muito provável, para Eventos), crie uma
   nova tabela/Model no Backend (ver [`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md) para o
   padrão), e um mecanismo de "ponte" do SDK para o Backend — reaproveite o padrão de eventos do
   `SessionManager` (`SessaoRegistrada`, etc. — ver
   [`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md)) se fizer sentido, ou crie um
   evento novo equivalente, específico para "evento recebido".

## 4. Exemplo completo: implementando Armar/Desarmar

Este é um exemplo **guiado, mas não implementado** — mostra exatamente que código escrever, sem
executá-lo (a decisão de implementar de verdade fica para quando for priorizado — ver
[`14_ROADMAP.md`](14_ROADMAP.md)).

```csharp
// SDK/CentralHub.SDK/Jfl/Server/ArmCommandService.cs  (arquivo NOVO)

public enum ArmCommandFailureReason { CentralOffline, Timeout, RespostaInvalida, ParticaoInvalida }

public sealed class ArmCommandResult
{
    public required bool Sucesso { get; init; }
    public PartitionState? EstadoConfirmado { get; init; }
    public ArmCommandFailureReason? Motivo { get; init; }
    public string? Erro { get; init; }
}

public sealed class ArmCommandService
{
    private readonly SessionManager _sessionManager;
    private readonly ILogger<ArmCommandService> _logger;
    private static readonly TimeSpan TimeoutPadrao = TimeSpan.FromSeconds(10);

    public ArmCommandService(SessionManager sessionManager, ILogger<ArmCommandService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public Task<ArmCommandResult> ArmarAsync(string numeroSerie, int particao, CancellationToken ct) =>
        EnviarAsync(numeroSerie, JflCommand.Armar, particao, ct);

    public Task<ArmCommandResult> DesarmarAsync(string numeroSerie, int particao, CancellationToken ct) =>
        EnviarAsync(numeroSerie, JflCommand.Desarmar, particao, ct);

    private async Task<ArmCommandResult> EnviarAsync(string numeroSerie, JflCommand cmd, int particao, CancellationToken ct)
    {
        if (particao is < 1 or > 16)
            return new ArmCommandResult { Sucesso = false, Motivo = ArmCommandFailureReason.ParticaoInvalida, Erro = "Particao deve estar entre 1 e 16." };

        if (!_sessionManager.TryGet(numeroSerie, out var session) || session is null)
            return new ArmCommandResult { Sucesso = false, Motivo = ArmCommandFailureReason.CentralOffline, Erro = $"Central {numeroSerie} offline." };

        var dados = new byte[] { (byte)particao };
        var resposta = await session.SendAndWaitAsync((byte)cmd, dados, TimeoutPadrao, ct);
        var status = CentralStatusResponse.Parse(resposta.Dados);
        var estadoParticao = status.Particoes.FirstOrDefault(p => p.Numero == particao)?.Estado;

        // "Armada" ou "ArmadaStay" contam como sucesso ao armar; "Desarmada" ao desarmar.
        var confirmado = cmd == JflCommand.Armar
            ? estadoParticao is PartitionState.Armada or PartitionState.ArmadaStay
            : estadoParticao is PartitionState.Desarmada;

        return confirmado
            ? new ArmCommandResult { Sucesso = true, EstadoConfirmado = estadoParticao }
            : new ArmCommandResult { Sucesso = false, Motivo = ArmCommandFailureReason.RespostaInvalida, Erro = "Central nao confirmou o novo estado." };
    }
}
```

O restante (Service do Backend, DTOs, endpoint no Controller) segue **exatamente** o padrão de
`PgmService`/`PgmDtos`/`CentralController` — trocando "PGM" por "Partição" e os comandos `0x50`/
`0x51` por `0x4E`/`0x4F`.

## 5. Exemplo completo: implementando Eventos

Diferente de Armar/PGM: o gatilho é a **central**, não o servidor. O handler
`EventoCommandHandlerStub` hoje só loga; a implementação real precisaria:

```csharp
// SDK/CentralHub.SDK/Jfl/Server/Handlers/EventoCommandHandler.cs  (substituiria o stub)

public sealed class EventoCommandHandler : IJflCommandHandler
{
    public bool CanHandle(byte cmd) => cmd == (byte)JflCommand.Evento;

    public async Task HandleAsync(JflSession session, JflPacket packet, CancellationToken ct)
    {
        var evento = EventoRequest.Parse(packet.Dados);   // classe nova, decodificando Contact ID etc.

        // TODO: notificar o Backend (ex.: via um evento C# no estilo SessionManager.SessaoRegistrada,
        // para o Backend gravar o evento numa tabela nova "Events").

        // O protocolo exige resposta confirmando o recebimento:
        var respostaDados = new byte[] { 0x01, /* 4 bytes do contador, ecoados */ };
        await session.ReplyAsync(packet, packet.Cmd, respostaDados, ct);
    }
}
```

O ponto mais delicado aqui é que este handler roda **dentro do laço de leitura da sessão** — ele
não pode bloquear por muito tempo (ex.: esperando uma escrita lenta no banco), sob risco de
atrasar o processamento de outros pacotes daquela mesma central. Considerar disparar a gravação em
segundo plano (fire-and-forget controlado, como já é feito em
`JflSessionPersistenceService` — ver [`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md)).

## 6. Exemplo completo: implementando Usuários

Comandos de usuário (consultar `0xC8`, programar `0xC9`) são "filhos" do envelope `0x37`
("comandos com senha") — diferente de Armar/PGM (que são comandos de superusuário diretos), estes
exigem: (a) enviar a senha do usuário dentro do payload, em formato BCD; (b) tratar os códigos de
erro específicos documentados na seção 5 do manual oficial (`0xA0` a `0xAC` — pacote inválido,
senha errada, sem permissão, etc.) — um formato de resposta **diferente** do "tela monitorar"
usado por Status/PGM/Armar. Isso significa que este comando **não pode reaproveitar
`CentralStatusResponse.Parse`** — precisa de um parser próprio para a resposta curta
`0x37 0x03 0xC0 RESP`.

## 7. Ordem correta das alterações

```
1. Ler o manual oficial da seção correspondente.
2. SDK: criar/editar o handler ou o Service novo.
3. SDK: escrever os testes unitários do handler/Service (sem sockets reais).
4. SDK: rodar dotnet test — confirmar que tudo passa, incluindo os testes JÁ existentes
   (nenhuma regressão).
5. Backend: criar o Service que faz a ponte com o banco.
6. Backend: criar os DTOs.
7. Backend: adicionar o endpoint no Controller.
8. Backend: registrar tudo em Program.cs.
9. Rodar dotnet build da solução inteira.
10. Testar manualmente contra uma central real (ou simulada, ver 13_DEVELOPER_GUIDE.md)
    antes de considerar pronto.
11. Frontend, se aplicável.
12. Atualizar esta documentação.
```

## 8. Cuidados e armadilhas

- **Nunca reintroduza o modelo de conexão de saída** (`AdapterFactory`/`JflAdapter`) para um
  comando novo — todo comando novo deve usar `SessionManager`/`SendAndWaitAsync`.
- **Nunca assuma que a resposta de um comando novo usa o formato "tela monitorar"** — confirme no
  manual; comandos com senha (`0x37`) usam um formato de resposta diferente (seção 6 acima).
- **Cuidado com handlers que bloqueiam o laço de leitura** — qualquer operação lenta (banco de
  dados, chamada externa) dentro de um `IJflCommandHandler.HandleAsync` atrasa o processamento de
  *todos* os próximos pacotes daquela sessão.
- **Nunca esqueça de validar a entrada antes de gastar uma tentativa de rede** — como
  `PgmCommandService` faz com o número da PGM (1-16), sempre valide localmente antes de acionar
  `SendAndWaitAsync`.

## 9. Checklist de implementação de um comando novo

- [ ] Confirmei o formato exato no manual oficial (não assumi nada).
- [ ] O `CMD` já existe em `JflCommand.cs`, ou eu adicionei.
- [ ] Criei/editei o Service ou Handler no SDK, seguindo o padrão existente.
- [ ] Registrei tudo em `JflServiceCollectionExtensions.cs`.
- [ ] Escrevi testes unitários novos.
- [ ] `dotnet test` passa, sem regressão nos testes já existentes.
- [ ] Criei o Service, DTOs e endpoint no Backend.
- [ ] Registrei tudo em `Program.cs`.
- [ ] `dotnet build` da solução inteira passa sem erros.
- [ ] Testei manualmente (real ou simulado).
- [ ] Atualizei `08_COMMANDS_GUIDE.md` e `14_ROADMAP.md`.

## 10. Casos de uso reais

Os dois exemplos reais e completos de "comando implementado do zero seguindo este padrão" são:
Status (0x4D) e PGM (0x50/0x51) — ambos podem ser lidos, arquivo por arquivo, como referência viva.

## 11. Boas práticas

Ver seção 8 (cuidados). Adicionalmente: sempre nomear métodos/classes em português, seguindo a
convenção já estabelecida em todo o projeto (`SessionManager` é uma exceção histórica de nome em
inglês, mas `Registrar`, `Remover`, `EnviarComandoAsync` etc. são em português) — consistência
facilita a leitura para quem vem depois.

## 12. Problemas comuns

- **"Meu handler novo nunca é chamado"** — verifique se ele foi registrado em
  `JflServiceCollectionExtensions.AddJflServer` e se `CanHandle` está retornando `true` para o
  `CMD` certo.
- **"Meu comando novo trava para sempre"** — verifique se você está usando
  `SendAndWaitAsync` (que tem timeout embutido) e não uma espera manual sem limite de tempo.

## 13. Como testar

Escreva testes seguindo `SDK/CentralHub.SDK.Tests/Server/PgmCommandServiceTests.cs` como modelo —
usando `DuplexMemoryStream` (ver `TestUtilities/`) para simular uma sessão sem precisar de sockets
reais, e `SessionManager.TryCompletePendingRequest` para simular a resposta da central.

## 14. Como depurar

Adicione logs em todo passo importante do handler/Service novo, seguindo o nível de detalhe já
usado em `PgmCommandService` (SEQ, bytes enviados/recebidos, tempo de resposta, resultado).

## 15. FAQ

**P: Preciso implementar Armar/Desarmar juntos, ou posso fazer só um?**
R: Tecnicamente pode fazer um de cada vez, mas como os dois compartilham praticamente todo o
código (só o `CMD` e a condição de sucesso mudam), o mais eficiente é implementar os dois juntos,
como o exemplo da seção 4 mostra.

**P: Como sei se um comando novo deveria reaproveitar `CentralStatusResponse`?**
R: Se a seção do manual disser "Resposta: Ver item 4.10", sim. Se descrever um formato de resposta
próprio e diferente (como os comandos com senha, seção 5 do manual), não.

## 16. Checklist final

- [ ] Sei diferenciar comandos "servidor pergunta" de comandos "central avisa".
- [ ] Sei os 12 passos da ordem correta de implementação.
- [ ] Sei quais armadilhas evitar.
- [ ] Sei onde estão os dois exemplos reais completos para copiar o padrão.

---

**Documento anterior:** [`09_WEB_GUIDE.md`](09_WEB_GUIDE.md)
**Próximo documento:** [`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
