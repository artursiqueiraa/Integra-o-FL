# 02 — Rede e captura (Wireshark)

> **Público-alvo:** quem precisa capturar tráfego real entre uma central JFL e o CentralHub para
> homologar um comando (Fase 7) ou depurar um problema de campo.

---

## 1. Onde o tráfego acontece

A central **disca para fora** (nunca o contrário) — abre um socket TCP contra o IP/porta do
CentralHub configurados na própria central (via ActiveNet). O servidor JFL do CentralHub escuta
na porta configurada em `Jfl:Porta` (`appsettings.json`, padrão `8085`). Todo o protocolo roda
em texto claro (sem TLS) sobre essa única porta — handshake, keep-alive, eventos e comandos de
superusuário compartilham a mesma conexão TCP persistente.

## 2. Capturando com Wireshark

1. Instale o [Wireshark](https://www.wireshark.org/) na máquina que roda o CentralHub (ou em um
   ponto da rede que veja o tráfego — ex.: a própria máquina, um switch com port mirroring, etc.).
2. Escolha a interface de rede correta (a que recebe a conexão da central).
3. Aplique o filtro:
   ```
   tcp.port==8085
   ```
   (troque `8085` se a porta configurada for outra).
4. Deixe capturando e provoque a interação que você quer registrar (ex.: reinicie a central para
   capturar o handshake completo, ou acione uma PGM pela tela do CentralHub).
5. Pare a captura assim que tiver o que precisa — sessões JFL ficam abertas por muito tempo
   (só o keep-alive já gera tráfego a cada 1-20 minutos), não é preciso capturar a sessão inteira.

## 3. Separando RX de TX

- **RX (central → CentralHub)**: pacotes com origem no IP da central, destino na porta 8085 do
  servidor. É aqui que aparecem Handshake (0x21), KeepAlive (0x40, pedido) e Evento (0x24).
- **TX (CentralHub → central)**: origem na porta 8085, destino no IP da central. É aqui que
  aparecem as respostas e os comandos de superusuário que o servidor inicia (Status, Armar,
  Desarmar, PGM, Inibir Zonas, Data/Hora).

No Wireshark, clique com o botão direito num pacote e use **Follow → TCP Stream** para ver a
conversa completa organizada por direção (cores diferentes para cada lado).

## 4. Exportando em hex para o Packet Inspector

1. Com o "Follow TCP Stream" aberto, mude o formato de exibição para **Hex Dump** (canto
   inferior do diálogo).
2. Copie os bytes do pacote que te interessa — cuidado para pegar só um pacote 0x7B por vez
   (cada um começa com `7b` e o segundo byte, `QDE`, diz o tamanho total; conte os bytes até lá).
3. Cole no [Inspetor de Pacotes](../../Frontend/src/pages/dev/PacketInspectorPage.tsx)
   (`/ferramentas/inspetor-pacotes` no Frontend, ver [`17_PACKET_ANALYZER.md`](17_PACKET_ANALYZER.md))
   para decompor campo a campo.
4. Se a captura for para virar um fixture permanente de teste/replay, salve os bytes brutos (não
   o hex-string) em `Documentation/RealCaptures/<Comando>.bin` — ver o `README.md` daquela pasta
   para o formato e convenção de nomes.

## 5. Problemas comuns

- **"Não vejo nada no filtro `tcp.port==8085`"** — confirme que está capturando na interface
  certa, e que o CentralHub está realmente escutando nessa porta (`dotnet run`, ver log
  `Servidor JFL escutando na porta 8085`).
- **"O pacote está cortado/incompleto no hex dump"** — o protocolo pode fragmentar um pacote em
  múltiplos segmentos TCP; sempre use "Follow TCP Stream" (que já remonta a conversa) em vez de
  olhar pacotes IP individuais.
- **"Quero simular tráfego sem esperar a central real conectar"** — use o
  [Central Simulator](15_SIMULATOR.md) (Fase 0.5): ele fala o protocolo de verdade e pode ser
  capturado com Wireshark exatamente como hardware real.

---

**Próximo documento:** [`14_PACKET_REFERENCE.md`](14_PACKET_REFERENCE.md)
**Índice:** [`00_INDEX.md`](00_INDEX.md)
