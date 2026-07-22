# 12 — FAQ GERAL DO PROJETO

> **Público-alvo:** qualquer pessoa com qualquer nível de familiaridade com o projeto, procurando
> uma resposta rápida sem precisar ler um documento inteiro. Cada resposta aqui é propositalmente
> curta e aponta para o documento certo quando o assunto merece profundidade.

---

## Índice por categoria

1. [Conceitos gerais / negócio](#1-conceitos-gerais--negócio)
2. [Protocolo JFL](#2-protocolo-jfl)
3. [Rede e conectividade](#3-rede-e-conectividade)
4. [Arquitetura e fluxo](#4-arquitetura-e-fluxo)
5. [Código-fonte](#5-código-fonte)
6. [Banco de dados](#6-banco-de-dados)
7. [SessionManager](#7-sessionmanager)
8. [Comandos](#8-comandos)
9. [Interface web](#9-interface-web)
10. [Extensão / desenvolvimento futuro](#10-extensão--desenvolvimento-futuro)
11. [Hardware e homologação](#11-hardware-e-homologação)
12. [Operação do dia a dia](#12-operação-do-dia-a-dia)

---

## 1. Conceitos gerais / negócio

**1. O que é o CentralHub?**
Um sistema (backend em C#/.NET + frontend em React) que recebe conexões de centrais de alarme JFL
Active 100 Bus, decodifica o protocolo binário delas, e expõe isso como uma API REST e uma
interface web. Ver [`01_PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md).

**2. Que problema o CentralHub resolve?**
Permite monitorar e operar (status, PGMs) centrais de alarme remotamente, sem depender do software
proprietário da fabricante (ActiveNet), integrando os dados a um sistema próprio.

**3. O CentralHub substitui o ActiveNet?**
Não totalmente — o ActiveNet ainda é necessário para *programação local* da central (usuários,
zonas, configurações). O CentralHub cobre o canal de *monitoramento/operação remota*. Ver
[`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), seção 5.

**4. Quais fabricantes de central são suportados hoje?**
Só JFL Active 100 Bus, de fato homologado. Há código legado (`IntelbrasAdapter`, `JflAdapter`
antigo) que não deve ser usado — ver [`01_PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md), seção 7.

**5. O projeto está pronto para produção?**
As funcionalidades implementadas (conexão, status, PGM) foram homologadas contra hardware real.
Funcionalidades como Usuários, Arme/Desarme, Eventos em tempo real, ainda não existem — ver
[`14_ROADMAP.md`](14_ROADMAP.md).

**6. Quem são os usuários finais deste sistema?**
Operadores de central de monitoramento / portaria, que precisam ver o status de uma central e
acionar PGMs remotamente.

**7. Por que "PGM"? O que isso significa?**
"Programmable Output" — uma saída programável da central de alarme, tipicamente ligada a um relé
que pode acionar portões, sirenes, iluminação, etc. Ver [`15_GLOSSARY.md`](15_GLOSSARY.md).

**8. O sistema funciona sem internet?**
A comunicação entre a central e o servidor CentralHub pode ser em rede local (LAN) — não depende
de internet, desde que haja conectividade IP entre os dois pontos.

**9. Existe suporte a múltiplos clientes/prédios (multi-tenant)?**
Existe uma tabela `Buildings` básica, mas não há isolamento de dados por tenant real. Ver
[`14_ROADMAP.md`](14_ROADMAP.md).

**10. O sistema grava histórico de eventos da central (alarmes, aberturas, etc.)?**
Não ainda — o comando de Evento (0x24) está implementado apenas como *stub* (recebe e confirma, mas
não processa nem persiste). Ver [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md).

## 2. Protocolo JFL

**11. O protocolo é aberto/documentado publicamente?**
A JFL publica um manual técnico do protocolo Active 100 Bus, usado como referência primária deste
projeto.

**12. O protocolo é texto ou binário?**
Binário — cada pacote é uma sequência de bytes com campos de tamanho fixo/variável, não JSON nem
XML. Ver [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), seção 6.

**13. Qual é a estrutura de um pacote?**
`CAB` (cabeçalho `0x7B`) + `QDE` (quantidade de dados) + `SEQ` (sequência) + `CMD` (comando) +
`DADOS` (payload) + `K` (checksum). Ver seção 6 do doc 02.

**14. O que é o campo SEQ e para que serve?**
Um número de sequência que correlaciona uma resposta à sua requisição correspondente — essencial
porque múltiplos comandos podem estar "em voo" ao mesmo tempo. Ver
[`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), seção 7.

**15. O que é o checksum e como ele é calculado?**
Um XOR (`^`) de todos os bytes anteriores do pacote — detecta corrupção de dados em trânsito. Ver
mesma seção do doc 02, com exemplo binário completo.

**16. O que acontece se o checksum estiver errado?**
O `PacketParser` rejeita o pacote (lança `JflProtocolException`), e ele nunca chega a um handler de
comando.

**17. Quais comandos estão implementados hoje?**
0x21 (conexão/handshake), 0x40 (keep-alive), 0x4D (status), 0x50/0x51 (PGM ligar/desligar, e Pulso
como sequência dos dois). Ver a tabela mestre em
[`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md).

**18. Quais comandos existem só como stub?**
0x24 (evento), 0x93 (pedir status "curto"), 0x4E/0x4F/0x53/0x54 (arme/desarme), 0x52 (inibir
zonas), 0x37 (comandos com envelope de senha, usados para usuários).

**19. O que é "MOD" no protocolo?**
Um byte que identifica o modelo do equipamento — `0xA4` para Active 100 Bus. Usado no comando de
conexão.

**20. Por que a central envia o Keep-Alive periodicamente?**
Para o servidor saber que a conexão continua viva mesmo sem eventos acontecendo — sem isso, o
servidor não teria como distinguir "conexão ociosa" de "conexão morta".

**21. Com que frequência o Keep-Alive é enviado?**
Definido na configuração da central (via ActiveNet) — nos testes reais, por volta de alguns
minutos. Não é um valor fixo do protocolo.

**22. O que é BCD, mencionado nos parsers?**
"Binary-Coded Decimal" — cada nibble (4 bits) de um byte representa um dígito decimal
separadamente, técnica comum em protocolos de alarme para codificar números como o de série. Ver
[`15_GLOSSARY.md`](15_GLOSSARY.md).

**23. Por que às vezes um campo do protocolo aparece "opcional"?**
A JFL evoluiu o protocolo ao longo de várias revisões, adicionando campos apenas no final dos
payloads existentes para preservar compatibilidade — versões antigas de firmware podem simplesmente
não enviar os campos mais novos.

**24. O comando de Status (0x4D) retorna zonas mesmo se a central não tiver 99 zonas físicas?**
Sim — o payload sempre reserva espaço para 99 zonas; zonas não usadas fisicamente retornam estado
"normal"/inativo.

**25. Pulso (para PGM) é um comando de protocolo à parte?**
Não — "Pulso" é uma sequência lógica implementada no CentralHub (Ligar → aguardar → Desligar), não
existe um único byte de comando "Pulso" no protocolo real. Ver
[`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md).

## 3. Rede e conectividade

**26. Quem inicia a conexão TCP: a central ou o servidor?**
A central. O CentralHub atua como **servidor TCP**, escutando uma porta e aguardando a central
discar para ele — não o contrário. Esse é o ponto arquitetural mais importante do projeto. Ver
[`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md).

**27. Qual porta o CentralHub usa?**
Configurável; nos testes reais, `8085`.

**28. Preciso liberar alguma porta no firewall?**
Sim — a porta configurada do `JflTcpServer`, para tráfego de **entrada** (inbound), na máquina onde
o backend roda. Foi a causa raiz de um problema real de homologação. Ver
[`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md), seção sobre firewall.

**29. `ping` funcionando entre central e servidor garante que a conexão TCP vai funcionar?**
Não — ICMP (ping) e TCP são protocolos diferentes, e regras de firewall podem tratá-los de forma
independente. Use `Test-NetConnection -Port <porta>` para testar a porta especificamente.

**30. A central e o servidor precisam estar na mesma rede/sub-rede?**
Não necessariamente — precisam apenas ter roteamento IP entre si (e a porta liberada em qualquer
firewall no caminho).

**31. O CentralHub abre alguma conexão de saída para a central?**
Não, na arquitetura atual — toda comunicação usa a conexão TCP que a central abriu.

**32. O que é o ActiveNet e ele compete com o CentralHub pela mesma porta?**
ActiveNet é o software oficial da JFL, usado localmente (USB ou rede local direta) para
programação da central — usa uma porta/canal diferente do canal de monitoramento. Ver
[`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), seção 5.

**33. Quantas conexões simultâneas o servidor aceita?**
Uma por central (por `NumeroSerie`) de forma lógica, mas o `TcpListener` aceita múltiplas conexões
de múltiplas centrais diferentes simultaneamente — o `SessionManager` é um dicionário indexado por
central.

**34. O que acontece se a mesma central conectar duas vezes?**
A sessão antiga é substituída pela nova (reconexão) — ver
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md), seção sobre reconexão.

**35. Preciso de IP público/fixo para a central se conectar?**
Depende do cenário de rede — em ambiente local (mesma rede/VPN) não é necessário; para acesso via
internet aberta, sim, seria necessário expor a porta publicamente (com os devidos cuidados de
segurança, não cobertos por este projeto ainda).

## 4. Arquitetura e fluxo

**36. Quais são as 3 partes do projeto?**
`SDK` (protocolo JFL puro, sem dependências de banco/web), `Backend` (API REST + persistência),
`Frontend` (interface web React). Ver [`01_PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md).

**37. Por que o protocolo está num SDK separado do Backend?**
Para manter o código do protocolo reutilizável e testável isoladamente, sem acoplar a um banco de
dados ou framework web específico. Ver [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md).

**38. Quais são as 12 fases do fluxo de uma conexão, do ligar da central até o desligar?**
Ver o fluxo completo passo a passo em [`04_PROTOCOL_FLOW.md`](04_PROTOCOL_FLOW.md).

**39. O Backend e o SDK podem ser usados em outro projeto?**
O SDK sim, foi desenhado para isso (sem dependências de EF Core/ASP.NET). O Backend está acoplado a
este domínio específico.

**40. Existe algum diagrama de dependências entre as camadas?**
Sim, em [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md), seção de mapa de dependências.

## 5. Código-fonte

**41. Onde fica o parser do protocolo?**
`SDK/CentralHub.SDK/Jfl/Protocol/PacketParser.cs`. Ver
[`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md).

**42. Onde fica a lógica de sessão?**
`SDK/CentralHub.SDK/Jfl/Server/SessionManager.cs` e `JflSession.cs`.

**43. Existe código legado no projeto? Qual e por que ainda está lá?**
Sim, mas cada vez menos: `SDK/CentralHub.SDK/Adapters/*` (`AdapterFactory`, `JflAdapter`,
`IntelbrasAdapter`, `FakeAdapter`, `TcpConnectionHelper` — arquitetura antiga de conexão de saída,
marcado `[Obsolete]`) e `KeepAliveService.cs` (desregistrado, não roda mais). Ambos continuam no
projeto só por precaução (não apagados sem necessidade) — ver
[`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md), seção 6.
`Backend/.../Services/ConnectionService.cs` (endpoint `POST /api/central/testar-conexao`) e
`Backend/.../Services/OperationService.cs` (fluxo simulado da tela "Operação", via
`AdapterFactory`/`FakeAdapter`) **foram removidos de verdade**, em limpezas separadas — ver
[`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md) e
[`Protocol/20_CHANGELOG.md`](Protocol/20_CHANGELOG.md) ("Fase 4"). `OperationController.cs`/
`OperationPage.tsx` **não são mais legados** — hoje chamam `PgmService` diretamente, o mesmo
serviço real da Tela Central, sem nenhuma simulação.

**44. Posso simplesmente apagar o código legado?**
O que resta (`Adapters/*`, `KeepAliveService.cs`) não tem mais nenhum consumidor real desde que
`OperationService` foi removido — tecnicamente já poderia ser apagado com segurança. Continua no
projeto por precaução (não apagar código sem necessidade explícita), documentado como órfão em vez
de removido às pressas — ver [`14_ROADMAP.md`](14_ROADMAP.md), seção 13.

**45. Onde ficam os testes unitários?**
`SDK/CentralHub.SDK.Tests/` — cobrem o parser, o checksum, os comandos implementados, usando
exemplos reais do manual.

**46. O projeto usa Dependency Injection?**
Sim, via o container nativo do ASP.NET Core (`Program.cs`), para serviços do Backend e do SDK
(`SessionManager`, `CentralStatusQueryService`, `PgmCommandService`, etc.).

**47. Como o `JflTcpServer` é iniciado?**
Como um `IHostedService`/`BackgroundService` registrado no `Program.cs` do Backend, iniciando junto
com a aplicação web.

## 6. Banco de dados

**48. Que banco de dados o projeto usa?**
SQLite — um arquivo único, sem servidor separado. Ver
[`06_DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md).

**49. Por que SQLite e não PostgreSQL/SQL Server?**
Simplicidade de desenvolvimento/homologação — não há necessidade de instalar/configurar um servidor
de banco separado. Ver limitações na seção 6 do doc 06 sobre produção em maior escala.

**50. O projeto usa EF Core Migrations?**
Não formalmente — usa `EnsureCreated()`, que cria tabelas que não existem mas **não** altera
tabelas já existentes quando o modelo C# ganha um campo novo. Isso já causou um problema real. Ver
doc 06, seção sobre `EnsureCreated()`.

**51. O que fazer se eu adicionar um campo num Model e ele não aparecer no banco?**
Ou migrar para EF Core Migrations de verdade, ou fazer `ALTER TABLE` manual (técnica realmente
usada neste projeto, documentada com exemplo em Python no doc 06).

**52. Qual é a diferença entre `Central` e `CentralSession`?**
`Central` é o cadastro permanente (uma linha por central física conhecida). `CentralSession` é o
registro de uma conexão TCP específica (pode haver várias ao longo do tempo, para a mesma central).
Ver doc 06, seção "Central vs CentralSession".

**53. O que é `NumeroSerie` e por que ele é tão importante?**
É a chave real que identifica uma central de forma única e permanente — usada para vincular sessões
TCP (que não conhecem o `Id` do banco) ao cadastro correto.

**54. Por que existem campos marcados "(legado)" na tabela `Centrals`?**
São campos da arquitetura antiga (IP/Porta/Usuario/Senha para conexão de saída), preservados por
não terem sido removidos, mas não usados pela arquitetura atual.

**55. A tabela `Histories` é usada atualmente?**
É legada, associada ao fluxo antigo (`OperationService`). Não é o mecanismo atual de log de
comandos.

## 7. SessionManager

**56. O que é uma "sessão", tecnicamente?**
Um objeto `JflSession` que envolve um `TcpClient` conectado, junto com metadados (número de série,
IP remoto, timestamps) e mecanismos de correlação de requisição/resposta. Ver
[`07_SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md).

**57. Como o servidor sabe qual resposta corresponde a qual requisição?**
Usa o campo `SEQ` do protocolo, combinado com um `ConcurrentDictionary` de
`TaskCompletionSource<JflPacket>` pendentes (`SendAndWaitAsync`/`TryCompletePendingRequest`). Ver
doc 07, seção de código.

**58. O que acontece se a central não responder dentro do tempo esperado?**
Um `CancellationTokenSource` com timeout cancela a espera, e o chamador recebe uma falha de timeout
— sem travar a aplicação indefinidamente.

**59. As sessões são thread-safe?**
Sim — usam `ConcurrentDictionary` para os dicionários compartilhados e `SemaphoreSlim` para
serializar escritas no socket (`_travaEscrita`), evitando bytes de comandos diferentes se
misturarem no mesmo stream.

**60. O que dispara o evento de sessão removida?**
Três causas possíveis: a central fecha a conexão, ocorre uma `IOException` na leitura, ou o
servidor está sendo desligado. Ver doc 07.

**61. Quem escuta os eventos de sessão (`SessaoRegistrada`/`SessaoRemovida`)?**
O `JflSessionPersistenceService`, que atualiza o banco de dados (`Central.Status`, etc.) em
resposta.

## 8. Comandos

**62. Como envio um comando de status via API?**
`GET /api/centrais/{id}/status` — usa a sessão ativa daquela central para consultar via 0x4D. Ver
[`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md).

**63. O que a API retorna se a central não estiver conectada no momento?**
`409 Conflict` — não `503`, porque a ausência de sessão ativa é um estado de negócio esperado
(central desligada), não uma falha do próprio serviço.

**64. Como envio um comando de PGM?**
Via endpoint do Backend que aciona `PgmCommandService`, que usa a sessão ativa da central — nunca
abre uma nova conexão TCP. Ver docs 08 e 09.

**65. Quantos PGMs a Active 100 Bus suporta?**
16.

**66. O que acontece se eu tentar acionar um PGM com a central offline?**
A requisição é rejeitada antes de qualquer tentativa de envio — é uma validação de segurança
explícita (checar `Status == Online` e sessão ativa antes de enviar).

**67. Como funciona o "Pulso" de um PGM?**
Envia Ligar, aguarda um intervalo configurado, envia Desligar — sequência orquestrada pelo
`PgmCommandService`, não um comando único do protocolo.

**68. Existe confirmação antes de acionar um PGM na interface web?**
Sim — um diálogo de confirmação é exibido antes do envio, para evitar acionamentos acidentais.

## 9. Interface web

**69. Que tecnologia o frontend usa?**
React + TypeScript, Vite como bundler, Material UI para componentes visuais, Axios para chamadas
HTTP. Ver [`09_WEB_GUIDE.md`](09_WEB_GUIDE.md).

**70. Como o status na tela se atualiza automaticamente?**
Via polling (`setInterval`), não WebSocket/SignalR — a tela reconsulta a API periodicamente (ex.:
a cada 5 segundos para status, 15 segundos para dados da central). Ver doc 09.

**71. A tela de operação (`OperationPage`) deve ser usada?**
Não — está marcada como legada, associada à arquitetura antiga. Use `CentralDetailPage`.

**72. Onde configuro a URL base da API no frontend?**
No Axios (`baseURL`), atualmente `http://localhost:5000/api` — ajustável conforme o ambiente. Ver
doc 09.

**73. A interface mostra as 99 zonas de uma vez?**
Sim, a tela de status foi desenhada para exibir a granularidade completa retornada pelo comando
0x4D: partições, zonas, PGMs, eletrificador, bateria, AC, problemas.

## 10. Extensão / desenvolvimento futuro

**74. Como adiciono um novo comando (ex.: Arme)?**
Siga o tutorial passo a passo em
[`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md), que distingue comandos "Tipo A"
(pergunta/resposta) de "Tipo B" (a central avisa por conta própria).

**75. Qual é a diferença entre comando Tipo A e Tipo B?**
Tipo A: o servidor pergunta, a central responde (ex.: Status, PGM, futuro Arme). Tipo B: a central
avisa por iniciativa própria, o servidor apenas reage (ex.: Handshake, Keep-Alive, futuro Evento).

**76. Comandos de Usuário usam o mesmo formato de resposta que Status/PGM?**
Não — usam um envelope diferente (`0x37`), com senha e uma resposta curta
(`0x37 0x03 0xC0 RESP`), diferente do formato "tela monitorar" usado por Status/PGM. Ver doc 10.

**77. O que está no roadmap do projeto?**
Eventos em tempo real, SignalR/WebSocket para push (substituindo polling), Arme/Desarme, Usuários,
Programação remota, suporte a Intelbras, Multi-Tenant real. Ver
[`14_ROADMAP.md`](14_ROADMAP.md).

**78. Por que Arme/Desarme não foi implementado ainda?**
Foi explicitamente fora do escopo autorizado nas fases de implementação realizadas até agora — não
é uma limitação técnica, é uma decisão de escopo.

## 11. Hardware e homologação

**79. O projeto foi testado contra hardware real?**
Sim — uma central Active 100 Bus real (NS 2751484124, firmware 6.5) foi usada durante toda a
homologação. Ver [`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md).

**80. Quais comandos foram validados contra a central física real (não simulada)?**
Handshake (0x21), Keep-Alive (0x40), Status (0x4D), vínculo automático de sessão. PGM foi validado
contra simulação, por precaução de segurança física. Ver doc 11.

**81. Por que PGM não foi testado no hardware real?**
Porque a equipe não tinha certeza do que estava fisicamente conectado a cada saída PGM daquela
instalação (portão, sirene, etc.) — acionar sem esse conhecimento é um risco físico desnecessário.

**82. Quais problemas reais foram encontrados durante a homologação?**
(1) arquitetura de conexão invertida, (2) firewall bloqueando entrada, (3) conflito entre dois
mecanismos de status, (4) vínculo tardio de sessão quando a central conecta antes do cadastro
existir. Todos detalhados no doc 11.

**83. O que fazer para homologar uma central nova?**
Seguir o checklist da seção 10 de [`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md).

**84. Outras versões de firmware (diferentes de 6.5) são suportadas?**
Provavelmente sim (a JFL preserva compatibilidade entre revisões), mas não foram efetivamente
testadas — tratar como "não homologado" até validar.

## 12. Operação do dia a dia

**85. Como sei se uma central está online?**
Consultando o cadastro dela (`Status == "Online"`) — atualizado automaticamente pelo
`JflSessionPersistenceService` conforme a sessão TCP muda de estado.

**86. Quanto tempo depois de uma queda de conexão a central aparece como offline?**
Imediatamente após a conexão TCP ser fechada/perdida (detecção de `IOException` na leitura) — não
depende de um timeout de polling adicional.

**87. Posso ter duas instâncias do Backend rodando ao mesmo tempo (mesma porta)?**
Não — a segunda falhará ao tentar `Bind` na mesma porta TCP já em uso.

**88. O que fazer se o backend cair com a central conectada?**
A central vai perceber a queda da conexão TCP e, conforme seu próprio firmware, tentar reconectar
automaticamente assim que o servidor voltar.

**89. Como faço backup do projeto/dados?**
Ver processo documentado (backup completo, não apenas `git clone`) em
[`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md).

**90. Onde ficam os logs do sistema?**
Na saída padrão/console do processo do Backend (ASP.NET Core logging), cobrindo desde aceitação de
conexão TCP até resultado de cada comando.

**91. Um log de "Conexao TCP aceita" garante que o handshake também vai funcionar?**
Não — são eventos separados; a conexão TCP pode ser aceita e, ainda assim, o primeiro pacote
recebido falhar no parser (ex.: checksum inválido).

**92. Como sei qual foi o último IP de onde uma central conectou?**
Campo `UltimoIpConectado` no cadastro da `Central`, atualizado automaticamente a cada nova conexão.

**93. É seguro reiniciar o Backend com centrais conectadas?**
As sessões atuais serão perdidas (a central vai detectar a queda e reconectar), então é seguro, mas
causa uma interrupção momentânea real de monitoramento — evite fazer isso durante uma operação
crítica em andamento (ex.: um PGM em meio a um Pulso).

**94. O sistema impede dois comandos simultâneos para a mesma central?**
A escrita no socket é serializada por um `SemaphoreSlim`, evitando bytes intercalados — mas isso não
impede logicamente que dois comandos concorrentes sejam enfileirados; eles apenas não corrompem o
protocolo.

**95. Qual é o tempo típico de resposta de um comando de Status?**
Depende da central/rede real; nos testes de homologação, a resposta chegou tipicamente em
milissegundos a poucos segundos.

**96. Como interpreto o campo "problemas" retornado pelo Status?**
São flags booleanas específicas (bateria baixa, falta de AC, tamper, etc.) — ver a tabela completa
de `ProblemFlags` em [`08_COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md).

**97. O eletrificador aparece no status mesmo se a central não tiver um instalado?**
Sim, o campo sempre existe no payload; se não houver eletrificador físico, os valores retornados
não devem ser interpretados como reais (não há como o protocolo, por si só, informar "não
instalado" nesse campo).

**98. Quem eu devo procurar se encontrar uma central de modelo/firmware diferente do homologado?**
Trate como uma nova homologação: siga o checklist do doc 11 e valide cuidadosamente antes de
assumir compatibilidade total.

**99. Existe algum limite de zonas/partições/PGMs imposto pelo CentralHub, ou é tudo definido pela
central?**
É definido pelo protocolo do modelo Active 100 Bus (16 partições, 99 zonas, 16 PGMs) — o
CentralHub não impõe limites adicionais, apenas decodifica o que a central envia.

**100. Este documento cobre tudo?**
Não — é um índice rápido. Para profundidade real em qualquer tópico, siga os links para o
documento correspondente, especialmente [`02`](02_JFL_PROTOCOL_GUIDE.md),
[`07`](07_SESSION_MANAGER_GUIDE.md) e [`08`](08_COMMANDS_GUIDE.md), que são os mais técnicos.

**101. Onde posso ver a lista de todos os documentos disponíveis?**
Em [`INDEX.md`](INDEX.md).

---

## Checklist

- [ ] Sei em qual documento aprofundar cada uma das 12 categorias acima.
- [ ] Sei distinguir uma pergunta de "conceito" de uma pergunta de "operação do dia a dia".

---

**Documento anterior:** [`11_HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md)
**Próximo documento:** [`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
