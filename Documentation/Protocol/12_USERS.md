# 12 — Usuários (Consulta/Programação) — planejado, Fase 4

> Status: 📋 especificação confirmada; implementação pendente.

## Formato — resposta totalmente diferente de §4.10

Envelope `0x37`, sub-comando no primeiro byte de dados — **não reaproveita
`CentralStatusResponse`**.

### Consulta de usuário (`0x37`, `0xC8`)

Único comando de senha que **não verifica a senha** (usada só para identificar o operador):

```
Envio:    0x37 0x06 0xC8 SENHA(3,BCD) USU(1)
Sucesso:  0x37 0x09 0xC8 SENHA(1: 0x00 sem senha / 0x01 com senha) ATRIB(6, bitmap)
Erro:     0x37 0x03 0xC0 RESP(1)
```
`USU`: 0=mestre (todos atributos ativos), 1-98=comuns, 99=instalador (sempre zerado).

### Programar senha e atributos (`0x37`, `0xC9`)

```
Envio:    0x37 0x11 0xC9 SENHA(3,BCD) USU(1) ALT-S(1) NOVA-SENHA(3,BCD) ALT-A(1) ATRIB(6)
Resposta: 0x37 0x03 0xC0 RESP(1)
```
`ALT-S`/`ALT-A`: flags independentes (0x00/0x01) — altera só senha, só atributos, ou os dois.
Usuário 00 sempre pode programar senha; 01-98 só a própria; 99 só se liberado (endereço 300).

### Bitmap `ATRIB` (6 bytes)

| Byte | Bits |
|---|---|
| 0 | Opera partições 1-8 |
| 1 | Opera partições 9-16 |
| 2 | bit0 Desarmar, bit1 AWAY, bit2 Inibir zonas, bit3-6 PGM1-4, bit7 Acesso remoto |
| 3 | bit0 Ronda, bit1 Opera eletrificador, bit2 SMS no disparo, bit3 Discagem no disparo, bit4-7 PGM5-8 |
| 4 | PGM9-16 |
| 5 | Reservado |

### Tabela `RESP` (compartilhada por todos os comandos `0x37`, §5.1-§5.5)

| Código | Significado |
|---|---|
| `0xBE` | ACK |
| `0xA0` | Pacote inválido (checksum) |
| `0xA1` | Erro de senha |
| `0xA2` | Comando/parâmetro inválido |
| `0xA8` | Sem permissão |
| `0xA9` | Função não programada |
| `0xAA` | Bloqueado (5 senhas erradas) |
| `0xAB` | Função não existente (produto não suporta) |
| `0xAC` | Já estava no estado pedido |

## Fora de escopo (decisão explícita)

`0xC1` (Armar/Desarmar com senha), `0xC3`/`0xCF` (Inibir/Desinibir com senha), `0xC7` (PGM com
senha) — funcionalidade equivalente já coberta pelo caminho de superusuário (Fases 2-3), fora
desta leva.

## Segurança

**Senha nunca é logada nem retornada em texto puro pela API** — mesma disciplina já seguida pelo
campo `Central.Senha` legado (nunca devolvido pela API existente).

## Arquivos a criar (Fase 4)

- `SDK/CentralHub.SDK/Jfl/Messages/PasswordCommandResponse.cs` — parser da resposta curta
  `0x37 0x03 0xC0 RESP`.
- `SDK/CentralHub.SDK/Jfl/Messages/ConsultaUsuarioResponse.cs` — parser da resposta longa
  (e da curta de erro, discriminando pelo tamanho do pacote).
- `SDK/CentralHub.SDK/Jfl/Server/UsuarioCommandService.cs` — `ConsultarAsync`, `ProgramarAsync`.
- Backend: `Services/UsuarioCommandService.cs`, `DTOs/UsuarioDtos.cs`, novas actions
  `GET/PUT ~/api/centrais/{id}/usuarios/{n}`.

---

**Índice:** [`00_INDEX.md`](00_INDEX.md)
