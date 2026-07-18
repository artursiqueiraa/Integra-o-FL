# 03 — NETWORK ARCHITECTURE

> **Público-alvo:** alguém que precisa entender fisicamente por onde os dados passam, desde a
> central de alarme até a tela do navegador — útil para diagnosticar problemas de rede/firewall
> durante uma instalação real.

---

## Índice

1. [Visão geral do caminho completo](#1-visão-geral-do-caminho-completo)
2. [Diagrama completo, camada por camada](#2-diagrama-completo-camada-por-camada)
3. [Etapa 1 — Active 100 Bus](#etapa-1--active-100-bus)
4. [Etapa 2 — TCP (a conexão em si)](#etapa-2--tcp-a-conexão-em-si)
5. [Etapa 3 — Firewall](#etapa-3--firewall)
6. [Etapa 4 — Servidor (a máquina física/virtual)](#etapa-4--servidor-a-máquina-físicavirtual)
7. [Etapa 5 — JflTcpServer](#etapa-5--jfltcpserver)
8. [Etapa 6 — SessionManager](#etapa-6--sessionmanager)
9. [Etapa 7 — API (ASP.NET Core)](#etapa-7--api-aspnet-core)
10. [Etapa 8 — Frontend](#etapa-8--frontend)
11. [Duas redes, duas portas: monitoramento × programação local](#11-duas-redes-duas-portas-monitoramento--programação-local)
12. [Portas usadas por todo o sistema](#12-portas-usadas-por-todo-o-sistema)
13. [Casos de uso reais de topologia](#13-casos-de-uso-reais-de-topologia)
14. [Boas práticas de rede](#14-boas-práticas-de-rede)
15. [Problemas comuns](#15-problemas-comuns)
16. [Como testar conectividade](#16-como-testar-conectividade)
17. [Como depurar problemas de rede](#17-como-depurar-problemas-de-rede)
18. [FAQ](#18-faq)
19. [Checklist](#19-checklist)

---

## 1. Visão geral do caminho completo

Um dado nasce dentro da central de alarme (por exemplo, "a bateria está fraca") e termina sendo
exibido como um texto na tela do navegador de um operador. Entre esses dois pontos, ele atravessa
oito camadas distintas. Este documento explica cada uma delas, na ordem.

```
Active 100 Bus
   ↓
TCP
   ↓
Firewall
   ↓
Servidor
   ↓
JflTcpServer
   ↓
SessionManager
   ↓
API
   ↓
Frontend
```

## 2. Diagrama completo, camada por camada

```
┌───────────────────────────────────────────────────────────────────────────┐
│ 1. ACTIVE 100 BUS (hardware físico, instalado no local do cliente)         │
│                                                                             │
│    Módulo Ethernet decide, sozinho, abrir uma conexão de SAÍDA para o      │
│    IP1:Porta1 configurados via ActiveNet.                                 │
└──────────────────────────────┬────────────────────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────────────────┐
│ 2. TCP — a conexão em si                                                   │
│                                                                             │
│    Pacotes IP saem da central, atravessam o roteador/gateway do local do   │
│    cliente, atravessam a internet (ou uma rede local/VPN, dependendo da    │
│    topologia), até chegar ao IP do servidor do CentralHub.                 │
└──────────────────────────────┬────────────────────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────────────────┐
│ 3. FIREWALL                                                                │
│                                                                             │
│    Antes de chegar no processo do CentralHub, o pacote passa pelo          │
│    firewall do sistema operacional (e, às vezes, um firewall de rede       │
│    adicional). Se não houver uma regra de ENTRADA liberando a porta        │
│    8085, o pacote é descartado silenciosamente — o CentralHub nunca chega  │
│    a saber que alguém tentou conectar.                                    │
└──────────────────────────────┬────────────────────────────────────────────┘
                                │ (só passa se o firewall permitir)
                                ▼
┌───────────────────────────────────────────────────────────────────────────┐
│ 4. SERVIDOR (a máquina física ou virtual rodando o CentralHub)             │
│                                                                             │
│    O sistema operacional entrega o pacote para o processo que está         │
│    escutando naquela porta — no nosso caso, o processo do Backend          │
│    (CentralHub.Api.exe), que hospeda o JflTcpServer dentro de si.          │
└──────────────────────────────┬────────────────────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────────────────┐
│ 5. JflTcpServer (SDK/CentralHub.SDK/Jfl/Server/JflTcpServer.cs)            │
│                                                                             │
│    Um TcpListener .NET, escutando 0.0.0.0:8085 (todas as interfaces de     │
│    rede da máquina). Aceita a conexão, cria uma JflSession, começa a ler   │
│    pacotes 0x7B do socket usando o JflFrameReader/PacketParser.            │
└──────────────────────────────┬────────────────────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────────────────┐
│ 6. SessionManager (SDK/CentralHub.SDK/Jfl/Server/SessionManager.cs)        │
│                                                                             │
│    Depois que o handshake 0x21 é processado e o número de série é          │
│    conhecido, a sessão é registrada aqui, indexada por NumeroSerie — é     │
│    aqui que qualquer outra parte do sistema "encontra" uma central online. │
└──────────────────────────────┬────────────────────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────────────────┐
│ 7. API (Backend/CentralHub.Api, ASP.NET Core, porta 5000)                 │
│                                                                             │
│    Quando o navegador chama, por exemplo, GET /api/centrais/5/status, o   │
│    Controller chama um Service, que chama o SessionManager para achar a   │
│    sessão daquela central, envia o comando 0x4D nela, espera a resposta,  │
│    converte para JSON, devolve pela mesma conexão HTTP.                   │
└──────────────────────────────┬────────────────────────────────────────────┘
                                │  HTTP/JSON, uma conexão nova a cada requisição
                                ▼
┌───────────────────────────────────────────────────────────────────────────┐
│ 8. FRONTEND (React, rodando no navegador do operador, porta 5173 em dev)  │
│                                                                             │
│    A Tela Central chama a API a cada poucos segundos (polling) e          │
│    re-renderiza a tela com os dados novos.                                │
└───────────────────────────────────────────────────────────────────────────┘
```

## Etapa 1 — Active 100 Bus

Já explicada em profundidade em [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md). O que
importa para este documento: a decisão de **quando** e **para onde** conectar não é do CentralHub
— é inteiramente definida pela configuração gravada dentro da central (via ActiveNet, campos
`IP1`/`Porta1` da tela "Comunicação → Monitoramento"). Se esses campos estiverem errados, ou
apontando para uma rede que a central não alcança, **nenhuma quantidade de configuração do lado
do CentralHub resolve** — o problema está na ponta da central.

## Etapa 2 — TCP (a conexão em si)

Uma vez que a central decide conectar, o pacote de abertura de conexão (chamado de `SYN`, em
TCP) sai da interface de rede da central, é roteado pela rede do cliente (possivelmente
atravessando um roteador com NAT — Network Address Translation, uma técnica que permite várias
máquinas de uma rede local compartilharem um único IP público), atravessa a internet (se aplicável)
e chega até a máquina que hospeda o CentralHub.

**Detalhe crítico observado durante a homologação real:** mesmo quando dois lados conseguem se
comunicar via `ping` (protocolo ICMP), isso **não garante** que uma conexão TCP numa porta
específica vá funcionar — pode existir uma regra de firewall/roteador que permite ICMP mas
bloqueia TCP, ou que bloqueia TCP só em certas portas. Isso foi documentado em detalhe durante a
fase de homologação do projeto (ver [`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md)).

## Etapa 3 — Firewall

Um firewall é um filtro que decide, com base em regras, quais pacotes de rede podem passar. Nesta
etapa, existem tipicamente **dois** firewalls possíveis no caminho:

1. **Firewall de rede** (roteador, appliance dedicado, firewall corporativo) — fora do controle
   direto do CentralHub, precisa ser configurado por quem administra a rede onde o servidor está.
2. **Firewall do sistema operacional** da própria máquina que roda o CentralHub (no Windows,
   "Windows Defender Firewall"; no Linux, `iptables`/`nftables`/`ufw`).

Durante a homologação real deste projeto, foi descoberto que o **Windows Firewall**, no perfil de
rede "Público" (que o Windows aplica automaticamente a redes Wi-Fi não reconhecidas como
confiáveis), tem uma política padrão de **`BlockInbound`** — bloqueia toda conexão de entrada não
explicitamente liberada. Isso impediu, por um tempo, que a central conseguisse conectar, mesmo com
tudo mais configurado corretamente. A correção foi criar uma regra de entrada explícita:

```powershell
New-NetFirewallRule -DisplayName "CentralHub JFL 8085" -Direction Inbound -Protocol TCP -LocalPort 8085 -Action Allow -Profile Any
```

Esse episódio real está documentado em detalhe (com os comandos de diagnóstico usados) em
[`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md).

## Etapa 4 — Servidor (a máquina física/virtual)

A máquina onde o processo `CentralHub.Api` roda. Pode ser um servidor físico, uma máquina virtual
na nuvem, ou (durante desenvolvimento, como foi o caso deste projeto) uma estação de trabalho
comum. Requisitos mínimos:

- .NET 9 Runtime instalado (ou o SDK completo, se for compilar localmente).
- Porta 8085 (ou a configurada) livre e liberada no firewall.
- Porta 5000 (ou a configurada) livre para a API HTTP.
- Espaço em disco para o banco SQLite (cresce lentamente, com o histórico de sessões).

## Etapa 5 — JflTcpServer

Este é o primeiro componente de código deste projeto que o pacote de rede realmente toca. É
explicado em profundidade em [`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md) e
[`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md). Resumo: ele é um laço infinito
(`AceitarConexoesAsync`) que fica chamando `TcpListener.AcceptTcpClientAsync()` — um método que
"dorme" até que uma conexão nova chegue, e quando chega, entrega um objeto `TcpClient`
representando aquela conexão específica.

## Etapa 6 — SessionManager

Depois que o handshake (0x21) é decodificado e o número de série da central é conhecido, a sessão
passa a existir no `SessionManager` — um dicionário em memória (não no banco de dados!) que mapeia
"número de série" → "sessão TCP viva". **Se o processo do CentralHub reiniciar, todas as sessões
em memória desaparecem** — as centrais precisam reconectar (o que elas fazem automaticamente,
graças ao ciclo de keep-alive/retry do próprio protocolo).

## Etapa 7 — API

A parte "normal" de uma aplicação web: Controllers recebem requisições HTTP, Services aplicam a
lógica de negócio, e (quando a requisição precisa falar com uma central) o Service pede ao SDK
para usar a sessão certa do `SessionManager`. Isso é uma conexão de rede **completamente separada**
da conexão da central — a API roda numa porta diferente (5000) e usa o protocolo HTTP, não o
protocolo binário JFL.

## Etapa 8 — Frontend

A página React, rodando no navegador do operador, chamando a API via `fetch`/`axios` pela rede
(pode ser a mesma máquina, durante desenvolvimento, ou uma máquina completamente diferente, em
produção). Ver [`09_WEB_GUIDE.md`](09_WEB_GUIDE.md).

## 11. Duas redes, duas portas: monitoramento × programação local

Um ponto que gera muita confusão em campo (documentado com detalhe durante a homologação real):
existem **dois canais de rede completamente diferentes e independentes** envolvendo a mesma
central física:

```
┌────────────────────────────────────────────────────────────────────┐
│                                                                      │
│   CANAL 1: MONITORAMENTO (o que o CentralHub usa)                   │
│   ────────────────────────────────────────────────                 │
│   Central  ──(dispara SAÍDA)──►  CentralHub :8085                   │
│   Protocolo: 0x7B (documentado neste projeto)                       │
│                                                                      │
│   CANAL 2: PROGRAMAÇÃO LOCAL (o que o ActiveNet usa)                │
│   ────────────────────────────────────────────────                 │
│   ActiveNet ──(dispara SAÍDA)──►  Central :porta-local               │
│   Protocolo: NÃO documentado nos manuais desta pasta;               │
│              observado, na prática, numa porta diferente            │
│              (perto de 9080/9090) do módulo Ethernet da central.    │
│                                                                      │
└────────────────────────────────────────────────────────────────────┘
```

O fato de o ActiveNet conseguir se conectar com sucesso na central **não prova nada** sobre o
Canal 1 — são dois protocolos, duas portas, duas direções de conexão diferentes. Um erro comum em
campo é assumir "o ActiveNet conecta, então a rede está ok" e concluir que o problema está no
CentralHub — quando na verdade o Canal 1 (que é o que importa para o monitoramento) nunca foi
testado de verdade.

## 12. Portas usadas por todo o sistema

| Porta | Serviço | Protocolo | Configurável em |
|---|---|---|---|
| 8085 | `JflTcpServer` (recebe conexões das centrais) | TCP binário (JFL 0x7B) | `appsettings.json`, chave `Jfl:Porta` |
| 5000 | API REST | HTTP | `Program.cs` (`--urls`) / `launchSettings.json` |
| 5173 | Frontend em desenvolvimento (Vite) | HTTP | `vite.config.ts` |
| (porta local do módulo Ethernet) | Programação via ActiveNet | Não documentado neste projeto | Não aplicável (fora do CentralHub) |

## 13. Casos de uso reais de topologia

**Cenário A — Rede local sem NAT (ex.: servidor e central na mesma LAN):** simples, a central
disca direto para o IP local do servidor. Foi o cenário usado na homologação real deste projeto
(central em `10.0.250.21`, servidor acessível em `192.168.201.232`, ambos alcançáveis através de
um roteador que fazia a ponte entre as duas sub-redes).

**Cenário B — Central atrás de NAT/celular, servidor com IP público:** a central disca para fora
normalmente (esse é justamente o cenário que o protocolo foi desenhado para suportar bem — ver
[`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), seção 8); o servidor precisa ter a porta
8085 redirecionada/liberada publicamente.

**Cenário C — Múltiplos clientes, um único CentralHub central:** cada prédio/cliente tem sua
própria central, todas discando para o mesmo servidor CentralHub — é assim que a arquitetura foi
desenhada para escalar (uma sessão por central, todas coexistindo no mesmo `SessionManager`).

## 14. Boas práticas de rede

- Sempre confirmar a regra de firewall de **entrada** (inbound) na porta do `JflTcpServer` antes
  de assumir que há um problema de código.
- Preferir IP fixo (ou DHCP reservado) para o servidor do CentralHub — se o IP mudar, todas as
  centrais precisam ser reconfiguradas via ActiveNet.
- Documentar, para cada instalação, a topologia real (existe NAT? existe VPN? existe firewall
  corporativo no meio?) — evita retrabalho de diagnóstico.

## 15. Problemas comuns

- **"O log nunca mostra 'Nova conexão TCP recebida'"** → o pacote nunca chegou ao processo; o
  problema é de rede/firewall, não de código. Ver seção 17.
- **`ping` funciona, mas a central não conecta** → ICMP (ping) e TCP são protocolos diferentes;
  um firewall pode permitir um e bloquear o outro.
- **Funcionava e parou de funcionar depois de reiniciar o servidor** → verificar se o firewall
  ainda está liberado (algumas ferramentas de segurança resetam regras) e se a porta configurada
  não mudou.

## 16. Como testar conectividade

No servidor, verificar se a porta está realmente escutando:

```powershell
Get-NetTCPConnection -LocalPort 8085
```

De uma máquina remota (ou da própria central, indiretamente, via captura de log), testar se a
porta está alcançável:

```powershell
Test-NetConnection -ComputerName <IP-do-servidor> -Port 8085
```

Se `TcpTestSucceeded` vier `False`, o problema é de rede/firewall — nada no código do CentralHub
vai resolver isso.

## 17. Como depurar problemas de rede

1. Confirmar que o processo do Backend está rodando e logou `"Servidor JFL escutando na porta
   8085"`.
2. Confirmar, no sistema operacional, que algo está de fato escutando naquela porta
   (`Get-NetTCPConnection`).
3. Confirmar a política ativa do firewall (`netsh advfirewall show currentprofile`, no Windows).
4. Confirmar que existe uma regra de entrada explícita para a porta.
5. Só depois de tudo isso confirmado, suspeitar de configuração incorreta na própria central
   (IP1/Porta1 errados no ActiveNet).

## 18. FAQ

**P: Por que a porta 8085 e não uma porta "padrão" da indústria?**
R: Não existe uma porta padrão universal para esse tipo de protocolo — é uma escolha de
configuração, tanto do lado do CentralHub (`appsettings.json`) quanto do lado da central
(campo `Porta1` no ActiveNet). Os dois lados só precisam concordar no mesmo número.

**P: O tráfego é criptografado?**
R: Não — nem o protocolo JFL (ver [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md)) nem,
por padrão, a API REST em desenvolvimento (roda em HTTP puro localmente). Para produção, recomenda-
se colocar a API atrás de HTTPS (ex.: um proxy reverso como Nginx/Caddy com certificado TLS) — a
conexão da central em si (porta 8085) permanece em texto puro, pois é assim que o protocolo do
fabricante foi definido.

**P: Dá para ter mais de um servidor CentralHub, para redundância?**
R: A arquitetura atual não foi desenhada para isso (o `SessionManager` é em memória, dentro de um
único processo) — está listado como possível evolução futura em [`14_ROADMAP.md`](14_ROADMAP.md).

## 19. Checklist

- [ ] Sei desenhar, de memória, as 8 etapas do caminho de rede.
- [ ] Sei explicar por que `ping` funcionar não garante que TCP vai funcionar.
- [ ] Sei diferenciar o canal de monitoramento (porta 8085) do canal de programação local
      (ActiveNet).
- [ ] Sei os comandos básicos de diagnóstico de firewall no Windows.

---

**Documento anterior:** [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md)
**Próximo documento:** [`04_PROTOCOL_FLOW.md`](04_PROTOCOL_FLOW.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
