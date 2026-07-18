# 15 — GLOSSÁRIO

> **Público-alvo:** qualquer pessoa que encontre um termo desconhecido em qualquer um dos outros 14
> documentos e precise de uma definição rápida, sem jargão adicional.

---

## Índice alfabético

[A](#a) · [B](#b) · [C](#c) · [D](#d) · [E](#e) · [F](#f) · [G](#g) · [I](#i) · [J](#j) · [K](#k) ·
[M](#m) · [N](#n) · [P](#p) · [Q](#q) · [R](#r) · [S](#s) · [T](#t) · [W](#w) · [Z](#z)

---

## A

**ACK** — sigla de "acknowledgement" (confirmação). No contexto do protocolo JFL, uma resposta que
confirma o recebimento de um comando, sem necessariamente carregar dados adicionais.

**ActiveNet** — software oficial da JFL usado para programação local de centrais Active 100 Bus
(usuários, zonas, configurações). Atua como cliente TCP que se conecta na central, papel oposto ao
do CentralHub (que atua como servidor). Ver [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md).

**API REST** — estilo de interface de programação onde recursos são acessados via HTTP (`GET`,
`POST`, etc.) usando URLs previsíveis, retornando tipicamente JSON.

**ASP.NET Core** — framework da Microsoft para construir aplicações web e APIs em C#/.NET, usado
pelo `Backend/CentralHub.Api`.

**Async/Await** — padrão de programação assíncrona em C# que permite esperar uma operação (ex.:
leitura de rede) sem bloquear a thread.

## B

**BCD (Binary-Coded Decimal)** — técnica de codificação onde cada 4 bits (nibble) de um byte
representa um único dígito decimal (0-9), em vez de o byte inteiro representar um número binário
puro. Usada no protocolo JFL para campos como o número de série.

**Backend** — a camada do projeto responsável pela API REST, persistência e hospedagem do servidor
TCP (`Backend/CentralHub.Api`).

**Bateria** — um dos itens reportados pelo comando de Status (0x4D); indica o estado da bateria
reserva da central (ex.: baixa, normal, ausente).

## C

**CAB** — "cabeçalho": o primeiro byte de todo pacote do protocolo JFL, fixo em `0x7B`, usado para
sincronizar o início de um pacote no fluxo de bytes recebido.

**CentralHub** — nome deste projeto: um sistema que atua como servidor TCP para centrais de alarme
JFL, expõe uma API REST e uma interface web para monitoramento/operação.

**Checksum** — valor calculado a partir dos bytes de um pacote, usado para detectar corrupção de
dados. Neste protocolo, é um XOR (`CheckSum8 Xor`) de todos os bytes anteriores do pacote.

**CMD** — campo do pacote JFL que identifica qual comando está sendo enviado/respondido (ex.:
`0x21` para conexão, `0x4D` para status).

**ConcurrentDictionary** — estrutura de dados do .NET, thread-safe, usada pelo `SessionManager` para
armazenar sessões ativas de forma segura mesmo com múltiplas threads acessando ao mesmo tempo.

## D

**DADOS** — campo do pacote JFL que carrega o conteúdo específico daquele comando (o "payload").

**DI (Dependency Injection / Injeção de Dependência)** — padrão onde uma classe recebe suas
dependências (outros serviços) de fora, em vez de criá-las internamente — usado extensivamente no
`Program.cs` do Backend.

**DTO (Data Transfer Object)** — classe simples usada para transportar dados entre camadas (ex.:
entre o `Service` e o `Controller`, ou entre o Backend e o Frontend via JSON).

## E

**Eletrificador** — dispositivo que aplica uma cerca elétrica de choque em um perímetro; a central
Active 100 Bus reporta seu estado como parte do comando de Status.

**EF Core (Entity Framework Core)** — ORM (mapeador objeto-relacional) da Microsoft, usado pelo
Backend para acessar o banco SQLite através de classes C# em vez de SQL manual.

**EnsureCreated()** — método do EF Core que cria as tabelas do banco se elas não existirem, mas
**não** aplica alterações a tabelas já existentes — diferente de Migrations. Ver
[`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md).

**Evento** — no protocolo JFL, um aviso que a central envia por conta própria (comando `0x24`)
quando algo acontece (disparo, abertura, etc.) — hoje implementado apenas como stub neste projeto.

## F

**Firewall** — mecanismo de filtragem de tráfego de rede; no Windows, controla, entre outras
coisas, se conexões TCP de entrada são permitidas ou bloqueadas por padrão.

**Firmware** — o software embarcado que roda dentro da central de alarme (não é o mesmo que o
software do CentralHub); a versão homologada neste projeto foi a `6.5`.

**Frontend** — a camada do projeto responsável pela interface web (React/TypeScript), em
`Frontend/`.

## G

**Gateway** — dispositivo/ponto de rede que interliga duas redes diferentes, permitindo que
pacotes IP viajem de uma para a outra.

## I

**ICMP** — protocolo usado por ferramentas como `ping`; não é o mesmo protocolo que TCP, e uma regra
de firewall pode permitir um e bloquear o outro.

**IHostedService / BackgroundService** — abstrações do ASP.NET Core para rodar processos de longa
duração (como o `JflTcpServer`) junto com o ciclo de vida da aplicação web.

**IP (Internet Protocol)** — protocolo de endereçamento que identifica um dispositivo em uma rede
(ex.: `10.0.250.21`).

## J

**JflSession** — classe do SDK que representa uma conexão TCP ativa com uma central específica,
incluindo seus metadados e mecanismos de correlação de requisição/resposta.

**JflTcpServer** — classe do SDK responsável por escutar a porta TCP e aceitar conexões de entrada
das centrais.

## K

**Keep-Alive** — comando (`0x40`) que a central envia periodicamente para indicar que a conexão
continua viva, mesmo sem eventos acontecendo.

## M

**MAC (Media Access Control)** — endereço físico único de uma interface de rede (ex.:
`8C:4F:00:0A:73:48`), diferente do endereço IP (que pode mudar).

**MOD** — byte do protocolo JFL que identifica o modelo do equipamento (`0xA4` para Active 100
Bus).

**Monitoramento (central de)** — serviço/empresa que recebe eventos de alarme de múltiplas centrais
remotamente, tipicamente 24 horas por dia.

## N

**NumeroSerie** — identificador único e permanente de uma central física; usado como chave real
para vincular sessões TCP ao cadastro correto no banco de dados.

## P

**Partição** — um agrupamento lógico de zonas dentro de uma central, que pode ser armado/desarmado
independentemente das outras partições. A Active 100 Bus suporta até 16.

**Payload** — o conteúdo de dados de um pacote, excluindo cabeçalho e checksum (equivalente ao
campo `DADOS` no protocolo JFL).

**PGM (Programmable Output / Saída Programável)** — uma saída da central de alarme que pode ser
acionada remotamente, tipicamente ligada a um relé (portão, sirene, iluminação). A Active 100 Bus
suporta até 16.

**Polling** — técnica de atualização onde o cliente consulta o servidor repetidamente em intervalos
fixos (ex.: `setInterval`), em vez de o servidor empurrar (push) atualizações.

**Protocolo** — conjunto de regras que define como dois sistemas trocam dados (formato dos pacotes,
ordem das mensagens, significado de cada campo).

## Q

**QDE** — campo do pacote JFL que indica a quantidade de bytes de dados presentes naquele pacote.

## R

**Receptora** — equipamento/software que recebe eventos de alarme via linha telefônica ou rede,
tipicamente em uma central de monitoramento (contexto histórico do setor de alarmes, anterior a
soluções IP puras como o CentralHub).

## S

**SEQ** — campo do pacote JFL usado para correlacionar uma resposta à requisição correspondente,
permitindo múltiplos comandos "em voo" simultaneamente.

**SessionManager** — classe do SDK responsável por registrar, buscar e remover sessões ativas de
centrais conectadas. Ver [`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md).

**SignalR** — biblioteca do ASP.NET Core para comunicação em tempo real (WebSocket e alternativas),
ainda não usada neste projeto — ver [`14_ROADMAP.md`](14_ROADMAP.md).

**SQLite** — sistema de banco de dados relacional onde todo o banco vive em um único arquivo, sem
processo de servidor separado.

**Stub** — implementação mínima/placeholder de uma funcionalidade, que reconhece a entrada mas não
processa o conteúdo de verdade — usado neste projeto para comandos ainda não implementados.

## T

**Tamper** — sensor que detecta violação física de um equipamento (ex.: abertura da tampa de um
sensor ou da própria central); reportado como uma das flags de problema no comando de Status.

**TaskCompletionSource** — classe do .NET que permite criar uma `Task` que é completada
manualmente por outro trecho de código — usada para correlacionar uma requisição enviada com a
resposta recebida de forma assíncrona.

**TCP (Transmission Control Protocol)** — protocolo de rede orientado a conexão, que garante
entrega ordenada e confiável de bytes entre dois pontos — a base de toda a comunicação entre
central e CentralHub.

**TcpClient / TcpListener** — classes do .NET; `TcpListener` escuta uma porta e aceita conexões
(usado pelo servidor), `TcpClient` representa uma conexão TCP já estabelecida (usado em ambas as
pontas).

## W

**WebSocket** — protocolo que permite comunicação bidirecional persistente entre cliente e
servidor web, alternativa ao polling — ver [`14_ROADMAP.md`](14_ROADMAP.md).

## Z

**Zona** — um ponto de detecção individual conectado à central (ex.: um sensor de porta, um sensor
de movimento). A Active 100 Bus suporta até 99.

---

## Checklist

- [ ] Sei onde procurar quando encontrar um termo desconhecido em qualquer documento deste
      conjunto.
- [ ] Sei distinguir termos de protocolo (SEQ, CAB, checksum) de termos de negócio (zona, partição,
      PGM) e de termos de infraestrutura (TCP, firewall, EF Core).

---

**Documento anterior:** [`14_ROADMAP.md`](14_ROADMAP.md)
**Próximo documento:** não há — este é o último documento de conteúdo.
**Índice geral:** [`INDEX.md`](INDEX.md)
