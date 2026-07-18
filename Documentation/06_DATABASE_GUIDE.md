# 06 — DATABASE GUIDE

> **Público-alvo:** alguém que nunca viu o banco de dados deste projeto — inclui uma explicação
> do zero sobre o que é SQLite e o que é um ORM, antes de entrar nas tabelas propriamente ditas.

---

## Índice

1. [O que é o banco de dados deste projeto](#1-o-que-é-o-banco-de-dados-deste-projeto)
2. [O que é SQLite (do zero)](#2-o-que-é-sqlite-do-zero)
3. [O que é Entity Framework Core / EnsureCreated (do zero)](#3-o-que-é-entity-framework-core--ensurecreated-do-zero)
4. [Diagrama de relacionamento entre tabelas](#4-diagrama-de-relacionamento-entre-tabelas)
5. [Tabela: Buildings](#5-tabela-buildings)
6. [Tabela: Centrals](#6-tabela-centrals)
7. [Tabela: CentralSessions](#7-tabela-centralsessions)
8. [Tabela: Histories](#8-tabela-histories)
9. [Por que cada campo existe — perguntas e respostas por campo](#9-por-que-cada-campo-existe--perguntas-e-respostas-por-campo)
10. [Diferença entre Central e CentralSession (fonte comum de confusão)](#10-diferença-entre-central-e-centralsession-fonte-comum-de-confusão)
11. [Casos de uso reais (consultas)](#11-casos-de-uso-reais-consultas)
12. [Boas práticas](#12-boas-práticas)
13. [Problemas comuns](#13-problemas-comuns)
14. [Como testar/inspecionar o banco](#14-como-testarinspecionar-o-banco)
15. [Como depurar problemas de dados](#15-como-depurar-problemas-de-dados)
16. [Limitações do SQLite para produção](#16-limitações-do-sqlite-para-produção)
17. [FAQ](#17-faq)
18. [Checklist](#18-checklist)

---

## 1. O que é o banco de dados deste projeto

O CentralHub guarda informação persistente (que sobrevive a reinícios do servidor) em um único
arquivo: `Backend/CentralHub.Api/centralhub.db`. Esse arquivo é criado automaticamente na primeira
vez que o Backend roda — não é preciso instalar nenhum programa de banco de dados separado (como
seria necessário com, por exemplo, PostgreSQL ou SQL Server).

## 2. O que é SQLite (do zero)

A maioria dos bancos de dados relacionais que você já deve ter ouvido falar (PostgreSQL, MySQL, SQL
Server) funcionam como um **programa servidor separado**: você instala o banco, ele fica escutando
numa porta de rede própria, e sua aplicação se conecta nele como cliente — exatamente o mesmo
modelo cliente-servidor explicado em
[`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md).

**SQLite é diferente: é uma biblioteca, não um servidor.** O banco de dados inteiro — todas as
tabelas, todos os dados — fica guardado dentro de **um único arquivo** no disco (no nosso caso,
`centralhub.db`). Quando a aplicação C# quer ler ou escrever algo, ela não faz uma chamada de rede
— ela abre esse arquivo diretamente, como abriria qualquer outro arquivo. Isso torna o SQLite
extremamente simples de usar em desenvolvimento (não precisa instalar nada, não precisa configurar
usuário/senha de banco) — mas tem limitações importantes para produção em larga escala, discutidas
na seção 16.

Você vai ver, junto ao `centralhub.db`, dois arquivos auxiliares: `centralhub.db-wal` e
`centralhub.db-shm`. Fazem parte de um mecanismo do SQLite chamado **WAL (Write-Ahead Log)**, que
melhora a performance de escrita — não precisa se preocupar com eles no dia a dia, mas **não devem
ser apagados manualmente enquanto o banco principal existir** (contêm dados ainda não
consolidados no arquivo principal).

## 3. O que é Entity Framework Core / EnsureCreated (do zero)

Escrever comandos SQL manualmente (`SELECT * FROM Centrals WHERE ...`) funciona, mas é repetitivo
e propenso a erros de digitação que só aparecem em tempo de execução. Um **ORM** (Object-Relational
Mapper) é uma biblioteca que permite trabalhar com o banco usando classes e objetos da própria
linguagem de programação, e ele traduz isso para SQL por baixo dos panos.

**Entity Framework Core** (EF Core) é o ORM oficial da Microsoft para .NET, usado neste projeto.
Cada tabela do banco corresponde a uma classe C# em `Backend/CentralHub.Api/Models/` (chamadas de
"entidades"), e o `AppDbContext.cs` é a classe que amarra tudo isso junto.

Este projeto usa um método chamado **`EnsureCreated()`** (chamado uma vez, em `Program.cs`, ao
iniciar o Backend) — ele verifica se o arquivo do banco já existe com as tabelas certas; se não
existir, cria tudo do zero, baseado nas classes de `Models/`. **Importante: `EnsureCreated()` NÃO
migra um banco já existente quando um campo novo é adicionado numa classe** — ele só cria do zero
se o banco ainda não existe. Isso é diferente do mecanismo de "Migrations" do EF Core (mais
robusto, usado em projetos maiores) — este projeto, por ser um MVP, optou pela abordagem mais
simples. As implicações práticas disso (o que fazer quando um campo novo é adicionado ao código)
estão na seção 16 e em [`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md).

## 4. Diagrama de relacionamento entre tabelas

```
┌─────────────────┐
│   Buildings       │   ("Prédios" — o imóvel onde uma ou mais centrais estão instaladas)
│───────────────────│
│ Id (PK)            │
│ Nome                │
│ Descricao           │
└─────────┬─────────┘
          │ 1
          │
          │ N
┌─────────▼─────────┐
│   Centrals          │   (o cadastro de uma central de alarme específica)
│───────────────────│
│ Id (PK)              │
│ Nome                  │
│ IP, Porta, Usuario,   │  ← campos LEGADOS (arquitetura antiga de discagem de saída)
│ Senha                 │
│ BuildingId (FK)        │
│ Fabricante, Modelo,    │
│ Firmware, Status,      │
│ Latencia               │
│ NumeroSerie ★          │  ← chave real de correlação com a sessão TCP viva
│ UltimoKeepAliveEmUtc   │
│ UltimoIpConectado      │
│ ConectadoDesdeUtc      │
└──────┬──────────┬────┘
       │ 1         │ 1
       │           │
       │ N         │ N
┌──────▼──────┐ ┌──▼────────────────┐
│  Histories    │ │  CentralSessions    │  (histórico de CADA conexão TCP que já existiu)
│──────────────│ │────────────────────│
│ Id (PK)        │ │ Id (PK)              │
│ Data            │ │ NumeroSerie           │
│ CentralId (FK)  │ │ CentralId (FK,        │
│ PGM             │ │   opcional/nulo)      │
│ Comando         │ │ Imei, Mac, Modelo,     │
│ Resultado       │ │   ModeloNome,          │
│ (LEGADO — só     │ │   VersaoFirmware       │
│  usado pela tela │ │ EnderecoRemoto         │
│  "Operação"       │ │ Status (enum)          │
│  antiga)          │ │ ConectadaEmUtc         │
└─────────────────┘ │ UltimoKeepAliveEmUtc    │
                     │ DesconectadaEmUtc       │
                     └───────────────────────┘
```

## 5. Tabela: Buildings

Representa um "prédio" (ou casa, loja, condomínio — qualquer imóvel) onde uma ou mais centrais de
alarme estão fisicamente instaladas. É a unidade de organização mais alta do sistema.

| Campo | Tipo | Obrigatório? | Descrição |
|---|---|---|---|
| `Id` | inteiro | Sim (gerado automaticamente) | Identificador único. |
| `Nome` | texto (até 150 caracteres) | Sim | Nome do prédio, para exibição (ex.: "Loja Centro"). |
| `Descricao` | texto (até 500 caracteres) | Não | Campo livre para anotações. |

## 6. Tabela: Centrals

O cadastro de uma central de alarme específica. É a tabela mais importante do sistema — tudo mais
gira em torno dela.

| Campo | Tipo | Obrigatório? | Descrição |
|---|---|---|---|
| `Id` | inteiro | Sim | Identificador único, usado nas URLs da API (ex.: `/api/centrais/5/status`). |
| `Nome` | texto (150) | Sim | Nome de exibição da central (ex.: "Central Loja Centro"). |
| `IP` | texto (45) | Não* | **Legado** — endereço IP do antigo fluxo de discagem de saída, cujo endpoint (`testar-conexao`) **foi removido**. Não tem relação com o IP real observado da central conectando (esse é `UltimoIpConectado`). |
| `Porta` | inteiro (1-65535) | Não* | **Legado** — idem. |
| `Usuario` | texto (100) | Não* | **Legado** — nunca usado de fato por nenhum adapter real. |
| `Senha` | texto (200) | Não* | **Legado** — nunca é retornada pela API (por segurança), nunca usada de fato. |
| `BuildingId` | inteiro (chave estrangeira) | Sim | A qual prédio esta central pertence. |
| `Fabricante` | texto (100) | Não | Nome do fabricante — preenchido automaticamente para `"JFL"` quando a central conecta de verdade pela primeira vez. |
| `Modelo` | texto (100) | Não | Nome do modelo (ex.: `"Active 100 Bus"`), atualizado a cada conexão real. |
| `Firmware` | texto (100) | Não | Versão do firmware (ex.: `"6.5"`), atualizado a cada conexão real. |
| `Status` | texto (50) | Não | `"Online"` ou `"Offline"` — atualizado automaticamente pelo `JflSessionPersistenceService` a cada conexão/desconexão real. |
| `Latencia` | inteiro longo | Não | **Legado** — usado pelo antigo `KeepAliveService` (hoje desregistrado); não é mais atualizado. |
| `NumeroSerie` | texto (10) | Não, mas **único** quando preenchido | ★ O campo mais importante desta tabela — é a chave que correlaciona esta linha do banco com uma sessão TCP viva no `SessionManager`. |
| `UltimoKeepAliveEmUtc` | data/hora | Não | Quando foi o último keep-alive (0x40) recebido de verdade desta central. |
| `UltimoIpConectado` | texto (45) | Não | O IP real, observado na última conexão TCP aceita — diferente do campo legado `IP` (que é digitado manualmente e nunca confirmado). |
| `ConectadoDesdeUtc` | data/hora (pode ser nulo) | Não | Quando a sessão *atualmente* ativa começou; fica `null` quando a central está offline — usado para calcular "Tempo Conectado" na tela. |

\* `IP`/`Porta`/`Usuario`/`Senha` continuam colunas `NOT NULL` no schema (com padrão
`string.Empty`/`0`) — nenhuma migração foi feita. A mudança foi só na camada de API: os DTOs de
criação/atualização passaram a aceitar esses campos como opcionais (sem `[Required]`), e
`CentralService` aplica o padrão vazio ao gravar quando omitidos. Ver
[`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md).

## 7. Tabela: CentralSessions

Um **histórico** de cada conexão TCP que já aconteceu — ao contrário da tabela `Centrals` (que
guarda só o estado *atual*), esta tabela acumula uma linha nova a cada vez que uma central conecta,
e marca essa linha como "desconectada" quando a sessão termina. Serve como um log de auditoria de
conectividade.

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | inteiro | Identificador único. |
| `NumeroSerie` | texto (10) | Número de série reportado nesta sessão específica. |
| `CentralId` | inteiro (FK, pode ser nulo) | A qual `Central` cadastrada esta sessão pertence — **fica nulo se a central conectou antes de existir um cadastro correspondente** (ver seção 10). |
| `Imei`, `Mac` | texto | Identificadores de hardware reportados no handshake. |
| `Modelo` | inteiro pequeno (byte) | O valor bruto do campo `MOD` do handshake. |
| `ModeloNome` | texto (50) | O nome já traduzido (ex.: "Active 100 Bus"). |
| `VersaoFirmware` | texto (20) | Versão de firmware reportada. |
| `EnderecoRemoto` | texto (64) | IP:porta observados nesta conexão específica. |
| `Status` | enum (`Conectada` / `Desconectada`) | Se esta sessão específica ainda está viva. |
| `ConectadaEmUtc` | data/hora | Quando esta sessão começou. |
| `UltimoKeepAliveEmUtc` | data/hora | Última atividade registrada nesta sessão. |
| `DesconectadaEmUtc` | data/hora (nulo) | Quando esta sessão terminou (nulo enquanto ainda ativa). |

## 8. Tabela: Histories

> ⚠️ **Legada** — usada apenas pela tela "Operação" antiga (`OperationPage.tsx` /
> `OperationController` / `OperationService`), que envia comandos de PGM **simulados** (nunca
> conversa de verdade com uma central). Os comandos de PGM reais, enviados pela Tela Central
> (`CentralDetailPage`/`PgmPanel`), **não gravam nesta tabela** — eles são registrados via logs
> estruturados (ver [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md)), não no banco de dados.

| Campo | Tipo | Descrição |
|---|---|---|
| `Id` | inteiro | Identificador único. |
| `Data` | data/hora | Quando o comando (simulado) foi "enviado". |
| `CentralId` | inteiro (FK) | Para qual central. |
| `PGM` | inteiro | Número da PGM. |
| `Comando` | texto (50) | `"Pulso"`, `"Ligar"` ou `"Desligar"`. |
| `Resultado` | texto (500) | Texto simulado (ex.: `"PGM 1 ligado"`) — nunca reflete um resultado real de hardware. |

## 9. Por que cada campo existe — perguntas e respostas por campo

- **Por que `NumeroSerie` e não `IP` é usado para achar a sessão de uma central?** Porque o IP de
  uma central pode mudar a qualquer momento (reconexão de celular, DHCP renovando um endereço,
  etc.) — o número de série é a única identidade **permanente** do equipamento. Explicado em
  detalhe em [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md).
- **Por que existe `Status` em `Centrals` E `Status` em `CentralSessions`?** O de `Centrals` é
  sempre "o estado mais recente conhecido, resumido" (útil para listar rapidamente todas as
  centrais numa tela); o de `CentralSessions` é "o histórico completo de cada conexão", útil para
  auditoria (ex.: "quantas vezes esta central caiu no mês passado?").
- **Por que `UltimoIpConectado` é diferente de `IP`?** `IP` é um campo digitado manualmente pelo
  operador (legado, do modelo antigo onde o CentralHub discaria para a central); `UltimoIpConectado`
  é o endereço real observado quando a central conecta de verdade — a fonte confiável.
- **Por que `ConectadoDesdeUtc` pode ser nulo?** Porque só faz sentido quando a central está online
  agora; quando ela desconecta, o campo é explicitamente limpo (`null`) para não mostrar um "tempo
  conectado" desatualizado e enganoso.

## 10. Diferença entre Central e CentralSession (fonte comum de confusão)

`Central` é **o cadastro** — existe mesmo que a central nunca tenha conectado nenhuma vez.
`CentralSession` é **um registro histórico de uma conexão real** — só existe depois que uma central
efetivamente conectou pelo menos uma vez.

Uma nuance importante, descoberta e documentada durante a homologação real: **se uma central
conecta antes de ser cadastrada no CentralHub, aquela sessão específica nasce com `CentralId =
null`** (porque, no momento da conexão, não havia nenhum cadastro correspondente para vincular). Se
você cadastrar a `Central` **depois**, a sessão antiga não é religada retroativamente — só a
**próxima** conexão (reconexão) vai nascer já vinculada corretamente. Isso é uma limitação
conhecida e documentada, não um bug.

## 11. Casos de uso reais (consultas)

**"Quais centrais estão online agora?"**
```sql
SELECT Nome, NumeroSerie, UltimoIpConectado FROM Centrals WHERE Status = 'Online';
```

**"Quantas vezes a central X desconectou no último mês?"**
```sql
SELECT COUNT(*) FROM CentralSessions
WHERE NumeroSerie = '2751484124' AND DesconectadaEmUtc >= date('now', '-30 days');
```

## 12. Boas práticas

- Nunca escreva diretamente na tabela `Centrals` os campos `Status`/`UltimoKeepAliveEmUtc`/
  `UltimoIpConectado`/`ConectadoDesdeUtc` manualmente — eles são de responsabilidade exclusiva do
  `JflSessionPersistenceService`; escrever manualmente vai ser sobrescrito na próxima
  conexão/desconexão de qualquer forma.
- Ao adicionar um campo novo em qualquer `Model`, sempre atualizar também o `DTO` correspondente e
  o mapeamento no `Service` (`ParaDto`) — o EF Core não faz isso automaticamente.

## 13. Problemas comuns

- **"Adicionei um campo na classe e ele não aparece no banco"** — porque `EnsureCreated()` não
  migra bancos existentes (ver seção 3). Solução: ou apagar o arquivo `.db` (perde todos os dados)
  ou rodar um `ALTER TABLE` manual (ver seção 14) — a segunda opção foi a usada durante o
  desenvolvimento real deste projeto, especificamente para não perder o cadastro da central
  homologada.
- **"A central está Online no `SessionManager` mas o banco mostra Offline"** — sintoma de a central
  ter conectado antes de existir cadastro (ver seção 10), ou de uma falha momentânea de
  persistência (ver logs de erro do `JflSessionPersistenceService`).

## 14. Como testar/inspecionar o banco

Como é um arquivo SQLite comum, pode ser aberto com qualquer ferramenta compatível (DB Browser for
SQLite, extensões de VS Code, ou por script). Exemplo em Python (usado durante o desenvolvimento
real deste projeto para inspecionar/migrar o banco sem precisar do Backend rodando):

```python
import sqlite3
con = sqlite3.connect("centralhub.db")
cur = con.cursor()
cur.execute("SELECT Id, Nome, NumeroSerie, Status FROM Centrals")
for row in cur.fetchall():
    print(row)
```

Para adicionar uma coluna nova sem perder dados (o que foi feito de verdade neste projeto ao
adicionar `ConectadoDesdeUtc`):

```python
cur.execute("ALTER TABLE Centrals ADD COLUMN ConectadoDesdeUtc TEXT NULL")
con.commit()
```

## 15. Como depurar problemas de dados

Habilitar o log de comandos SQL do EF Core (já habilitado por padrão no ambiente de
desenvolvimento deste projeto) mostra, no console, cada `SELECT`/`INSERT`/`UPDATE` executado,
com os parâmetros — extremamente útil para confirmar que um dado está sendo salvo como esperado.

## 16. Limitações do SQLite para produção

- **Concorrência de escrita limitada**: SQLite permite múltiplas leituras simultâneas, mas só uma
  escrita por vez no arquivo inteiro — para o volume atual (uma escrita por evento de
  conexão/keep-alive/comando) isso não é problema, mas se o número de centrais crescer muito
  (milhares, com keep-alives simultâneos), pode virar gargalo.
- **Um único arquivo, uma única máquina**: não há replicação nativa, backup contínuo, ou failover
  — para produção séria, migrar para PostgreSQL ou SQL Server é recomendado (ver
  [`14_ROADMAP.md`](14_ROADMAP.md)).
- **Sem migrations automatizadas**: como explicado na seção 3, mudanças de schema exigem
  intervenção manual — outro motivo para migrar para um banco com suporte robusto a Migrations do
  EF Core em produção.

## 17. FAQ

**P: Onde o arquivo do banco fica fisicamente?**
R: `Backend/CentralHub.Api/centralhub.db` (mais os arquivos auxiliares `-wal`/`-shm`), no mesmo
diretório onde o Backend roda.

**P: Posso apagar o banco e recomeçar do zero?**
R: Sim, tecnicamente (apagando os três arquivos, `.db`/`.db-wal`/`.db-shm`) — mas isso apaga TODOS
os dados, incluindo cadastros de centrais reais já homologadas. Sempre fazer backup antes (ver
[`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md), seção de backup).

**P: O campo `Senha` da central é criptografado?**
R: Não — é texto plano no banco, embora nunca seja devolvido pela API. Isso é uma limitação de
segurança conhecida, listada em [`14_ROADMAP.md`](14_ROADMAP.md).

## 18. Checklist

- [ ] Sei explicar a diferença entre SQLite e um banco cliente-servidor tradicional.
- [ ] Sei listar as 4 tabelas e o que cada uma representa.
- [ ] Sei explicar por que `NumeroSerie` é a chave real de correlação, não `IP`.
- [ ] Sei o que fazer quando um campo novo precisa ser adicionado sem perder dados existentes.

---

**Documento anterior:** [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md)
**Próximo documento:** [`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
