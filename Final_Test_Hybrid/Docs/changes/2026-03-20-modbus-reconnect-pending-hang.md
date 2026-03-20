# 2026-03-20 modbus reconnect pending hang

## Failure mode

- После потери связи с котлом queued Modbus-команда могла зависнуть навсегда, если уже стояла в очереди, но ещё не была взята worker'ом.
- Типичный production path: `Ready_1` уже получен, step ставит `WriteUInt16Async`/`ReadUInt16Async`, worker уходит в reconnect-loop, а `command.Task` не завершается.
- В этом сценарии шаг не доходит до своей fail-ветки, не пишет `Fault=true` в PLC и не оставляет ожидаемый step-log.
- Отдельный race того же периода: writer мог зависнуть на `WriteAsync` в заполненный bounded channel, а после закрытия/восстановления очереди потерять reconnect-классификацию и выйти не через ожидаемый reconnect-fail.

## Root cause

- Очередь диспетчера завершала pending команды только в `StopAsync` через `CancelAllPendingCommands()`.
- reconnect-path не завершал pending queued команды вообще.
- Во время `IsReconnecting` новые команды продолжали попадать в очередь для non-`UI.*` источников, поэтому даже после начала reconnect оставался путь к повторному зависанию.

## Resolution

- reconnect-path теперь:
  - сразу помечает `IsReconnecting=true`;
  - закрывает каналы очереди на запись;
  - завершает pending queued команды communication-fail;
  - reject'ит новые команды того же периода тем же communication-fail;
  - переводит writer'ов, ждавших место в полном bounded channel, в тот же reconnect-fail текущего периода;
  - после успешного повторного открытия порта пересоздаёт каналы и принимает только новые операции.

## Verification

- Добавлены регрессии на:
  - reconnect drain pending high/low команд;
  - сохранение cancel-path для `StopAsync`;
  - reject новых команд во время reconnect;
  - восстановление очереди после повторного открытия порта;
  - writer waiting on full queue, попавший в reconnect-period.

## Notes

- Этот change-set не лечит hang после успешного выхода из Modbus IO в чистое PLC wait (`Ready_1 -> phase2 wait`).
- Отдельного incident-registry в репозитории не обнаружено; данный change-doc используется как явная фиксация нового failure mode и должен упоминаться из impact.
