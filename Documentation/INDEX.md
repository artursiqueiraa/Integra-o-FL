# ÍNDICE GERAL — Documentação Oficial do CentralHub

> Esta é a documentação oficial e definitiva do projeto CentralHub. Ela foi escrita para que
> qualquer pessoa — mesmo sem nenhum conhecimento prévio de centrais de alarme, protocolos
> binários, TCP, C#, React, ou do fabricante JFL — consiga entender o projeto inteiro e assumir sua
> manutenção lendo apenas estes documentos.
>
> Se você é novo no projeto, comece pelo documento 01 e siga a ordem numérica. Se você já conhece o
> projeto e só precisa de uma resposta pontual, vá direto ao documento 12 (FAQ) ou 15 (Glossário).

---

## Como usar esta documentação

- Cada documento é autocontido, mas assume que você já leu os documentos anteriores na ordem
  numérica (01 → 15) — especialmente os conceitos de protocolo (02) e arquitetura (03), que são
  pré-requisito para entender quase tudo depois.
- Todo documento termina com links de navegação: **Documento anterior**, **Próximo documento** e
  **Índice geral** (este arquivo).
- Termos técnicos desconhecidos podem ser procurados a qualquer momento no
  [`15_GLOSSARY.md`](15_GLOSSARY.md).
- Perguntas rápidas e diretas podem ser procuradas no [`12_FAQ.md`](12_FAQ.md) antes de mergulhar
  num documento inteiro.

---

## Trilha de leitura recomendada (do zero ao domínio completo)

```
┌─────────────────────────────────────────────────────────────────────┐
│  FUNDAMENTOS DE NEGÓCIO E PROTOCOLO                                  │
│  01 → 02 → 03 → 04                                                   │
│  "O que é o projeto, o que é o protocolo JFL, como a rede é          │
│   desenhada, e como uma conexão real flui do início ao fim."         │
├─────────────────────────────────────────────────────────────────────┤
│  IMPLEMENTAÇÃO ATUAL                                                 │
│  05 → 06 → 07 → 08 → 09                                              │
│  "Onde cada coisa está no código, como o banco é modelado, como as   │
│   sessões TCP funcionam por dentro, o que cada comando faz byte a    │
│   byte, e como a interface web consome tudo isso."                   │
├─────────────────────────────────────────────────────────────────────┤
│  EXTENSÃO E VALIDAÇÃO                                                │
│  10 → 11                                                             │
│  "Como adicionar um comando novo, e a prova de que tudo isso já foi  │
│   testado contra um equipamento físico real."                        │
├─────────────────────────────────────────────────────────────────────┤
│  REFERÊNCIA RÁPIDA (consultar sob demanda, não precisa ler em ordem) │
│  12 → 13 → 14 → 15                                                   │
│  "Perguntas frequentes, como rodar o projeto no seu ambiente, o que  │
│   falta implementar, e definição de cada termo técnico usado."       │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Sumário dos 15 documentos

| # | Documento | O que você encontra lá |
|---|---|---|
| 01 | [`PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md) | O que é o CentralHub, que problema resolve, história do projeto, arquitetura em alto nível, tecnologias usadas e por quê, fabricantes suportados |
| 02 | [`JFL_PROTOCOL_GUIDE.md`](02_JFL_PROTOCOL_GUIDE.md) | O protocolo binário JFL explicado do zero: pacotes, checksum, SEQ, cada comando, com exemplos reais byte a byte |
| 03 | [`NETWORK_ARCHITECTURE.md`](03_NETWORK_ARCHITECTURE.md) | As 8 camadas de rede do projeto, firewall, portas, cenários reais de topologia |
| 04 | [`PROTOCOL_FLOW.md`](04_PROTOCOL_FLOW.md) | As 12 fases de uma conexão real, do ligar da central ao desligar, com logs reais |
| 05 | [`SOURCE_CODE_GUIDE.md`](05_SOURCE_CODE_GUIDE.md) | Mapa completo de arquivos e classes de SDK, Backend e Frontend; o que é código legado |
| 06 | [`DATABASE_GUIDE.md`](06_DATABASE_GUIDE.md) | SQLite e EF Core do zero, todas as tabelas campo a campo, limitações conhecidas |
| 07 | [`SESSION_MANAGER_GUIDE.md`](07_SESSION_MANAGER_GUIDE.md) | Como uma sessão TCP nasce, vive e morre; correlação de requisição/resposta por dentro |
| 08 | [`COMMANDS_GUIDE.md`](08_COMMANDS_GUIDE.md) | Referência completa de cada comando implementado, com exemplos reais marcados [REAL] |
| 09 | [`WEB_GUIDE.md`](09_WEB_GUIDE.md) | Todas as telas do frontend, como o polling funciona, rotas, componentes |
| 10 | [`HOW_TO_ADD_NEW_COMMAND.md`](10_HOW_TO_ADD_NEW_COMMAND.md) | Tutorial passo a passo para implementar um comando novo, com código de exemplo completo |
| 11 | [`HARDWARE_VALIDATION.md`](11_HARDWARE_VALIDATION.md) | Prova da homologação contra hardware real: ficha técnica, problemas encontrados, logs reais |
| 12 | [`FAQ.md`](12_FAQ.md) | Mais de 100 perguntas e respostas curtas cobrindo todo o projeto |
| 13 | [`DEVELOPER_GUIDE.md`](13_DEVELOPER_GUIDE.md) | Como compilar, rodar, testar, publicar e fazer backup do projeto |
| 14 | [`ROADMAP.md`](14_ROADMAP.md) | O que falta implementar, em que ordem, e por quê |
| 15 | [`GLOSSARY.md`](15_GLOSSARY.md) | Glossário alfabético de todos os termos técnicos usados na documentação |
| — | [`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md) | Documento complementar ao 02/03/09: arquitetura antiga (discagem de saída) vs. arquitetura real (`SessionManager`), com diagramas, fluxogramas, exemplos e FAQ — foco na tela "Centrais" e no painel de monitoramento de sessão |

---

## Índice por assunto (atalho temático)

**Quero entender o protocolo binário da JFL** → [`02`](02_JFL_PROTOCOL_GUIDE.md), [`08`](08_COMMANDS_GUIDE.md)

**Quero entender por que a central conecta no servidor (e não o contrário)** → [`02`](02_JFL_PROTOCOL_GUIDE.md) seção "quem conecta em quem", [`03`](03_NETWORK_ARCHITECTURE.md)

**Quero configurar o ambiente e rodar o projeto pela primeira vez** → [`13`](13_DEVELOPER_GUIDE.md)

**Quero adicionar um comando novo (ex.: Arme)** → [`10`](10_HOW_TO_ADD_NEW_COMMAND.md), [`14`](14_ROADMAP.md) seção 7

**Quero saber se algo já foi testado contra hardware real** → [`11`](11_HARDWARE_VALIDATION.md)

**Quero saber o que uma tabela do banco significa** → [`06`](06_DATABASE_GUIDE.md)

**Quero saber como uma sessão TCP funciona por dentro** → [`07`](07_SESSION_MANAGER_GUIDE.md)

**Quero saber onde um arquivo/classe específico está** → [`05`](05_SOURCE_CODE_GUIDE.md)

**Quero saber o que uma tela do sistema faz** → [`09`](09_WEB_GUIDE.md)

**Quero entender por que o teste de conexão/`ConnectionService` sumiu, e como a tela "Centrais" monitora a sessão agora** → [`ARQUITETURA_SESSION_MANAGER.md`](ARQUITETURA_SESSION_MANAGER.md)

**Tenho uma central conectando mas não sei por quê nada acontece** → [`03`](03_NETWORK_ARCHITECTURE.md), [`04`](04_PROTOCOL_FLOW.md), [`11`](11_HARDWARE_VALIDATION.md) seção 15

**Não sei o que um termo técnico significa** → [`15`](15_GLOSSARY.md)

**Tenho uma dúvida rápida e não quero ler um documento inteiro** → [`12`](12_FAQ.md)

---

## Convenções usadas em toda a documentação

- **[REAL]** ao lado de um exemplo indica que o dado foi capturado de fato — do manual oficial da
  JFL ou de logs reais de hardware durante a homologação deste projeto — não é um exemplo
  inventado.
- Diagramas em ASCII (caixas e setas feitas de caracteres de texto) são usados propositalmente em
  vez de imagens, para que a documentação continue legível em qualquer lugar (terminal, editor de
  texto puro, controle de versão) sem depender de arquivos binários externos.
- Toda seção de "Boas práticas" reflete uma decisão real tomada durante o desenvolvimento deste
  projeto, não uma recomendação genérica de livro-texto.
- Toda seção de "Problemas comuns" documenta um problema **realmente encontrado** durante o
  desenvolvimento/homologação, com a causa raiz e a solução real aplicada.

---

**Início da trilha de leitura:** [`01_PROJECT_OVERVIEW.md`](01_PROJECT_OVERVIEW.md)
