# 13 — Data e Hora — planejado, Fase 5

> Status: 📋 especificação confirmada; implementação pendente.

## Formato

```
Envio:    0x55 HORA(1,BCD) MIN(1,BCD) SEG(1,BCD) DIA(1,BCD) MES(1,BCD) ANO(1,BCD)
Resposta: formato "tela monitorar" completo (§4.10) — confirma lendo o campo HORA de volta.
```

Mesmo padrão BCD já usado por `JflBcd`/`ParseDataHora` em `CentralStatusResponse.cs`
(reaproveitado, nunca alterado).

## Captura real

```
7B 0B 4A 55 11 52 16 21 06 21 3C
CMD=0x55 HORA=0x11 MIN=0x52 SEG=0x16 DIA=0x21 MES=0x06 ANO=0x21
```

**Atenção**: os nibbles desta captura específica do manual têm valores que, decodificados
literalmente como BCD padrão, resultam em dígitos fora de 0-9 em alguns campos — pode ser ruído
de transcrição do próprio manual, ou uma nuance do formato não capturada na leitura desta
pesquisa. **Confirmar contra hardware real na Fase 7** antes de considerar o formato
definitivamente fechado — não assumir que esta captura de exemplo está livre de erro.

## Arquivos a criar (Fase 5)

- `SDK/CentralHub.SDK/Jfl/Server/DateTimeCommandService.cs` — `AtualizarAsync(numeroSerie,
  DateTime novaDataHora, ct)`.
- Backend: `Services/DateTimeCommandService.cs`, `DTOs/DateTimeDtos.cs`,
  `POST/GET ~/api/centrais/{id}/data-hora`.

---

**Índice:** [`00_INDEX.md`](00_INDEX.md)
