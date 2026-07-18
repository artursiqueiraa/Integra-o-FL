# 10 — Arme (Armar/Desarmar/Stay/Away) — implementado, Fase 2

> Status: ✅ implementado e testado (contra `JflTcpServer` real + Central Simulator, sem hardware
> físico ainda — ver [`11_HARDWARE_VALIDATION.md`](../11_HARDWARE_VALIDATION.md) para o que já foi
> homologado contra equipamento real, que por enquanto é só Handshake/KeepAlive/Status/PGM).

## Tipo de comando

**Tipo A** (servidor pergunta, central responde) — mesmo padrão já implementado por
`PgmCommandService` (`SDK/CentralHub.SDK/Jfl/Server/PgmCommandService.cs`), que esta fase
espelha exatamente. Exemplo-modelo já escrito em
[`../10_HOW_TO_ADD_NEW_COMMAND.md`](../10_HOW_TO_ADD_NEW_COMMAND.md) §4.

## Formato

```
Envio:    0x4E (Armar) | 0x4F (Desarmar) | 0x53 (ArmarStay) | 0x54 (ArmarAway)  +  PART(1)
Resposta: formato "tela monitorar" completo (§4.10 — ver 14_PACKET_REFERENCE.md), mesmo já
          usado por Status/PGM.
```

`PART`: 1-16 = `0x01`-`0x10`. **`0x63` (99) é valor especial: opera o eletrificador** como se
fosse uma partição — confirmado por captura real ("COMANDO DE ARMAR O ELETRIFICADOR":
`7B 06 03 4E 63 53`). `ArmCommandService` deve aceitar partição até 99 especificamente para esse
valor, tratando como operação no eletrificador — não é um bug de validação, é intencional.

## Confirmação de sucesso

`PartitionState` (já existente, `SDK/CentralHub.SDK/Jfl/Messages/Status/PartitionState.cs`) só
tem `Desarmada`/`Armada`/`ArmadaStay` (+ variantes `EmDisparo`) — **não existe um estado
"ArmadaAway" separado no fio**. `ArmarAsync` e `ArmarAwayAsync` confirmam via
`PartitionState.Armada` (mesmo estado que arme normal); só `ArmarStayAsync` confirma via
`ArmadaStay`. Confirmar contra hardware real na Fase 7 antes de fechar como definitivo — é
consistente com o enum existente, mas o manual não documenta "Away" como estado de fio distinto
de "Armada" simples.

## Capturas reais confirmadas

```
Armar partição 1:    7B 06 42 4E 01 70   (Documentation/RealCaptures/Arme.bin)
Desarmar partição 1: 7B 06 44 4F 01 77   (Documentation/RealCaptures/Desarme.bin)
```

## Arquivos criados (Fase 2)

- `SDK/CentralHub.SDK/Jfl/Server/ArmCommandService.cs` — mesma assinatura de construtor que
  `PgmCommandService` (`SessionManager`, `ILogger<ArmCommandService>`), métodos `ArmarAsync`,
  `DesarmarAsync`, `ArmarStayAsync`, `ArmarAwayAsync`, validação especial de `particao == 99`
  (`ArmarStayAsync` rejeita 99 — eletrificador não tem modo Stay). Confirmação via
  `CentralStatusResponse.Particoes`/`Eletrificador` (mesmo parser 4.10 já homologado).
  Registrado em `JflServiceCollectionExtensions.AddJflServer`. Testado em
  `SDK/CentralHub.SDK.Tests/Server/ArmCommandServiceTests.cs` (14 testes, incluindo o caso do
  eletrificador e a confirmação via `ArmadaStay` vs. `Armada` normal).
- Backend: `Services/ArmService.cs` (espelha `PgmService.cs`), `DTOs/ArmDtos.cs`
  (`ArmCommandResultDto`), 4 novas actions em `Controllers/CentralController.cs`
  (`POST ~/api/centrais/{id}/particoes/{p}/armar|desarmar|armar-stay|armar-away`, `p` aceita 1-16
  ou 99). Testado contra `JflTcpServer` real + Central Simulator em
  `Backend/CentralHub.Api.Tests/ArmServiceTests.cs` (7 testes).
- Frontend: `components/ArmPanel.tsx` (espelha `PgmPanel.tsx`) — um card por partição (1-16) com
  os 4 botões, mais um card especial "Eletrificador" (Armar/Desarmar/Away, sem Stay), diálogo de
  confirmação antes de qualquer comando. Renderizado na Tela Central, entre o bloco de Status e o
  `PgmPanel`.

**Stub `ArmCommandHandlerStub` continua registrado** (Tipo A — só captura respostas
órfãs/atrasadas, mesmo papel que `PgmCommandHandlerStub` já cumpre hoje ao lado do
`PgmCommandService` real).

---

**Índice:** [`00_INDEX.md`](00_INDEX.md)
