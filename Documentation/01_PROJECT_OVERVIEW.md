# 01 — PROJECT OVERVIEW

> **Público-alvo deste documento:** qualquer pessoa que nunca ouviu falar do CentralHub. Não é
> necessário saber nada sobre alarmes, redes ou programação para entender este documento.
> Ele é o ponto de partida de toda a documentação.

---

## Índice deste documento

1. [O que é o CentralHub](#1-o-que-é-o-centralhub)
2. [O problema que ele resolve](#2-o-problema-que-ele-resolve)
3. [Por que ele foi criado](#3-por-que-ele-foi-criado)
4. [Como ele surgiu (histórico do projeto)](#4-como-ele-surgiu-histórico-do-projeto)
5. [Objetivo do projeto](#5-objetivo-do-projeto)
6. [Arquitetura geral (visão de 30.000 pés)](#6-arquitetura-geral-visão-de-30000-pés)
7. [Tecnologias usadas e por quê](#7-tecnologias-usadas-e-por-quê)
8. [Fabricantes suportados hoje](#8-fabricantes-suportados-hoje)
9. [Fabricantes que poderão ser adicionados](#9-fabricantes-que-poderão-ser-adicionados)
10. [Glossário rápido dos termos usados neste documento](#10-glossário-rápido-dos-termos-usados-neste-documento)
11. [Problemas comuns de quem está começando a ler o projeto](#11-problemas-comuns-de-quem-está-começando-a-ler-o-projeto)
12. [FAQ deste documento](#12-faq-deste-documento)
13. [Checklist de entendimento](#13-checklist-de-entendimento)

---

## 1. O que é o CentralHub

O **CentralHub** é um software que fica no meio do caminho entre **centrais de alarme físicas**
(equipamentos instalados em prédios, casas, empresas — as caixinhas com sirene, teclado e fiação
de sensores que você já deve ter visto em algum lugar) e **as pessoas ou sistemas que precisam
saber o que está acontecendo com esses alarmes e comandar ações neles remotamente**, como ligar
uma sirene de teste, verificar se uma partição está armada, ou (no futuro) armar/desarmar o
sistema.

Ele é composto por três partes que trabalham juntas:

1. **Um servidor de rede (Backend)** — um programa que fica ligado 24 horas por dia, esperando
   que as centrais de alarme se conectem nele pela internet ou rede local, e conversando com
   elas usando o "idioma" (protocolo) que cada fabricante de central usa.
2. **Uma biblioteca de protocolo (SDK)** — o "dicionário e gramática" desse idioma: código que
   sabe montar e entender os pacotes de bytes que uma central de alarme específica (hoje, a
   **JFL Active 100 Bus**) envia e espera receber.
3. **Uma interface web (Frontend)** — uma página que um ser humano abre no navegador para
   cadastrar prédios, cadastrar centrais, ver se elas estão online, ver o status delas (zonas,
   partições, bateria...) e mandar comandos (hoje, ligar/desligar/pulsar saídas PGM).

Se você nunca trabalhou com alarmes: pense em uma central de alarme como um computador pequeno e
burro, dedicado, que fica dentro de uma caixa de metal na parede. Ele não tem tela, não tem
teclado de verdade (só um teclado numérico simples), e a única forma de "conversar" com ele à
distância é através de uma conexão de rede (cabo Ethernet ou chip de celular) usando um protocolo
binário específico do fabricante — não é HTTP, não é algo que um navegador entende.

O CentralHub existe para ser a ponte entre esse mundo de "caixinha de metal falando um protocolo
binário proprietário de 1990-e-alguma-coisa adaptado para redes IP" e o mundo moderno de
"aplicações web, APIs REST, bancos de dados relacionais e dashboards".

## 2. O problema que ele resolve

Imagine que uma empresa de monitoramento de alarmes (ou o setor de segurança de um condomínio,
ou de uma rede de lojas) precisa saber, a qualquer momento:

- Quais centrais de alarme estão online agora?
- Uma central específica está armada ou desarmada?
- Alguma zona está com problema (sensor sem comunicação, bateria fraca, sabotagem/tamper)?
- A bateria de backup está boa?
- A energia elétrica (AC) da central caiu?
- É possível acionar remotamente uma saída (PGM) — por exemplo, para abrir um portão, ligar uma
  luz, ou testar uma sirene?

Sem um software como o CentralHub, a única forma de saber essas coisas é: (a) ir fisicamente até
o local e olhar o teclado da central, ou (b) usar o software do próprio fabricante (no caso da
JFL, o **ActiveNet**), que é um programa de Windows feito para um técnico programar **uma**
central de cada vez, conectado diretamente nela pela rede local — não é feito para monitorar
dezenas ou centenas de centrais espalhadas em vários endereços, e não expõe os dados de forma que
outros sistemas (como um dashboard web, ou um sistema de portaria virtual) consigam consumir.

O CentralHub resolve isso construindo, do zero, o lado "estação de monitoramento" desse
ecossistema: um serviço que centenas de centrais podem se conectar simultaneamente, que guarda o
histórico de cada uma no banco de dados, e que expõe tudo isso por uma API REST comum (JSON sobre
HTTP) — o tipo de interface que qualquer sistema moderno (um app mobile, um dashboard React, um
sistema de portaria virtual, uma integração com outro software) sabe consumir.

## 3. Por que ele foi criado

O ponto de partida do projeto foi a constatação de que centrais de alarme JFL (e de outros
fabricantes) **falam um protocolo de rede próprio**, documentado pelo fabricante em um PDF técnico
("Protocolo de comunicação dos equipamentos JFL com a estação monitoramento"), mas **não existia
nenhum software open/customizável rodando esse protocolo do lado do servidor** dentro da
organização — só o ActiveNet (ferramenta do fabricante, fechada, pensada para instaladores, não
para operação de monitoramento em escala).

Construir o CentralHub significa ter controle total sobre:
- Como os dados das centrais são armazenados e por quanto tempo.
- Como esses dados são expostos (APIs próprias, formatos próprios).
- Como o sistema se integra com outros sistemas da empresa (portaria virtual, CFTV, controle de
  acesso, sistemas de chamado).
- Quais fabricantes são suportados — hoje JFL, mas a arquitetura foi pensada desde o início para
  suportar múltiplos fabricantes (veja a seção 8 e 9).

## 4. Como ele surgiu (histórico do projeto)

O projeto começou como um **MVP (Minimum Viable Product)** simples: uma tela para cadastrar
prédios, cadastrar centrais (com campos de IP, porta, usuário, senha) e enviar comandos de PGM.
Nessa primeira versão, a arquitetura assumia — de forma equivocada, como uma auditoria posterior
descobriria — que o **CentralHub deveria discar para a central** (abrir uma conexão TCP de saída
para o IP/porta cadastrado da central), no mesmo modelo que um navegador abre uma conexão para um
site.

Uma auditoria técnica completa da documentação oficial da JFL revelou que **esse modelo estava
architeturalmente invertido**: o protocolo real da JFL define que é **a central quem disca para
fora**, contra um servidor que a estação de monitoramento mantém escutando — o oposto do que a
primeira versão implementava. Esse achado motivou uma reescrita da camada de comunicação:

1. Construção de um **servidor TCP** de verdade (`JflTcpServer`), que fica escutando conexões
   de entrada.
2. Construção de um **gerenciador de sessões** (`SessionManager`), que mantém uma sessão viva por
   central conectada, correlacionada pelo **número de série** de cada equipamento (não pelo IP,
   já que centrais atrás de NAT/celular têm IP variável).
3. Implementação dos comandos oficiais do protocolo JFL, um de cada vez, com testes escritos
   contra **exemplos reais capturados do próprio manual do fabricante** (não inventados).
4. Uma **homologação com hardware real** — uma central Active 100 Bus física, firmware 6.5, foi
   conectada de verdade contra este software, provando que a implementação bate com o
   comportamento real do equipamento (não só com o que o manual diz).
5. Depois da homologação, construção da camada operacional (comandos de PGM: ligar, desligar,
   pulso) e da interface web para operar isso.

Esse histórico importa porque ele explica **por que existem, no código, vestígios da arquitetura
antiga** — um `AdapterFactory`, um `JflAdapter` que finge conversar com a central abrindo conexões
de saída — que **não fazem parte do caminho real usado hoje**. O endpoint `POST
/api/central/testar-conexao` e a tela "Testar Conexão" chegaram a existir nessa fase de transição,
mas **foram removidos** (não só marcados como legado) numa limpeza posterior, já que ambos
induziam ao erro descrito acima. O `OperationService` (fluxo simulado da tela "Operação",
alimentado por esses mesmos Adapters) **também foi removido** numa limpeza seguinte — a tela
"Operação" hoje chama `PgmService` diretamente, o mesmo caminho real da Tela Central. O restante do
`AdapterFactory`/`JflAdapter`/`IntelbrasAdapter` continua no código, agora marcado com `[Obsolete]`
e **sem nenhum consumidor real restante** — não foi apagado por precaução, não porque ainda seja
necessário. Um desenvolvedor novo precisa saber que esse código é legado e não deve ser copiado
como exemplo. Isso
está detalhado nos documentos [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md) e
[`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md) (este último cobre
especificamente a tela "Centrais" e o painel de monitoramento de sessão que substituiu o teste de
conexão).

## 5. Objetivo do projeto

Resumindo em uma frase: **transformar o protocolo binário proprietário que uma central de alarme
JFL fala pela rede em uma API REST e uma interface web modernas, mantendo compatibilidade byte a
byte com o protocolo oficial documentado pelo fabricante, validada contra hardware real.**

Objetivos concretos, em ordem de conquista:

| # | Objetivo | Status |
|---|----------|--------|
| 1 | Aceitar conexões TCP reais de centrais Active 100 Bus | ✅ Concluído e homologado |
| 2 | Decodificar o handshake de conexão (0x21) | ✅ Concluído e homologado |
| 3 | Manter a sessão viva via keep-alive (0x40) | ✅ Concluído e homologado |
| 4 | Consultar o status completo da central (0x4D) | ✅ Concluído e homologado |
| 5 | Vincular a sessão TCP a um cadastro de Central no banco | ✅ Concluído e homologado |
| 6 | Acionar/desacionar/pulsar PGMs (0x50/0x51) | ✅ Concluído e validado |
| 7 | Interface web para operar tudo isso | ✅ Concluído |
| 8 | Armar/desarmar/Stay/Away remotamente, incluindo eletrificador (0x4E/0x4F/0x53/0x54) | ✅ Concluído e validado (via simulador; hardware real pendente) |
| 9 | Inibir/desinibir zonas remotamente (0x52) | ✅ Concluído e validado (via simulador; hardware real pendente) |
| 10 | Receber eventos em tempo real (0x24) | ⏳ Não implementado (stub) |
| 11 | Gerenciar usuários da central remotamente | ⏳ Não implementado (stub) |
| 12 | Suportar outros fabricantes (ex.: Intelbras) de verdade | ⏳ Não implementado (só o "encaixe" existe) |

Veja o [`14_ROADMAP.md`](14_ROADMAP.md) para o detalhamento de tudo que falta.

## 6. Arquitetura geral (visão de 30.000 pés)

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         REDE DO CLIENTE (ex.: prédio)                     │
│                                                                            │
│   ┌────────────────────┐                                                  │
│   │  Central de Alarme  │   Equipamento físico instalado no local.        │
│   │  JFL Active 100 Bus │   Tem módulo Ethernet. Sabe abrir uma conexão   │
│   │  (módulo Ethernet)  │   TCP de SAÍDA para um IP/porta configurados.   │
│   └──────────┬──────────┘                                                  │
│              │ TCP de saída (a central é o CLIENTE)                       │
└──────────────┼─────────────────────────────────────────────────────────────┘
               │
               │  Internet / VPN / rede local, dependendo da instalação
               ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    SERVIDOR DO CENTRALHUB (esta aplicação)                │
│                                                                            │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │  JflTcpServer  (porta 8085 — configurável)                        │    │
│  │  Fica ESCUTANDO conexões de entrada. Quando uma central conecta,  │    │
│  │  nasce uma "Sessão" que vive enquanto a central estiver online.   │    │
│  └───────────────────────────┬──────────────────────────────────────┘    │
│                               │                                          │
│  ┌───────────────────────────▼──────────────────────────────────────┐    │
│  │  SessionManager                                                    │    │
│  │  Guarda todas as sessões ativas, indexadas pelo Número de Série   │    │
│  │  de cada central. É aqui que a API "encontra" uma central online. │    │
│  └───────────────────────────┬──────────────────────────────────────┘    │
│                               │                                          │
│  ┌───────────────────────────▼──────────────────────────────────────┐    │
│  │  API REST (ASP.NET Core)  — porta 5000                            │    │
│  │  Controllers → Services → banco de dados SQLite                   │    │
│  │  Ex.: GET /api/centrais/5/status, POST /api/centrais/5/pgm/3/ligar│    │
│  └───────────────────────────┬──────────────────────────────────────┘    │
│                               │                                          │
│  ┌───────────────────────────▼──────────────────────────────────────┐    │
│  │  Banco de dados SQLite (arquivo local)                             │    │
│  │  Tabelas: Buildings, Centrals, CentralSessions, Histories          │    │
│  └────────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────────┘
               ▲
               │  HTTP / JSON (REST comum, qualquer navegador entende)
               │
┌──────────────┴─────────────────────────────────────────────────────────────┐
│                    NAVEGADOR DO OPERADOR (Frontend React)                  │
│  Tela "Prédios", tela "Centrais", tela "Central" (status + PGM)            │
└──────────────────────────────────────────────────────────────────────────┘
```

Note a direção da seta entre a Central e o CentralHub: **a central é quem inicia a conexão**. Isso
é o ponto arquitetural mais importante de todo o projeto, e está explicado em profundidade no
documento [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md).

### Os três projetos de código

O código está organizado em três projetos independentes, dentro de uma única solução .NET
(`CentralHub.sln`):

```
central/
├── Backend/
│   └── CentralHub.Api/       → A API REST + banco de dados + servidor TCP hospedado aqui dentro
├── SDK/
│   ├── CentralHub.SDK/       → A biblioteca de protocolo (o "dicionário" do idioma JFL)
│   └── CentralHub.SDK.Tests/ → Testes automatizados da biblioteca acima
└── Frontend/
    └── src/                  → A interface web (React + TypeScript)
```

Por que separar SDK do Backend? Porque a lógica de "como montar um pacote 0x7B e calcular o
checksum" não tem nada a ver com "como salvar isso no SQLite" — são responsabilidades diferentes.
Essa separação também deixa a porta aberta para, no futuro, reaproveitar o SDK em outro tipo de
aplicação (por exemplo, uma ferramenta de linha de comando para testar centrais, sem precisar de
todo o Backend).

## 7. Tecnologias usadas e por quê

| Tecnologia | Onde é usada | Por quê |
|---|---|---|
| **C# / .NET 9** | Backend e SDK | Linguagem fortemente tipada, com bom suporte a programação assíncrona (`async`/`await`), essencial para lidar com centenas de conexões TCP simultâneas sem travar threads. Ecossistema ASP.NET Core maduro para APIs REST. |
| **ASP.NET Core** | Backend (API REST) | Framework web oficial da Microsoft para .NET; suporte nativo a Controllers, injeção de dependência, middlewares, Swagger. |
| **Entity Framework Core** | Backend (acesso a dados) | ORM (Object-Relational Mapper) — permite trabalhar com o banco de dados usando classes C# em vez de escrever SQL manualmente. |
| **SQLite** | Backend (banco de dados) | Banco de dados leve, em um único arquivo, sem precisar instalar um servidor de banco separado — ideal para um MVP e para desenvolvimento local. (Ver [`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md) sobre limitações e quando migrar.) |
| **xUnit** | SDK.Tests | Framework de testes automatizados padrão do ecossistema .NET moderno. |
| **TcpListener / TcpClient (`System.Net.Sockets`)** | SDK | Classes nativas do .NET para trabalhar com sockets TCP brutos — necessário porque o protocolo JFL não é HTTP, é um protocolo binário customizado que exige controle fino sobre os bytes que trafegam. |
| **React 18 + TypeScript** | Frontend | React é a biblioteca de interface mais usada do mercado; TypeScript adiciona tipagem estática ao JavaScript, pegando erros em tempo de compilação em vez de em produção. |
| **Vite** | Frontend (build tool) | Ferramenta de build moderna, rápida, usada para compilar e servir o projeto React durante o desenvolvimento. |
| **Material UI (MUI)** | Frontend (componentes visuais) | Biblioteca de componentes prontos (botões, tabelas, diálogos) seguindo o padrão visual "Material Design" do Google — evita reinventar componentes de UI do zero. |
| **Axios** | Frontend (chamadas HTTP) | Biblioteca para fazer requisições HTTP ao Backend a partir do navegador. |

## 8. Fabricantes suportados hoje

**Apenas JFL, e apenas o modelo Active 100 Bus, com firmware compatível com a versão 6.5 (a que
foi homologada), é suportado de verdade** — ou seja, com implementação real do protocolo binário,
testada contra o manual oficial e contra hardware físico.

O código tem uma pasta `SDK/CentralHub.SDK/Adapters/` com um `IntelbrasAdapter` e um `JflAdapter`
— mas **esses são vestígios da arquitetura antiga (pré-reescrita)**, marcados com `[Obsolete]`, e
são **100% simulados** (não implementam nenhum protocolo real). Não confunda a existência desses
arquivos com "suporte real a Intelbras". Veja [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md),
seção "Código legado", para o mapeamento completo do que é real e do que é vestígio.

## 9. Fabricantes que poderão ser adicionados

A arquitetura fundamental (servidor TCP central + `SessionManager` correlacionando sessões por
identificador único do equipamento) é genérica o bastante para suportar qualquer fabricante cujo
protocolo siga o modelo "o equipamento disca para o servidor de monitoramento" — que é o modelo
padrão da indústria de alarmes (não é uma peculiaridade da JFL). Candidatos razoáveis para uma
próxima integração, na ordem em que fazem mais sentido dado o esforço:

1. **Intelbras** — outro grande fabricante brasileiro de centrais de alarme, com um protocolo IP
   próprio (documentação separada, precisa ser obtida e auditada como foi feito com a JFL).
2. **Outros modelos JFL** que não a Active 100 Bus (ex.: Active 20, Active 32 Duo) — o protocolo
   documentado é o mesmo (mesmo cabeçalho `0x7B`), mas cada modelo tem capacidades diferentes
   (número de zonas, partições, PGMs), então o parser de status precisaria ser generalizado.

Para o passo a passo real de como adicionar um novo fabricante ou um novo comando, veja
[`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md).

## 10. Glossário rápido dos termos usados neste documento

> Um glossário muito mais completo está em [`15_GLOSSARY.md`](15_GLOSSARY.md). Aqui vão só os
> termos usados acima, para não obrigar a pular de documento imediatamente.

- **Central de alarme**: o equipamento físico instalado no local, que monitora sensores (portas,
  janelas, movimento) e pode acionar sirenes.
- **PGM**: uma saída elétrica programável da central, que pode ser usada para acionar qualquer
  coisa (um relé de portão, uma luz, um sinalizador) — não é exclusiva para sirene.
- **Zona**: um ponto de monitoramento da central (por exemplo, "Zona 1 = sensor da porta da
  frente").
- **Partição**: um agrupamento independente de zonas dentro da mesma central — permite armar/
  desarmar áreas diferentes do mesmo imóvel separadamente (ex.: "Partição 1 = loja",
  "Partição 2 = depósito").
- **Protocolo**: o conjunto de regras que definem como dois sistemas trocam informação — no nosso
  caso, quais bytes significam o quê.
- **Sessão TCP**: uma conexão de rede persistente entre dois computadores, que fica aberta por um
  tempo (ao contrário de uma requisição HTTP normal, que abre e fecha rapidinho).
- **API REST**: um jeito padronizado de expor funcionalidades de um sistema pela web, usando URLs
  e o formato JSON.

## 11. Problemas comuns de quem está começando a ler o projeto

- **"Cadê o botão 'Testar Conexão' que eu vi em prints antigos?"** — foi removido de propósito: ele
  tentava discar para a central (arquitetura invertida, ver seção 4), o que nunca funciona contra
  o modelo real. A tela "Centrais" mostra hoje um card "Status da Conexão" que só consulta a sessão
  TCP real, sem nunca abrir um socket. Veja [`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md)
  e [`12_FAQ.md`](12_FAQ.md).
- **"Cadastrei a central mas ela nunca fica Online"** — o cadastro no CentralHub só registra a
  intenção; quem precisa ser configurada para *discar* para o CentralHub é a **central física em
  si**, através do software ActiveNet (endereço de destino = IP do servidor CentralHub, porta =
  8085 por padrão). Veja [`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md).
- **"Por que existe um `AdapterFactory` que nunca é usado no fluxo real?"** — código legado, ver
  seção 4 acima e [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md).

## 12. FAQ deste documento

**P: O CentralHub substitui o ActiveNet?**
R: Não. O ActiveNet continua sendo necessário para a **programação/instalação inicial** da central
(configurar zonas, senhas, endereço IP de destino etc.), feita localmente por um técnico. O
CentralHub é o **monitoramento remoto contínuo** depois que a central já está instalada e
configurada.

**P: O CentralHub precisa de internet?**
R: A central precisa conseguir alcançar, pela rede, o endereço IP e a porta onde o CentralHub está
escutando. Isso pode ser pela internet (com o CentralHub hospedado em um servidor com IP público
ou atrás de um roteador com redirecionamento de porta) ou por uma rede local/VPN, dependendo de
como a infraestrutura do cliente é montada.

**P: Isso é um produto comercial pronto?**
R: Não — é um MVP (produto mínimo viável) com a integração JFL homologada contra hardware real e
uma primeira funcionalidade operacional (PGM) completa. Várias funcionalidades importantes (arme/
desarme, eventos em tempo real, autenticação de usuários da API) ainda não existem — veja
[`14_ROADMAP.md`](14_ROADMAP.md).

**P: Por que .NET e não outra linguagem?**
R: Escolha de projeto já estabelecida antes desta documentação ser escrita; .NET/C# oferece bom
equilíbrio entre performance, tipagem forte e produtividade para APIs REST + processamento de
sockets binários, que são as duas necessidades centrais deste projeto.

## 13. Checklist de entendimento

Antes de seguir para o próximo documento, você deveria conseguir responder "sim" para todas estas
perguntas:

- [ ] Eu sei explicar, em uma frase, o que o CentralHub faz.
- [ ] Eu sei que existem três projetos de código (Backend, SDK, Frontend) e por que eles são
      separados.
- [ ] Eu sei que a central de alarme é quem inicia a conexão de rede, não o CentralHub.
- [ ] Eu sei que só o modelo JFL Active 100 Bus é suportado de verdade hoje.
- [ ] Eu sei que existe código legado no projeto que não deve ser usado como referência.

Se todas as respostas forem "sim", siga para
[`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), onde o protocolo da JFL é explicado do
zero absoluto.

---

**Documento anterior:** nenhum (este é o primeiro)
**Próximo documento:** [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
