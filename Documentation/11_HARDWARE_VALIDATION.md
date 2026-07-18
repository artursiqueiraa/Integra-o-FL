# 11 — HARDWARE VALIDATION

> **Público-alvo:** qualquer pessoa que precise confirmar que o CentralHub foi de fato testado
> contra um equipamento físico real (não só contra a documentação teórica do fabricante), incluindo
> auditores, novos desenvolvedores céticos, e quem for homologar uma central adicional no futuro.
> Este documento é o registro histórico completo da homologação real deste projeto.

---

## Índice

1. [Por que homologação com hardware real importa](#1-por-que-homologação-com-hardware-real-importa)
2. [Ficha técnica do equipamento homologado](#2-ficha-técnica-do-equipamento-homologado)
3. [Linha do tempo da homologação](#3-linha-do-tempo-da-homologação)
4. [Problema encontrado nº 1 — arquitetura de conexão invertida](#4-problema-encontrado-nº-1--arquitetura-de-conexão-invertida)
5. [Problema encontrado nº 2 — Windows Firewall bloqueando entrada](#5-problema-encontrado-nº-2--windows-firewall-bloqueando-entrada)
6. [Problema encontrado nº 3 — conflito entre dois mecanismos de status](#6-problema-encontrado-nº-3--conflito-entre-dois-mecanismos-de-status)
7. [Problema encontrado nº 4 — vínculo tardio de sessão a cadastro](#7-problema-encontrado-nº-4--vínculo-tardio-de-sessão-a-cadastro)
8. [Testes realizados e resultados](#8-testes-realizados-e-resultados)
9. [Logs reais capturados](#9-logs-reais-capturados)
10. [Checklist de homologação (para uma central nova)](#10-checklist-de-homologação-para-uma-central-nova)
11. [Casos de uso reais](#11-casos-de-uso-reais)
12. [Boas práticas](#12-boas-práticas)
13. [Problemas comuns em campo](#13-problemas-comuns-em-campo)
14. [Como testar uma central nova](#14-como-testar-uma-central-nova)
15. [Como depurar uma homologação que não avança](#15-como-depurar-uma-homologação-que-não-avança)
16. [FAQ](#16-faq)
17. [Checklist final](#17-checklist-final)

---

## 1. Por que homologação com hardware real importa

Implementar um protocolo "seguindo o manual" não é garantia de que ele vai funcionar contra o
equipamento físico de verdade — manuais podem ter ambiguidades, erros de digitação, campos
subdocumentados, ou o firmware real pode se comportar de forma sutilmente diferente do que o texto
descreve. Por isso, todo comando implementado neste projeto foi, na medida do possível, validado
em duas camadas: (1) contra **exemplos reais capturados** publicados no próprio manual oficial da
JFL (capturas de tráfego real de outras instalações, incluídas como exemplos didáticos no PDF), e
(2) contra uma **central física real**, conectada de verdade, testada ao vivo.

## 2. Ficha técnica do equipamento homologado

| Item | Valor |
|---|---|
| Fabricante | JFL Alarmes |
| Modelo | Active 100 Bus |
| Placa | PCI-350 |
| Byte de identificação (`MOD`) | `0xA4` |
| Número de série | `2751484124` |
| Endereço MAC | `8C:4F:00:0A:73:48` |
| Versão de firmware | `6.5` |
| Via de comunicação | Ethernet |
| IP observado da central (rede do cliente) | `10.0.250.21` |
| IP do servidor CentralHub (rede local de homologação) | `192.168.201.232` |
| Porta do servidor CentralHub | `8085` |

## 3. Linha do tempo da homologação

```
1. Auditoria da arquitetura existente e da documentação oficial da JFL
   → conclusão: a arquitetura original (CentralHub disca para a central) estava invertida.
2. Reescrita da camada de comunicação: TcpListener, SessionManager, parser, checksum
   → 87 testes unitários escritos contra exemplos reais do manual.
3. Primeira tentativa de conexão real: falhou (nenhum log de conexão recebida).
4. Diagnóstico de rede: Windows Firewall bloqueando entrada (perfil "Público").
5. Regra de firewall liberada → central conectou com sucesso pela primeira vez.
6. Handshake (0x21) confirmado, decodificado corretamente, dados reais conferidos.
7. Instrumentação de logs adicionada para consolidar a evidência da homologação.
8. Consulta de Status (0x4D) implementada e validada contra a central real.
9. Vínculo automático Sessão ↔ Cadastro implementado e validado.
10. Comandos de PGM (0x50/0x51 + Pulso) implementados; validados contra central
    SIMULADA (não a real, por precaução — ver seção 8) e contra a estrutura de
    protocolo idêntica à real.
11. Interface web construída sobre tudo isso, testada contra a central real ainda
    conectada (sem interrupção da sessão em produção).
```

## 4. Problema encontrado nº 1 — arquitetura de conexão invertida

**Sintoma:** o código original tentava abrir uma conexão TCP de **saída** do CentralHub para o IP
cadastrado manualmente da central.

**Causa raiz:** o manual oficial da JFL (seção 2.1) documenta exatamente o oposto: a central é
quem inicia a conexão, o software de monitoramento deve ser um **servidor**.

**Resolução:** reescrita completa da camada de rede (`JflTcpServer`, `SessionManager`), abandonando
o modelo de discagem de saída. Ver [`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md), seção
8, para a explicação completa.

## 5. Problema encontrado nº 2 — Windows Firewall bloqueando entrada

**Sintoma:** mesmo com o `JflTcpServer` escutando corretamente na porta 8085, e a central
configurada corretamente (via ActiveNet) para discar para o IP/porta certos, **nenhuma tentativa de
conexão chegava a aparecer nos logs**.

**Diagnóstico:**
```powershell
netsh advfirewall show currentprofile
```
revelou que o perfil de rede ativo era **"Público"**, com política `BlockInbound` — o Windows
Firewall descarta, por padrão, qualquer conexão de entrada não explicitamente liberada nesse
perfil. Um teste adicional confirmou que `ping` (ICMP) funcionava entre as duas redes envolvidas,
mas conexões TCP não — prova de que ICMP passar não garante que TCP também passe.

**Resolução:**
```powershell
New-NetFirewallRule -DisplayName "CentralHub JFL 8085" -Direction Inbound -Protocol TCP -LocalPort 8085 -Action Allow -Profile Any
```

Ver [`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md) para o contexto completo.

## 6. Problema encontrado nº 3 — conflito entre dois mecanismos de status

**Sintoma (achado durante auditoria, antes de virar problema em produção):** o `KeepAliveService`
legado (um `BackgroundService` que testava conectividade de saída a cada 30 segundos) e o novo
`JflSessionPersistenceService` (que atualiza `Status` baseado em eventos reais de sessão)
escreviam no **mesmo campo** `Central.Status`, por caminhos completamente diferentes e
conflitantes.

**Resolução:** o `KeepAliveService` foi **desregistrado** (comentado em `Program.cs`), sem apagar a
classe — ver [`05_SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md), seção 6.

## 7. Problema encontrado nº 4 — vínculo tardio de sessão a cadastro

**Sintoma:** ao cadastrar uma `Central` no banco **depois** que ela já havia conectado pelo menos
uma vez, os campos `Status`/`UltimoIpConectado`/`Firmware`/`Modelo` continuavam `null`, mesmo com a
central genuinamente online.

**Causa raiz:** a sessão TCP daquela conexão específica nasceu **antes** de existir um cadastro
correspondente — a busca por `Central` (feita no momento em que a sessão é registrada) não achou
nada, e o vínculo (`CentralId`) ficou permanentemente `null` para aquela sessão específica.

**Resolução:** documentada como **limitação conhecida e aceita**, não corrigida com religação
retroativa (fora do escopo pedido) — o comportamento correto e esperado é: cadastre a Central
**antes**, ou aguarde a próxima reconexão da central (que acontece naturalmente, seja por queda de
energia, seja pelo próprio ciclo do protocolo) para o vínculo se formar. Confirmado com um teste
real: uma segunda conexão, feita depois do cadastro já existir, vinculou tudo corretamente,
imediatamente.

## 8. Testes realizados e resultados

| Teste | Método | Resultado |
|---|---|---|
| Handshake (0x21) contra hardware real | Conexão real, log observado ao vivo | ✅ NS, MAC, Modelo, Versão decodificados corretamente |
| Keep-Alive (0x40) contra hardware real | Observação de ciclo real | ✅ Confirmado |
| Status (0x4D) contra hardware real | `GET /api/centrais/{id}/status` com sessão real ativa | ✅ Resposta decodificada com sucesso (partições, zonas, PGMs, bateria, problemas) |
| Vínculo automático Sessão↔Cadastro | Central cadastrada, nova conexão observada | ✅ `Status`, `Firmware`, `Modelo`, `UltimoIpConectado`, `ConectadoDesdeUtc` todos preenchidos corretamente |
| Online/Offline automático | Observação de conexão e desconexão reais | ✅ Confirmado nos dois sentidos |
| PGM Ligar/Desligar/Pulso | Central **simulada** via socket (não a real — ver nota abaixo) | ✅ Confirmado, incluindo tempo real de espera do Pulso |
| Regressão: central real segue funcionando após mudanças subsequentes | Consultas repetidas ao longo do desenvolvimento | ✅ Nenhuma regressão observada — a mesma sessão real permaneceu `Online` durante toda a implementação de PGM |

> **Nota sobre PGM e hardware real:** os comandos de PGM foram deliberadamente validados contra uma
> central **simulada** (um script que fala o protocolo exatamente como a central real falaria),
> em vez da central física, porque o time de desenvolvimento não tinha informação sobre o que
> estava fisicamente conectado a cada saída PGM daquela instalação real (poderia ser um portão, uma
> sirene, qualquer coisa) — acionar uma saída física sem esse conhecimento é um risco desnecessário.
> A estrutura de protocolo validada é idêntica à que a central real usaria; a única diferença é a
> ponta final (relé de verdade vs. simulação).

## 9. Logs reais capturados

Trecho de log real, do primeiro handshake bem-sucedido contra a central física:

```
info: CentralHub.SDK.Jfl.Server.JflTcpServer[0]
      Conexao TCP aceita: IP remoto=10.0.250.21 Porta remota=64883
info: CentralHub.SDK.Jfl.Server.JflTcpServer[0]
      Primeiro comando recebido de 10.0.250.21:64883: Cmd=0x21 Seq=0x4D DadosLength=97
info: CentralHub.SDK.Jfl.Server.Handlers.ConnectionCommandHandler[0]
      Conexao recebida (cmd 0x21) de 10.0.250.21:64883: NS=2751484124 Modelo=Active 100 Bus
      Versao=6.5 IMEI=(vazio) MAC=8C4F000A7348 Via=Ethernet Sinal/Status=52 bytes
info: CentralHub.SDK.Jfl.Server.SessionManager[0]
      Sessao registrada: central 2751484124 (10.0.250.21:64883)
info: CentralHub.Api.Services.JflSessionPersistenceService[0]
      Sessao persistida para central 2751484124 (CentralId=1)
info: CentralHub.SDK.Jfl.Server.Handlers.ConnectionCommandHandler[0]
      Central 2751484124 (Active 100 Bus) autenticada; keep-alive definido para 5 min
```

Trecho de log real, do vínculo automático completo confirmado via API (`GET /api/central/1`):

```json
{
  "id": 1,
  "nome": "Active 100 Bus Real",
  "numeroSerie": "2751484124",
  "fabricante": "JFL",
  "modelo": "Active 100 Bus",
  "firmware": "6.5",
  "status": "Online",
  "ultimoKeepAliveEmUtc": "2026-07-13T22:51:58.9178782",
  "ultimoIpConectado": "10.0.250.21",
  "conectadoDesdeUtc": "2026-07-13T22:51:58.3002426"
}
```

## 10. Checklist de homologação (para uma central nova)

- [ ] Backend rodando, `JflTcpServer` escutando na porta configurada (log confirmado).
- [ ] Regra de firewall de entrada liberada na máquina do servidor.
- [ ] Roteamento de rede confirmado entre a rede da central e a rede do servidor (teste de porta
      TCP, não só `ping`).
- [ ] Central configurada via ActiveNet: `IP1`/`Porta1` apontando para o servidor, "Reporte via
      rede Ethernet/Wi-Fi" habilitado.
- [ ] Log `"Conexao TCP aceita"` observado.
- [ ] Log `"Primeiro comando recebido ... Cmd=0x21"` observado.
- [ ] `NumeroSerie`, `Modelo`, `Versao` decodificados corretamente e conferidos manualmente contra
      o equipamento físico.
- [ ] Sessão persistida (`CentralId` não nulo, se a Central já estava cadastrada).
- [ ] `GET /api/centrais/{id}/status` retorna 200 com dados plausíveis.
- [ ] Ciclo de Keep-Alive observado pelo menos uma vez (aguardar o intervalo configurado).

## 11. Casos de uso reais

Este documento em si é o caso de uso real mais importante do projeto — é a prova de que a
implementação não é apenas teoricamente correta (seguindo o manual), mas genuinamente funcional
contra hardware físico.

## 12. Boas práticas

- Nunca declare um comando "pronto" só porque os testes automatizados passam — sempre que possível,
  valide contra hardware real também (ou, quando o risco físico não permitir, contra uma simulação
  fiel, como foi feito para PGM).
- Documente, sempre, o firmware exato usado na homologação — comportamentos podem mudar entre
  versões.

## 13. Problemas comuns em campo

Ver [`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md), seção 15, para os problemas de
rede mais comuns (que foram, em sua maioria, descobertos durante esta própria homologação).

## 14. Como testar uma central nova

Seguir o checklist da seção 10. Para um passo a passo mais detalhado de configuração de ambiente,
ver [`13_DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md).

## 15. Como depurar uma homologação que não avança

1. Confirmar que o Backend está rodando e escutando (log de inicialização).
2. Confirmar firewall (ver [`03_NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md)).
3. Confirmar configuração da central via ActiveNet.
4. Se tudo isso estiver certo e ainda assim não houver log de conexão, considerar captura de
   tráfego de rede (Wireshark) do lado do servidor, para confirmar se o pacote ao menos chega à
   interface de rede da máquina.

## 16. FAQ

**P: Esta homologação vale para qualquer Active 100 Bus, ou só para esta unidade física
específica?**
R: A implementação do protocolo é a mesma para qualquer unidade do mesmo modelo/firmware — mas
recomenda-se sempre confirmar contra a unidade real de cada nova instalação, seguindo o checklist
da seção 10, porque particularidades de rede/configuração são específicas de cada instalação.

**P: E se a versão de firmware for diferente de 6.5?**
R: O protocolo de rede documentado pela JFL é, historicamente, estável entre revisões de firmware
(a JFL só adiciona campos no final, nunca reordena — ver
[`02_JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md)) — mas nenhuma outra versão foi
efetivamente testada por este projeto. Trate como "deveria funcionar, não homologado" até validar.

## 17. Checklist final

- [ ] Sei os 4 problemas reais encontrados durante a homologação e como cada um foi resolvido.
- [ ] Sei a ficha técnica completa do equipamento homologado.
- [ ] Sei o checklist de homologação de uma central nova.
- [ ] Sei por que PGM foi validado contra simulação, não hardware real.

---

**Documento anterior:** [`10_HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md)
**Próximo documento:** [`12_FAQ.md`](12_FAQ.md)
**Índice geral:** [`INDEX.md`](INDEX.md)
