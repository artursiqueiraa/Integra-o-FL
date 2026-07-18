# 11 — Zonas (Inibir/Desinibir/Consultar) — implementado, Fase 3

> Status: ✅ implementado e testado (contra `JflTcpServer` real + Central Simulator, sem hardware
> físico ainda — mesma ressalva da Fase 2, ver [`10_ARM.md`](10_ARM.md)).

## Achado crítico: ordem de bits diferente entre comando e status

O comando de Inibir Zonas (§4.6, superusuário) e o campo `P-INIB` da resposta de status (§4.10)
são dois bitmaps de 13 bytes/99 zonas **com convenções de bit opostas** — confirmado com 3
exemplos reais do manual, não é suposição:

- **Comando `0x52` (envio)**: bit mais significativo (bit 7) = zona **menor** do byte; bit menos
  significativo (bit 0) = zona **maior**.
  - `80 00...00` = "Inibir a zona 01" → `1000 0000` → bit7 = zona 1. ✓
  - `F0 00...00` = "Inibir zonas 01 a 04" → `1111 0000` = zonas 1,2,3,4. ✓
  - `FF 80 00...` = "Inibir zonas 01 a 09" → byte1=`FF` (zonas 1-8), byte2 bit7=`80` = zona 9
    (byte2 cobre zonas 9-16). ✓
- **Campo `P-INIB` da resposta 4.10 (permissão de inibir)**: bit **menos** significativo = zona
  **menor** — já implementado corretamente em `ZoneStatus.cs`/`CentralStatusResponse.cs`, e
  confirmado pelo próprio changelog do manual (*"Revisão 02: Correção do byte de permissão de
  inibir zonas no comando de status... O documento estava informando o bit mais significativo
  como zona 1 e é o bit menos significativo"*) — essa correção histórica é **específica do campo
  de status/permissão**, não se aplica ao comando `0x52`.

**Um `ZoneInhibitCommandService` que reaproveitasse a lógica de bit do `P-INIB` inibiria a zona
errada silenciosamente.** Este é exatamente o tipo de erro que a conferência byte a byte contra
o manual existe para evitar.

## Semântica: substituir, não somar

Cada comando `0x52` carrega o conjunto **completo** de zonas que devem ficar inibidas depois
dele — confirmado pela sequência de exemplos do manual (inibir zona 1, depois um comando novo e
independente inibir zonas 1-4, depois outro inibir 1-9 — cada um redefine o estado completo).
**Inibir e Desinibir usam o mesmo comando `0x52`**: calcula-se o novo conjunto completo (atual ±
a zona alvo) e reenvia o bitmap inteiro.

## Consultar — não precisa de comando novo

O campo `ZONA` da resposta de status (já implementado, `ZoneState.Inibida = 1`) já informa quais
zonas estão inibidas *agora* — diferente de `P-INIB`, que é permissão, não estado. "Consultar
zonas inibidas" = filtrar `CentralStatusResponse.Zonas` por `Estado == ZoneState.Inibida`, 100%
suportado hoje sem tocar o SDK.

## Formato do comando

```
Envio:    0x52 ZONA(13, bitmap MSB-first por byte)
Resposta: formato "tela monitorar" completo (§4.10)
```

## Fora de escopo (decisão explícita)

O manual também documenta Inibir/Desinibir **por senha, uma zona por vez** (§5.2, `0x37`/`0xC3`
e `0x37`/`0xCF`) — resposta totalmente diferente (`0x37 0x03 0xC0 RESP`, não reaproveita
`CentralStatusResponse`). Fica fora desta fase — o pedido original ("Bitmap. Todas as 99 zonas")
descreve exatamente o comando `0x52` de superusuário.

## Arquivos criados (Fase 3)

- `SDK/CentralHub.SDK/Jfl/Server/ZoneInhibitCommandService.cs` —
  `InibirZonasAsync(numeroSerie, IReadOnlySet<int> zonasQueDevemFicarInibidas, ct)`, empacota os
  13 bytes com `bit = 7 - ((zona-1) % 8)`, `byteIndex = (zona-1) / 8` (MSB-first confirmado
  acima). Método único, exatamente como planejado — a lógica de "somar/tirar uma zona do conjunto
  atual" fica no Backend (ver abaixo), não no SDK. Registrado em
  `JflServiceCollectionExtensions.AddJflServer`. Testado em
  `SDK/CentralHub.SDK.Tests/Server/ZoneInhibitCommandServiceTests.cs` (9 testes, incluindo os 3
  exemplos reais do manual: zona 1 → `0x80`, zonas 1-4 → `0xF0`, zonas 1-9 → `0xFF 0x80`).
- Backend: `Services/ZoneInhibitService.cs` — consulta o estado atual via
  `CentralStatusQueryService` (0x4D, o mesmo já usado por `CentralStatusService`), calcula o novo
  conjunto completo (atual + a zona alvo, ou atual - a zona alvo) e só então chama
  `ZoneInhibitCommandService`; `DTOs/ZoneInhibitDtos.cs` (`ZoneInhibitResultDto`); 3 novas actions:
  `POST ~/api/centrais/{id}/zonas/{zona}/inibir`, `.../desinibir`,
  `GET ~/api/centrais/{id}/zonas/inibidas`. Testado contra `JflTcpServer` real + Central Simulator
  em `Backend/CentralHub.Api.Tests/ZoneInhibitServiceTests.cs` (6 testes, incluindo um que prova a
  semântica de substituição não afeta outras zonas já inibidas).
- Frontend: `components/ZonasPanel.tsx` — os chips de zona já existentes na Tela Central agora são
  clicáveis quando `permiteInibir=true` (o próprio dado já vem do parser homologado da resposta
  4.10), com diálogo de confirmação antes de inibir/desinibir.

---

**Índice:** [`00_INDEX.md`](00_INDEX.md)
