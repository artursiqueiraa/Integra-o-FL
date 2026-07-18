# 02 — JFL PROTOCOL GUIDE

> **Público-alvo deste documento:** alguém que nunca viu uma central de alarme, nunca ouviu falar
> de sockets TCP, e não sabe o que é um protocolo binário. Esta é a "aula" completa — depois de
> ler este documento, você deve entender exatamente como uma central JFL Active 100 Bus conversa
> com o CentralHub, byte por byte.

---

## Índice deste documento

1. [O que é uma central de alarme](#1-o-que-é-uma-central-de-alarme)
2. [O que é a Active 100 Bus](#2-o-que-é-a-active-100-bus)
3. [O que é um módulo Ethernet](#3-o-que-é-um-módulo-ethernet)
4. [O que é uma sessão TCP (explicado do zero)](#4-o-que-é-uma-sessão-tcp-explicado-do-zero)
5. [O que é um "software monitorador"](#5-o-que-é-um-software-monitorador)
6. [O que é o ActiveNet](#6-o-que-é-o-activenet)
7. [Programador × Monitorador × Receptora × Servidor × Cliente](#7-programador--monitorador--receptora--servidor--cliente)
8. [Quem conecta em quem, e por quê](#8-quem-conecta-em-quem-e-por-quê)
9. [A anatomia de um pacote do protocolo JFL](#9-a-anatomia-de-um-pacote-do-protocolo-jfl)
10. [O checksum, explicado do zero](#10-o-checksum-explicado-do-zero)
11. [O SEQ (byte de sequência), explicado do zero](#11-o-seq-byte-de-sequência-explicado-do-zero)
12. [O comando de conexão — 0x21 (Handshake)](#12-o-comando-de-conexão--0x21-handshake)
13. [O comando de Keep-Alive — 0x40](#13-o-comando-de-keep-alive--0x40)
14. [O comando de Evento — 0x24](#14-o-comando-de-evento--0x24)
15. [O comando de Pedir Status — 0x93](#15-o-comando-de-pedir-status--0x93)
16. [O comando de Status como superusuário — 0x4D](#16-o-comando-de-status-como-superusuário--0x4d)
17. [Os comandos de PGM — 0x50 e 0x51](#17-os-comandos-de-pgm--0x50-e-0x51)
18. [Tabela-resumo de todos os comandos citados](#18-tabela-resumo-de-todos-os-comandos-citados)
19. [Casos de uso reais](#19-casos-de-uso-reais)
20. [Boas práticas ao trabalhar com este protocolo](#20-boas-práticas-ao-trabalhar-com-este-protocolo)
21. [Problemas comuns](#21-problemas-comuns)
22. [Como testar](#22-como-testar)
23. [Como depurar](#23-como-depurar)
24. [FAQ](#24-faq)
25. [Checklist de entendimento](#25-checklist-de-entendimento)

---

## 1. O que é uma central de alarme

Uma central de alarme é um equipamento eletrônico instalado dentro de um imóvel (residencial ou
comercial), geralmente dentro de uma caixa metálica pregada na parede, próxima ao quadro de
energia. Fisicamente conectados a ela, existem:

- **Sensores de zona** — dispositivos espalhados pelo imóvel que detectam abertura de porta/
  janela (sensor magnético), movimento (sensor infravermelho — IR), quebra de vidro, fumaça, etc.
  Cada sensor está ligado a uma **zona** da central (a central "enxerga" cada zona como aberta,
  fechada, ou em curto/sem comunicação).
- **Teclado(s)** — onde um usuário digita a senha para armar/desarmar o sistema.
- **Sirene(s)** — o alarme sonoro que dispara quando uma zona armada é violada.
- **Bateria de backup** — para o sistema continuar funcionando mesmo se a energia elétrica (AC)
  cair.
- **Módulo de comunicação** — Ethernet, Wi-Fi, celular (GPRS/4G) ou linha telefônica, usado para
  a central "avisar" um software de monitoramento externo sobre o que está acontecendo.

A central é, na prática, um **microcontrolador embarcado** (um "computador" muito mais simples que
um PC, dedicado a uma única tarefa), rodando um firmware (o "sistema operacional" gravado nele
pelo fabricante) que:
1. Lê continuamente o estado de todas as zonas.
2. Decide, com base na programação (quais zonas estão armadas, em qual partição), se deve disparar
   o alarme.
3. Reporta tudo isso — eventos, status, problemas — para fora, via rede, usando um **protocolo
   próprio do fabricante**.

Esse último ponto — "protocolo próprio" — é a razão de existir de metade deste projeto. Não existe
um padrão universal que toda central de alarme do mundo fale (existem alguns padrões abertos, como
o **Contact ID**, usado só para uma parte da informação — o código do evento — mas o "transporte"
completo, incluindo comandos remotos, é proprietário de cada fabricante).

## 2. O que é a Active 100 Bus

A **Active 100 Bus** é um modelo específico de central de alarme fabricado pela **JFL Alarmes**
(fabricante brasileiro). Dentro da família de produtos JFL, ela é um dos modelos de maior
capacidade:

| Capacidade | Active 100 Bus |
|---|---|
| Partições | 16 |
| Zonas | 99 |
| Saídas PGM | 16 |
| Controle de eletrificador | Sim |
| Placa (identificação interna do fabricante) | PCI-350 |
| Byte de identificação no protocolo (campo `MOD`) | `0xA4` |

"Bus" no nome se refere à forma como as zonas são conectadas fisicamente (via um barramento
compartilhado, ao contrário de fiação ponto a ponto tradicional) — um detalhe de hardware que não
afeta o protocolo de rede documentado neste guia.

O CentralHub, hoje, só implementa (e só foi testado contra) este modelo específico. Outros modelos
JFL (Active 20, Active 32 Duo, Active 8W...) usam o **mesmo protocolo de rede** (mesmo cabeçalho
`0x7B`), mas têm capacidades diferentes (menos zonas, menos partições) — então, mesmo que a
conexão "funcione" com outro modelo, os campos de status (que têm tamanhos fixos calculados para
16 partições e 99 zonas) precisariam ser revisados. Veja
[`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md).

## 3. O que é um módulo Ethernet

"Módulo Ethernet" é a peça de hardware, dentro da central, responsável por colocá-la na rede local
via cabo de rede (RJ45) — o mesmo tipo de cabo que conecta um computador a um roteador. Esse
módulo:

- Recebe um endereço IP (fixo ou via DHCP) dentro da rede local do cliente.
- Sabe abrir conexões TCP de saída para um IP e porta configurados (o endereço do software de
  monitoramento — no nosso caso, o CentralHub).
- Também expõe, separadamente, uma porta local de configuração (usada pelo ActiveNet para
  programar a central diretamente — ver seção 6).

Sem o módulo Ethernet (ou um módulo equivalente de Wi-Fi/celular), a central não tem como se
comunicar remotamente — ela continuaria funcionando localmente (sirene, teclado), mas nenhum
software externo saberia o que está acontecendo com ela.

## 4. O que é uma sessão TCP (explicado do zero)

Para entender o protocolo JFL, primeiro é preciso entender o que é **TCP**.

Quando dois computadores (ou, no nosso caso, um computador e uma central de alarme) precisam
trocar dados pela rede de forma confiável — sem perder pedaços, na ordem certa — eles usam um
protocolo de transporte chamado **TCP** (Transmission Control Protocol). TCP garante que:

- Os bytes enviados chegam na mesma ordem em que foram enviados.
- Nenhum byte se perde (ou, se a conexão cair, o programa é avisado — não fica um "silêncio"
  ambíguo).

Para usar TCP, um dos dois lados precisa **escutar** (ficar esperando alguém se conectar) em um
número específico chamado **porta** — um número de 1 a 65535 que identifica "qual programa, dentro
daquele computador, deve receber a conexão" (o mesmo computador pode ter vários programas
escutando em portas diferentes ao mesmo tempo — por exemplo, o CentralHub escuta a porta **8085**
para as centrais e a porta **5000** para a API web, ao mesmo tempo, no mesmo processo).

O outro lado é quem **inicia** a conexão — chamamos isso de "discar", "conectar", ou, tecnicamente,
"abrir um socket cliente". Uma vez que a conexão é aceita pelo lado que escuta, os dois lados podem
trocar bytes em qualquer direção, quantas vezes quiserem, até que um dos dois feche a conexão (ou
ela caia por problema de rede).

Chamamos essa conexão aberta, de longa duração, de **sessão** — no nosso contexto, "a sessão de
uma central" é o período contínuo entre o momento em que ela conecta e o momento em que
desconecta.

```
   QUEM ESCUTA (SERVIDOR)                    QUEM CONECTA (CLIENTE)
   ─────────────────────                     ──────────────────────
   Fica esperando, numa porta                Decide, sozinho, quando
   específica, alguém bater à                conectar. Sabe o IP e a
   porta.                                    porta de quem escuta.

           ┌─────────┐                             ┌─────────┐
           │ ESCUTA   │◄────── conecta ─────────────│ DISCA    │
           │ porta X  │                             │          │
           └─────────┘                             └─────────┘
                │                                        │
                └──────────── troca de bytes ────────────┘
                     (nos dois sentidos, livremente,
                      enquanto a conexão existir)
```

No jargão de redes, quem escuta é chamado de **servidor**, e quem conecta é chamado de
**cliente** — mas atenção: esses termos descrevem só o papel *na abertura da conexão*. Depois que
a conexão está aberta, os dois lados podem enviar dados um para o outro livremente — "cliente" não
significa "só recebe", significa apenas "foi quem bateu à porta".

## 5. O que é um "software monitorador"

"Software monitorador" (ou "estação de monitoramento", ou "software receptor de eventos") é o
termo que o próprio manual da JFL usa para descrever o papel que **o CentralHub desempenha**:
um programa que fica sempre ligado, sempre escutando, pronto para receber a conexão de qualquer
central de alarme configurada para reportar a ele, e capaz de:

- Aceitar a conexão e autenticar a central pelo número de série.
- Receber eventos (disparos, arme, desarme, problemas) em tempo real.
- Responder a comandos de keep-alive para manter a conexão viva.
- Enviar comandos remotos para a central (status, PGM, e futuramente arme/desarme) *dentro* da
  mesma conexão que a central já abriu.

É exatamente o papel do `JflTcpServer` no CentralHub (ver
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md)).

## 6. O que é o ActiveNet

O **ActiveNet** é o software oficial da JFL, para Windows, usado por um **técnico instalador**
para programar uma central de alarme: cadastrar zonas, definir senhas de usuários, configurar
partições, e — o que mais nos interessa aqui — **configurar para qual IP e porta a central deve
reportar** (a tela "Comunicação → Monitoramento", com os campos `IP1`/`Porta1`, é onde se digita o
endereço do CentralHub).

O ActiveNet se conecta **diretamente na central**, pela rede local, numa porta de configuração
própria do módulo Ethernet — **esse é um protocolo diferente** do protocolo de monitoramento
(explicado a partir da seção 7), e o ActiveNet, nesse modo, é o **cliente**, a central é o
**servidor** — o oposto do fluxo de monitoramento! Isso é uma fonte comum de confusão, então vale
grifar:

> ⚠️ **Ao ser programada pelo ActiveNet, a central se comporta como servidor (o ActiveNet conecta
> nela). Ao reportar para o CentralHub, a mesma central se comporta como cliente (ela conecta no
> CentralHub).** São dois protocolos/portas diferentes, dois papéis diferentes, na mesma central
> física.

## 7. Programador × Monitorador × Receptora × Servidor × Cliente

Esta seção existe porque esses cinco termos aparecem espalhados pela documentação da JFL e do
próprio CentralHub, e são frequentemente confundidos.

| Termo | O que significa | Exemplo no nosso contexto |
|---|---|---|
| **Programador** | Software (ou tela dentro de um software) usado para *configurar* uma central — zonas, senhas, endereços. | O ActiveNet, no modo de conexão direta local. |
| **Monitorador** (ou "software de monitoramento", "estação de monitoramento") | Software que *recebe* a conexão da central para monitorar eventos/status em tempo real e enviar comandos remotos. | O CentralHub (`JflTcpServer`). |
| **Receptora (de eventos)** | Sinônimo de "monitorador" no jargão do setor — o sistema que "recebe" os eventos reportados pela central. | Também o CentralHub. |
| **Servidor** | Papel de rede: quem fica escutando, esperando conexões de entrada. | O CentralHub, na porta 8085 (para centrais) — **e também a própria central**, na porta local de configuração, quando o ActiveNet conecta nela. |
| **Cliente** | Papel de rede: quem inicia a conexão. | A central, ao reportar para o CentralHub — **e também o ActiveNet**, ao conectar na central para programá-la. |

O ponto-chave: **"servidor" e "cliente" não são propriedades fixas de um programa — são papéis que
dependem de qual conexão estamos falando.** A central de alarme é cliente numa conexão (a de
monitoramento, com o CentralHub) e servidor em outra (a de programação local, com o ActiveNet).

## 8. Quem conecta em quem, e por quê

Esta é a pergunta mais importante deste documento inteiro, porque foi a origem do maior erro
arquitetural do início do projeto (ver [`01_PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md), seção
4).

**A central de alarme conecta no software de monitoramento — nunca o contrário.** Isso está
explícito no manual oficial da JFL (seção 2.1 do documento "Protocolo de comunicação dos
equipamentos JFL com a estação monitoramento"):

> *"A estação de monitoramento provém um socket tipo servidor e disponibiliza para ser programado
> no equipamento JFL um endereço IP de destino e uma porta. O equipamento abre um socket de
> comunicação com esse servidor para iniciar a comunicação e transferência dos dados."*

### Por que a central conecta no software (e não o contrário)?

Porque, na imensa maioria das instalações reais, a central está atrás de uma rede que **não tem um
endereço IP público fixo e alcançável de fora** — ela pode estar atrás de um roteador doméstico
com NAT (que esconde endereços internos), pode estar usando um chip de celular (cujo IP muda a
cada reconexão da operadora), ou atrás de um firewall corporativo que bloqueia conexões de
entrada por padrão. Se o software de monitoramento tivesse que "adivinhar" o IP da central e
conectar nela, isso falharia na grande maioria dos casos.

O software de monitoramento, por outro lado, é tipicamente hospedado num servidor com endereço
estável e acessível (seja um servidor próprio da empresa de monitoramento, seja um serviço na
nuvem) — é muito mais fácil garantir que **um** endereço (o do servidor) seja sempre alcançável do
que garantir isso para **milhares** de centrais espalhadas em endereços diferentes.

### Por que o software NÃO conecta na central?

Pelos mesmos motivos ao contrário: não há garantia de que o software consiga alcançar a central
(NAT, IP dinâmico, firewall do cliente bloqueando entrada). Foi exatamente esse problema que a
**primeira versão do CentralHub** cometeu — ela tentava abrir uma conexão de saída para o IP
cadastrado manualmente da central, o que só funcionaria em cenários muito específicos (rede local
sem NAT, IP fixo) e contraria o modelo documentado pelo próprio fabricante.

```
                       ❌ MODELO ERRADO (versão antiga do CentralHub)
     ┌─────────────┐                                    ┌──────────────┐
     │ CentralHub   │──── tenta discar para a central ──►│ Central JFL   │
     │ (cliente??)  │     (falha se houver NAT/firewall) │ (servidor??)  │
     └─────────────┘                                    └──────────────┘


                       ✅ MODELO CORRETO (documentado pela JFL, implementado hoje)
     ┌─────────────┐                                    ┌──────────────┐
     │ CentralHub   │◄─── a central disca para fora ─────│ Central JFL   │
     │ (servidor)   │     (funciona atrás de NAT/        │ (cliente)     │
     │ porta 8085   │      firewall do lado da central,  │               │
     │              │      porque é ela quem inicia)     │               │
     └─────────────┘                                    └──────────────┘
```

## 9. A anatomia de um pacote do protocolo JFL

Agora que você entende TCP e sessão, vamos ao conteúdo que trafega dentro dessa conexão.

TCP entrega **bytes brutos** — não entrega "mensagens" prontas. É responsabilidade de quem projeta
o protocolo definir onde uma mensagem começa e termina, e o que cada byte significa. A JFL define
isso assim (para o cabeçalho `0x7B`, usado pela Active 100 Bus):

```
┌──────┬──────┬──────┬──────┬─────────────────────┬────────┐
│ CAB  │ QDE  │ SEQ  │ CMD  │        DADOS         │   K    │
│ 1 B  │ 1 B  │ 1 B  │ 1 B  │   tamanho variável    │  1 B   │
└──────┴──────┴──────┴──────┴─────────────────────┴────────┘
```

("B" = byte. Um byte é a menor unidade de dados que o computador manipula — um número de 0 a 255.)

- **CAB (Cabeçalho)** — sempre o byte `0x7B` (que, em ASCII, é o caractere `{`). É como dizer
  "atenção, aqui começa um pacote do protocolo JFL". Se o receptor olha para um byte e ele não é
  `0x7B`, ele sabe que está fora de sincronia (ver
  [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md) para como o código trata isso).
- **QDE (Quantidade)** — quantos bytes tem o pacote **inteiro**, contando ele mesmo (CAB + QDE +
  SEQ + CMD + DADOS + K). Como é 1 byte só, o valor máximo é 255 — então um pacote `0x7B` nunca
  passa de 255 bytes.
- **SEQ (Sequência)** — um número que identifica *esta* mensagem, para que a resposta a ela possa
  ser identificada mesmo que outras mensagens cheguem no meio. Explicado em detalhe na seção 11.
- **CMD (Comando)** — qual é o "assunto" deste pacote: conexão, keep-alive, evento, PGM, status...
  Cada valor de CMD tem um significado documentado (ver seção 18).
- **DADOS** — o conteúdo específico daquele comando. Tamanho variável, definido pelo próprio
  comando (às vezes zero bytes, como no keep-alive de pedido).
- **K (Checksum)** — um byte de verificação de integridade. Explicado em detalhe na seção 10.

### Exemplo real, byte a byte

Este é um comando de keep-alive real, capturado do manual oficial da JFL:

```
7B 05 18 40 26
```

Decompondo:

| Byte | Valor | Campo | Significado |
|---|---|---|---|
| 1º | `7B` | CAB | "Isto é um pacote JFL" |
| 2º | `05` | QDE | O pacote inteiro tem 5 bytes (e de fato, contando os 5 bytes aqui, bate) |
| 3º | `18` | SEQ | Este pacote é identificado com o número de sequência `0x18` (24 em decimal) |
| 4º | `40` | CMD | Comando de Keep-Alive |
| — | (nenhum) | DADOS | O pedido de keep-alive não tem dados — é um "oi, ainda estou aqui" vazio |
| 5º | `26` | K | Checksum calculado sobre os 4 bytes anteriores |

## 10. O checksum, explicado do zero

**Checksum** é uma técnica simples para detectar se algum byte de uma mensagem foi corrompido no
caminho (por interferência elétrica, erro de rede, etc.) — não é criptografia, não protege contra
alguém malicioso alterando os dados de propósito, só ajuda a pegar erros acidentais de transmissão.

A JFL usa a técnica mais simples que existe: **XOR (ou-exclusivo) de todos os bytes**.

XOR é uma operação matemática entre dois bits (0 ou 1) que segue esta regra:

```
0 XOR 0 = 0
0 XOR 1 = 1
1 XOR 0 = 1
1 XOR 1 = 0
```

("Dá 1 quando os dois bits são diferentes, dá 0 quando são iguais.")

Para calcular o checksum de um pacote inteiro (todos os bytes, incluindo o cabeçalho `0x7B`), você
faz XOR de todos eles, byte a byte, acumulando o resultado. O valor final é colocado como o último
byte do pacote (o campo K).

### Exemplo passo a passo (o mesmo pacote da seção anterior, sem o K)

Bytes a XORar: `0x7B`, `0x05`, `0x18`, `0x40`.

```
  0111 1011   (0x7B)
XOR 0000 0101   (0x05)
  ─────────
  0111 1110   (resultado parcial: 0x7E)

  0111 1110   (0x7E)
XOR 0001 1000   (0x18)
  ─────────
  0110 0110   (resultado parcial: 0x66)

  0110 0110   (0x66)
XOR 0100 0000   (0x40)
  ─────────
  0010 0110   (resultado final: 0x26)
```

O resultado, `0x26`, é exatamente o byte K que aparece no exemplo real (`7B 05 18 40 26`) — prova
de que o cálculo está certo.

### Como verificar um pacote recebido

A beleza dessa técnica: se você fizer XOR de **todos os bytes, incluindo o próprio K**, o
resultado deve dar **zero**. Isso porque XORar um valor com ele mesmo sempre dá zero (`X XOR X =
0`), e o K foi construído exatamente para ser "o valor que zera tudo".

```
0x7B XOR 0x05 XOR 0x18 XOR 0x40 XOR 0x26 = 0x00   ✅ pacote íntegro
```

Se o resultado não for zero, o pacote foi corrompido em algum lugar, e deve ser descartado.

No código, isso está implementado em
[`ChecksumCalculator.cs`](../SDK/CentralHub.SDK/Jfl/Protocol/ChecksumCalculator.cs) — duas
funções: `Calculate` (calcula o K a partir de todos os outros bytes) e `IsValid` (confere se um
pacote completo, incluindo o K, resulta em zero).

## 11. O SEQ (byte de sequência), explicado do zero

Imagine esta situação: o CentralHub envia um comando de status (0x4D) para a central. Antes da
central responder, ela mesma detecta um evento (por exemplo, uma zona abriu) e envia esse evento
*primeiro*, fora de ordem. Como o CentralHub sabe diferenciar "isto é a resposta que eu esperava"
de "isto é uma mensagem nova, não solicitada"?

A resposta é o **SEQ**: cada pacote enviado carrega um número de sequência (de `0x01` a `0xFF` —
nunca zero), e **a resposta a um comando deve usar o mesmo SEQ do comando que a originou**. Assim,
mesmo que mensagens cheguem fora de ordem, é possível casar pergunta com resposta pelo número.

```
CentralHub envia: SEQ=0x4D, CMD=0x4D (pedido de status)
                        │
                        │  (a central pode mandar outras coisas nesse meio tempo,
                        │   cada uma com seu próprio SEQ diferente)
                        ▼
Central responde: SEQ=0x4D, CMD=0x4D (resposta de status — mesmo SEQ do pedido!)
```

No código do CentralHub, essa correlação é implementada em
[`JflSession.SendAndWaitAsync`](../SDK/CentralHub.SDK/Jfl/Server/JflSession.cs): ao enviar um
comando, o método guarda uma "promessa" (`TaskCompletionSource`) associada àquele SEQ específico;
quando um pacote chega com aquele mesmo SEQ, a promessa é cumprida com aquele pacote como
resultado. Esse mecanismo está detalhado em
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md).

## 12. O comando de conexão — 0x21 (Handshake)

"Handshake" (aperto de mão, em inglês) é o termo usado em redes para a troca inicial de mensagens
que estabelece uma conversa — como duas pessoas se cumprimentando e se apresentando antes de
começar a falar sobre o assunto de verdade.

No protocolo JFL, o handshake é o comando `0x21`, **sempre o primeiro pacote enviado pela central**
assim que a conexão TCP é aceita. Ele contém tudo que o CentralHub precisa para identificar quem
está conectando:

| Campo | Tamanho | O que é |
|---|---|---|
| NS (Número de Série) | 10 bytes | Identificador único e permanente do equipamento — a "impressão digital" que o CentralHub usa para saber *qual* central é esta, independente do IP dela. |
| IMEI | 15 bytes | Identificador do chip celular, se houver (vazio/`0xFF` se não usa celular). |
| MAC | 12 bytes | Endereço físico da placa de rede Ethernet. |
| MOD (Modelo) | 1 byte | Qual modelo de equipamento é este (`0xA4` = Active 100 Bus). |
| VER (Versão) | 3 bytes | Versão do firmware (ex.: "6.5"). |
| IP, SIMCARD, VIA, OPE | 4 bytes | Detalhes de qual via de comunicação está sendo usada. |
| STATUS | resto do pacote | Um resumo de status, tamanho variável por modelo. |

O CentralHub responde com:

| Campo | Tamanho | O que é |
|---|---|---|
| RESULT | 1 byte | `0x01` = liberado (aceito) / `0x00` = bloqueado (a central derruba a conexão sozinha, ao receber isso). |
| KEEP | 1 byte | De quantos em quantos minutos a central deve mandar o próximo keep-alive (1 a 20 minutos). |

### Exemplo real capturado (Active 20 Ethernet, do manual oficial)

```
TX (a central envia): 7B 66 17 21 32 37 33 35 38 37 39 32 35 34 FF FF ... A3 36 30 30 01 01 01 06 ...
RX (o servidor responde): 7B 07 17 21 01 00 4B
```

Na resposta: `SEQ=0x17` (o mesmo do pedido), `CMD=0x21`, `RESULT=0x01` (liberado),
`KEEP=0x00` (0x00 é tratado como "1 minuto" por regra especial do protocolo).

No código: [`ConnectionCommandHandler.cs`](../SDK/CentralHub.SDK/Jfl/Server/Handlers/ConnectionCommandHandler.cs)
processa este comando, [`ConnectionRequest.cs`](../SDK/CentralHub.SDK/Jfl/Messages/ConnectionRequest.cs)
faz o parsing dos campos. Detalhes byte a byte completos em
[`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md).

## 13. O comando de Keep-Alive — 0x40

"Keep-alive" (manter vivo) é um comando que não carrega informação de negócio nenhuma — sua única
função é confirmar, periodicamente, que a conexão continua funcionando dos dois lados. Sem ele, se
a rede caísse silenciosamente (sem um dos lados perceber), a conexão TCP poderia ficar "pendurada"
indefinidamente, com os dois lados achando que ainda está tudo bem.

A central envia um `0x40` (sem dados) a cada N minutos (o N combinado no handshake), e o
CentralHub responde com o próximo intervalo (que pode, teoricamente, mudar a cada resposta, embora
na prática o CentralHub sempre responda com o mesmo valor configurado).

```
Central: 7B 05 18 40 26            (pedido de keep-alive, SEQ=0x18)
Servidor: 7B 06 18 40 00 25        (resposta: KEEP=0x00 → 1 minuto, mesmo SEQ)
```

Ver [`13_COMMANDS_GUIDE`](08_COMMANDS_GUIDE.md) para os detalhes de implementação em
[`KeepAliveCommandHandler.cs`](../SDK/CentralHub.SDK/Jfl/Server/Handlers/KeepAliveCommandHandler.cs).

## 14. O comando de Evento — 0x24

O comando `0x24` é usado pela central para reportar, em tempo real, **qualquer coisa que
aconteça**: uma zona disparou, um usuário armou o sistema, a energia caiu, etc. Ele carrega um
código no padrão **Contact ID** (um padrão da indústria, de décadas atrás, usado originalmente
para linha telefônica, e reaproveitado aqui dentro do pacote binário).

> ⚠️ **Este comando ainda NÃO está implementado no CentralHub** — existe um "stub" (um
> reconhecedor vazio, que só loga o recebimento) em
> [`EventoCommandHandlerStub.cs`](../SDK/CentralHub.SDK/Jfl/Server/Handlers/Stubs/EventoCommandHandlerStub.cs).
> Isso está documentado como pendência explícita no [`14_ROADMAP.md`](14_ROADMAP.md) e no
> [`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md).

## 15. O comando de Pedir Status — 0x93

Comando opcional, enviado *pelo software* para a central, pedindo um resumo leve de status (nível
de sinal, se há problema, quantas partições existem, o estado geral). É diferente do comando 0x4D
(seção 16) — este é mais enxuto, não traz zona por zona.

> ⚠️ **Este comando também não está implementado** — existe apenas um stub
> ([`PedirStatusCommandHandlerStub.cs`](../SDK/CentralHub.SDK/Jfl/Server/Handlers/Stubs/PedirStatusCommandHandlerStub.cs)).
> O CentralHub usa o **0x4D** (seção 16) para consultar status, que traz muito mais detalhe
> (inclusive zona por zona), então o 0x93 não foi priorizado.

## 16. O comando de Status como superusuário — 0x4D

Este é o comando real, implementado e homologado, que o CentralHub usa para consultar **tudo** que
está acontecendo numa central: as 16 partições, as 99 zonas, os 16 PGMs, o eletrificador, a
bateria, a alimentação AC, e um mapa de 40 bits de problemas diversos — tudo numa única resposta.

O pedido não tem dados (`7B QDE SEQ 0x4D K`); a central responde com um pacote grande (até 115
bytes de dados), no formato que o manual chama de "resposta da tela monitorar" (porque
originalmente essa é a mesma resposta que aparece quando um técnico usa a tela "Monitorar" do
ActiveNet).

Esse formato de resposta é tão importante que é **reaproveitado por outros comandos também**
(incluindo os comandos de PGM, seção 17) — ver
[`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md) para o detalhamento campo a campo.

## 17. Os comandos de PGM — 0x50 e 0x51

`0x50` = "Acionar PGM" (ligar uma saída). `0x51` = "Desacionar PGM" (desligar). Os dois recebem 1
byte de dados: o número da PGM (1 a 16). A resposta é a mesma "resposta da tela monitorar" do
0x4D — ou seja, para saber se o comando funcionou, o CentralHub olha, dentro dessa resposta
completa, se o estado daquela PGM específica mudou para o esperado.

> Não existe, no protocolo oficial, um terceiro comando de "Pulso". O CentralHub implementa Pulso
> como **Acionar, esperar um tempo configurável, Desacionar** — dois comandos oficiais em
> sequência, não um comando novo inventado.

Exemplo real capturado (Active 20 Ethernet, do manual):
```
Pedido:    7B 06 46 50 01 6A        (Acionar PGM 1, SEQ=0x46)
Resposta:  7B 76 46 50 01 79 ... (118 bytes — a "resposta da tela monitorar" completa)
```

## 18. Tabela-resumo de todos os comandos citados

| CMD (hex) | Nome | Direção | Implementado? |
|---|---|---|---|
| `0x21` | Comando de conexão (handshake) | Central → Servidor | ✅ Sim |
| `0x24` | Comando de evento | Central → Servidor | ❌ Stub |
| `0x40` | Comando de keep-alive | Central → Servidor | ✅ Sim |
| `0x4D` | Comando de status (superusuário) | Servidor → Central | ✅ Sim |
| `0x50` | Acionar PGM | Servidor → Central | ✅ Sim |
| `0x51` | Desacionar PGM | Servidor → Central | ✅ Sim |
| `0x93` | Pedir status (opcional) | Servidor → Central | ❌ Stub |
| `0x4E`/`0x4F`/`0x53`/`0x54` | Armar/Desarmar/Stay/Away | Servidor → Central | ✅ Sim |
| `0x52` | Inibir zonas | Servidor → Central | ✅ Sim |
| `0x37` | Comandos com senha (armar, PGM, usuários...) | Servidor → Central | ❌ Stub |

## 19. Casos de uso reais

- **"Preciso saber se a loja X está com o alarme armado agora"** → chamar
  `GET /api/centrais/{id}/status` e olhar o campo `particoes`.
- **"Preciso abrir o portão da garagem remotamente"** (supondo o portão ligado numa PGM) →
  `POST /api/centrais/{id}/pgm/{numero}/pulso`.
- **"A central está reportando bateria fraca — quero confirmar"** → o mesmo endpoint de status,
  campo `bateria`.

## 20. Boas práticas ao trabalhar com este protocolo

- **Nunca assuma que um campo documentado para um modelo vale para outro** — a JFL reaproveita
  cabeçalhos entre modelos com capacidades diferentes; sempre confira contra o manual do modelo
  específico.
- **Sempre valide o checksum antes de confiar em um pacote** — nunca pule essa checagem, mesmo em
  código de teste.
- **Sempre correlacione por SEQ, nunca assuma que a próxima mensagem que chega é a resposta que
  você espera** — eventos podem chegar entre um pedido e sua resposta.
- **Trate campos desconhecidos/não documentados como "reservado", nunca como erro fatal** — o
  próprio manual da JFL orienta isso, para manter compatibilidade com firmwares futuros.

## 21. Problemas comuns

- **Checksum sempre inválido** → verifique se você está incluindo o byte `0x7B` (cabeçalho) no
  cálculo — é um erro comum esquecer dele.
- **SEQ não bate** → normalmente sinal de estar respondendo com um SEQ novo em vez de ecoar o SEQ
  recebido.
- **Central nunca manda o 0x21** → normalmente é problema de rede (a central nunca chega a
  conectar), não do parser. Ver [`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md).

## 22. Como testar

O projeto tem testes automatizados que reconstróem pacotes reais capturados do manual e comparam
byte a byte com o que o código gera — ver
[`SDK/CentralHub.SDK.Tests/Protocol/PacketBuilderTests.cs`](../SDK/CentralHub.SDK.Tests/Protocol/PacketBuilderTests.cs)
e [`ChecksumCalculatorTests.cs`](../SDK/CentralHub.SDK.Tests/Protocol/ChecksumCalculatorTests.cs).
Rodar com `dotnet test` (ver [`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md)).

## 23. Como depurar

O `appsettings.json` do Backend já vem configurado com nível `Debug` para o namespace
`CentralHub.SDK.Jfl` — rodando o Backend, todo pacote recebido/enviado aparece logado, incluindo
IP remoto, SEQ, CMD e quantidade de bytes. Ver
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md) para exemplos reais de log.

## 24. FAQ

**P: Por que a JFL usa um protocolo binário próprio em vez de algo como JSON/HTTP?**
R: O protocolo foi desenhado para rodar em microcontroladores muito simples, com pouquíssima
memória e poder de processamento, décadas atrás — processar texto (JSON) exige muito mais
recursos do que processar bytes fixos.

**P: O que acontece se dois pacotes chegarem "grudados" na mesma leitura de rede?**
R: O `JflFrameReader` lida com isso — ele processa um buffer que pode conter múltiplos pacotes de
uma vez, ou um pacote parcial que precisa de mais leituras para completar. Ver
[`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md).

**P: Existe criptografia no protocolo?**
R: Não. O protocolo trafega em texto binário puro, sem TLS/criptografia. Isso tem implicações de
segurança discutidas em [`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md).

## 25. Checklist de entendimento

- [ ] Sei explicar o que é TCP e a diferença entre "quem escuta" e "quem conecta".
- [ ] Sei que a central é sempre quem inicia a conexão com o CentralHub.
- [ ] Sei calcular um checksum XOR simples na mão.
- [ ] Sei para que serve o SEQ.
- [ ] Sei diferenciar handshake (0x21), keep-alive (0x40), evento (0x24) e status (0x4D).
- [ ] Sei que Pulso não é um comando de protocolo — é dois comandos (0x50 + 0x51) em sequência.

---

**Documento anterior:** [`01_PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md)
**Próximo documento:** [`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
