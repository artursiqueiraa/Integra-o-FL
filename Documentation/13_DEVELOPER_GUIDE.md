# 13 — DEVELOPER GUIDE

> **Público-alvo:** um desenvolvedor que acabou de receber acesso a este repositório e precisa,
> sozinho, colocar o ambiente para funcionar do zero — compilar, rodar, testar, publicar, restaurar
> um backup — sem depender de ninguém explicar nada verbalmente.

---

## Índice

1. [Pré-requisitos de ambiente](#1-pré-requisitos-de-ambiente)
2. [Estrutura de pastas na raiz](#2-estrutura-de-pastas-na-raiz)
3. [Como abrir o projeto](#3-como-abrir-o-projeto)
4. [Como compilar o Backend/SDK](#4-como-compilar-o-backendsdk)
5. [Como executar o Backend](#5-como-executar-o-backend)
6. [Como executar o Frontend](#6-como-executar-o-frontend)
7. [Como rodar os testes](#7-como-rodar-os-testes)
8. [Como configurar a porta do servidor JFL](#8-como-configurar-a-porta-do-servidor-jfl)
9. [Como configurar o firewall](#9-como-configurar-o-firewall)
10. [Como criar uma EF Core Migration de verdade](#10-como-criar-uma-ef-core-migration-de-verdade)
11. [Como publicar (build de produção)](#11-como-publicar-build-de-produção)
12. [Como fazer/restaurar um backup completo](#12-como-fazerrestaurar-um-backup-completo)
13. [Fluxo de trabalho recomendado no dia a dia](#13-fluxo-de-trabalho-recomendado-no-dia-a-dia)
14. [Boas práticas](#14-boas-práticas)
15. [Problemas comuns](#15-problemas-comuns)
16. [FAQ](#16-faq)
17. [Checklist](#17-checklist)

---

## 1. Pré-requisitos de ambiente

| Ferramenta | Versão | Para quê |
|---|---|---|
| .NET SDK | 9.x | Compilar/rodar Backend e SDK |
| Node.js | 18+ (LTS recomendado) | Compilar/rodar Frontend |
| npm | (vem com o Node) | Gerenciar dependências do Frontend |
| PowerShell | 5.1+ (Windows) | Scripts e comandos deste guia |
| Editor | Visual Studio, VS Code ou Rider | Qualquer um compatível com .csproj/.sln |

Confirme as versões instaladas:
```powershell
dotnet --version
node --version
npm --version
```

## 2. Estrutura de pastas na raiz

```
central/
├── CentralHub.sln                  ← solução .NET (abre tudo de uma vez)
├── Backend/
│   └── CentralHub.Api/             ← API REST + JflTcpServer hospedado
├── SDK/
│   ├── CentralHub.SDK/             ← protocolo JFL puro (sem dependência de banco/web)
│   └── CentralHub.SDK.Tests/       ← testes unitários do protocolo
├── Frontend/                       ← aplicação React/TypeScript/Vite
├── Documentation/                  ← esta pasta de documentação
└── Backups/                        ← backups completos gerados manualmente
```

## 3. Como abrir o projeto

- **Backend/SDK:** abra `CentralHub.sln` na raiz — carrega os 3 projetos .NET de uma vez
  (`CentralHub.Api`, `CentralHub.SDK`, `CentralHub.SDK.Tests`).
- **Frontend:** abra a pasta `Frontend/` separadamente no seu editor (é um projeto Node
  independente, não faz parte da `.sln`).

## 4. Como compilar o Backend/SDK

Da raiz do repositório:
```powershell
dotnet build CentralHub.sln
```
Isso restaura pacotes NuGet automaticamente e compila os 3 projetos. Erros de compilação aparecem
no console com o caminho do arquivo e a linha exatos.

## 5. Como executar o Backend

```powershell
cd Backend\CentralHub.Api
dotnet run
```
Ao iniciar com sucesso, você deve ver, entre outras linhas:
```
info: CentralHub.SDK.Jfl.Server.JflTcpServer[0]
      Aguardando conexoes na porta 8085...
info: Microsoft.Hosting.Lifetime[0]
      Now listening on: http://localhost:5000
```
A API REST fica disponível em `http://localhost:5000` (ajustável em
`Backend/CentralHub.Api/appsettings.json` / `launchSettings.json`); o servidor TCP para as centrais
sobe junto, na porta configurada (ver seção 8).

## 6. Como executar o Frontend

```powershell
cd Frontend
npm install
npm run dev
```
O Vite normalmente sobe em `http://localhost:5173`. Confirme que a URL base da API configurada no
frontend (Axios `baseURL`, ver [`09_WEB_GUIDE.md`](09_WEB_GUIDE.md)) aponta para onde o Backend
está realmente rodando.

## 7. Como rodar os testes

```powershell
dotnet test CentralHub.sln
```
Ou, para rodar só os testes do SDK:
```powershell
dotnet test SDK\CentralHub.SDK.Tests\CentralHub.SDK.Tests.csproj
```
Os testes cobrem o parser do protocolo, o checksum, e cada comando implementado, usando exemplos
reais extraídos do manual da JFL e de capturas de hardware real — ver
[`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md) para a origem desses exemplos.

## 8. Como configurar a porta do servidor JFL

A porta é lida da configuração do Backend (`appsettings.json`, seção correspondente ao
`JflServerOptions` — ver [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md)). Altere o valor lá e
reinicie o Backend para aplicar.

## 8.1 Como configurar CORS (origens permitidas do Frontend)

### Por que isso existe

O navegador bloqueia, por padrão, qualquer chamada `fetch`/Axios feita por uma página em uma
origem (`http://localhost:5173`, por exemplo) contra uma API rodando em outra origem
(`http://localhost:5000`) — a menos que o servidor responda com os cabeçalhos CORS dizendo
explicitamente "essa origem pode me chamar". O Backend precisa declarar, ele mesmo, a lista de
origens que confia.

**Armadilha comum:** o Vite (servidor de dev do Frontend) sobe por padrão em `5173`, mas se essa
porta já estiver ocupada (outro `npm run dev` esquecido rodando, outra ferramenta usando a porta),
ele sobe automaticamente em `5174`, `5175`, etc. Se o Backend só confia em `5173` (hardcoded), toda
chamada feita a partir do Vite rodando em `5174` é bloqueada pelo navegador com um erro de CORS —
mesmo com o Backend saudável e respondendo normalmente. Foi exatamente isso que aconteceu neste
projeto até a correção descrita abaixo.

### Como está configurado hoje

As origens permitidas **não ficam mais hardcoded em `Program.cs`** — ficam nos arquivos de
configuração por ambiente, e o `Program.cs` só lê o que estiver lá:

- [`appsettings.Development.json`](../Backend/CentralHub.Api/appsettings.Development.json):
  ```json
  {
    "Cors": {
      "AllowedOrigins": [
        "http://localhost:5173",
        "http://localhost:5174"
      ]
    }
  }
  ```
- [`appsettings.Production.json`](../Backend/CentralHub.Api/appsettings.Production.json):
  ```json
  {
    "Cors": {
      "AllowedOrigins": []
    }
  }
  ```

O ASP.NET Core já carrega o arquivo certo sozinho, sem código adicional: `WebApplication.CreateBuilder`
lê `appsettings.json` e depois `appsettings.{ASPNETCORE_ENVIRONMENT}.json` por cima (o `launchSettings.json`
do projeto já define `ASPNETCORE_ENVIRONMENT=Development` para `dotnet run` local — ver seção 5). Em
`Program.cs`, a política de CORS é montada lendo essa seção:

```csharp
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

`WithOrigins(...)` só aceita uma lista explícita — nunca usa `AllowAnyOrigin()`. Isso significa que
**se `appsettings.Production.json` estiver com a lista vazia (o padrão atual, já que este projeto
ainda não tem um domínio de produção definido), o CORS bloqueia todas as origens em produção** —
comportamento intencional e seguro (fail-closed), não um bug. Antes de publicar de verdade, edite
`appsettings.Production.json` e coloque a(s) URL(s) reais do Frontend publicado, por exemplo:
```json
{ "Cors": { "AllowedOrigins": ["https://central.suaempresa.com.br"] } }
```

### Antes × Depois

| | Antes | Depois |
|---|---|---|
| Onde a origem era definida | Hardcoded em `Program.cs`: `WithOrigins("http://localhost:5173")` | `Cors:AllowedOrigins` em `appsettings.Development.json` / `appsettings.Production.json`, lida dinamicamente |
| Porta 5174 do Vite | Bloqueada por CORS | Permitida |
| Produção | Mesma lista de dev (ou exigiria editar `Program.cs` e recompilar) | Lista própria, isolada, editável sem recompilar |
| `AllowAnyOrigin()` | Não usado | Continua não usado |

### Se precisar adicionar mais uma porta/origem de dev

Edite `appsettings.Development.json` e acrescente a URL na lista `Cors:AllowedOrigins` — não precisa
tocar em `Program.cs`.

### Swagger continua funcionando?

Sim — o Swagger (`app.UseSwagger()`/`UseSwaggerUI()`) é servido pela própria API, na mesma origem que
você acessa no navegador (`http://localhost:5000/swagger`), então não é uma chamada cross-origin e
nunca passou pela política de CORS, antes ou depois desta mudança. Validado manualmente após a
alteração: `swagger.json` e `swagger/index.html` respondem `200` normalmente em Development.

## 9. Como configurar o firewall

Se o Backend roda em uma máquina Windows e a central está em outra máquina/rede, é necessário
liberar entrada na porta TCP configurada:
```powershell
New-NetFirewallRule -DisplayName "CentralHub JFL 8085" -Direction Inbound -Protocol TCP -LocalPort 8085 -Action Allow -Profile Any
```
Contexto completo do porquê isso é necessário (e como foi descoberto) em
[`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md) e
[`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md).

## 10. Como criar uma EF Core Migration de verdade

O projeto atualmente usa `EnsureCreated()`, que **não** migra tabelas existentes (ver
[`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md)). Para migrar para Migrations formais:

```powershell
cd Backend\CentralHub.Api
dotnet tool install --global dotnet-ef   # se ainda não tiver a ferramenta instalada
dotnet ef migrations add NomeDaMigracao
dotnet ef database update
```
Depois disso, `EnsureCreated()` deve ser substituído por `Database.Migrate()` no código de inicialização — essa
substituição ainda não foi feita neste projeto (ver [`14_ROADMAP.md`](14_ROADMAP.md)).

## 11. Como publicar (build de produção)

**Backend:**
```powershell
cd Backend\CentralHub.Api
dotnet publish -c Release -o .\publish
```
Gera um binário pronto para rodar em `./publish` (ex.: `dotnet .\publish\CentralHub.Api.dll`).

**Frontend:**
```powershell
cd Frontend
npm run build
```
Gera os arquivos estáticos finais em `Frontend/dist/`, prontos para servir por qualquer servidor
HTTP estático (ou embutir no próprio Backend, se desejado no futuro).

## 12. Como fazer/restaurar um backup completo

Um backup completo (não apenas `git clone`) já foi feito uma vez, em
`Backups/Backup_Hardware_Homologado_2026-07-13_19-37/`, contendo cópia integral de Backend/,
Frontend/, SDK/, banco SQLite, documentação, `appsettings`, solução, testes, e um `BACKUP_INFO.md`
descrevendo data/hora/motivo/funcionalidades homologadas naquele momento.

**Para criar um novo backup**, siga o mesmo padrão: crie uma pasta
`Backups/Backup_<Motivo>_<AAAA-MM-DD_HH-mm>/`, copie manualmente (não via git) as pastas
`Backend/`, `Frontend/`, `SDK/`, o arquivo do banco SQLite, `Documentation/`, e escreva um
`BACKUP_INFO.md` novo explicando o motivo do backup e o estado funcional do sistema naquele
momento.

**Para restaurar**, copie o conteúdo da pasta de backup de volta sobre a estrutura do projeto atual
— **sempre confira primeiro** se há trabalho não commitado que seria perdido nesse processo.

## 13. Fluxo de trabalho recomendado no dia a dia

```
1. dotnet build CentralHub.sln         → confirma que compila
2. dotnet test CentralHub.sln          → confirma que nada quebrou
3. dotnet run (Backend)                → sobe API + servidor TCP
4. npm run dev (Frontend, outro terminal) → sobe interface web
5. Testar manualmente no navegador (http://localhost:5173)
6. Se alterar algo no protocolo/comandos → sempre rodar os testes do SDK de novo
```

## 14. Boas práticas

- Nunca altere `Handshake`/`Parser`/`Checksum`/`KeepAlive` sem rodar toda a suite de testes do SDK
  antes e depois — são a base de tudo o que já foi homologado contra hardware real.
- Ao adicionar um campo em um `Model` do Backend, lembre da limitação do `EnsureCreated()` (seção
  10) — não assuma que o banco vai se atualizar sozinho.
- Prefira sempre testar mudanças de protocolo contra os exemplos reais documentados em
  [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md) antes de considerar "pronto".

## 15. Problemas comuns

| Sintoma | Causa provável | Solução |
|---|---|---|
| `dotnet build` falha por SDK não encontrado | .NET SDK não instalado/versão errada | Instalar .NET SDK 9.x |
| Frontend não conecta na API | `baseURL` do Axios apontando para porta/host errado | Conferir `Frontend/src` — ver doc 09 |
| Central não conecta | Porta TCP não liberada no firewall | Ver seção 9 deste documento |
| Campo novo do Model não aparece no banco | `EnsureCreated()` não migra tabelas existentes | Ver seção 10 deste documento |
| Dois `dotnet run` do Backend ao mesmo tempo | Porta já em uso | Encerrar a instância anterior antes de rodar de novo |
| Frontend recebe erro de CORS no console do navegador | Vite subiu em uma porta (ex.: `5174`) que não está na lista `Cors:AllowedOrigins` do Backend | Ver seção 8.1 — adicionar a porta em `appsettings.Development.json` |

## 16. FAQ

**P: Preciso do Visual Studio, especificamente, para desenvolver este projeto?**
R: Não — qualquer editor com suporte a .NET (VS Code + extensão C#, Rider, etc.) funciona
perfeitamente com a `.sln`.

**P: Existe um Dockerfile?**
R: Não neste momento — execução é feita diretamente via `dotnet run`/`npm run dev`. Ver
[`14_ROADMAP.md`](14_ROADMAP.md) para possíveis melhorias futuras de empacotamento.

## 17. Checklist

- [ ] Consigo compilar o Backend/SDK com `dotnet build`.
- [ ] Consigo rodar os testes com `dotnet test`.
- [ ] Consigo subir o Backend e o Frontend simultaneamente e ver a interface funcionando.
- [ ] Sei liberar a porta do servidor JFL no firewall.
- [ ] Sei a limitação do `EnsureCreated()` e como contorná-la.
- [ ] Sei onde e como criar um backup completo do projeto.

---

**Documento anterior:** [`12_FAQ.md`](12_FAQ.md)
**Próximo documento:** [`14_ROADMAP.md`](14_ROADMAP.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
