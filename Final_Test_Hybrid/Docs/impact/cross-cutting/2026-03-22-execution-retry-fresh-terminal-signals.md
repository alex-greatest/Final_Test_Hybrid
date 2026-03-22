# 2026-03-22 execution retry fresh terminal signals

## Summary

- Для execution PLC-block шагов добавлен runtime fresh-barrier на terminal сигналы `End/Error`.
- После успешного `Start=true` execution шаг принимает только свежие OPC updates текущей попытки.
- Retry-path по-прежнему не делает pre-wait на `Block.End=false` и не очищает runtime-cache.

## Why

- На retry execution PLC-step мог мгновенно завершиться по stale `Block.Error=true` или `Block.End=true`, которые оставались в OPC runtime-cache от прошлой попытки.
- Это особенно проявлялось в фазовых шагах, где между `Start=true` и terminal wait есть дополнительная логика (`Ready_*`, измерения, `Fault/Continua`).
- Простая очистка cache была небезопасна: можно потерять уже пришедший свежий `End/Error` текущей попытки.

## What Changed

- `OpcUaSubscription` теперь хранит sequence номер последнего valid update для каждого cached тега.
- Внутренний sequence/barrier тип fresh-gate переведён на `ulong`; это не меняет поведение, но убирает лишнюю тревогу вокруг теоретического переполнения.
- Для `TagWaiter` добавлен execution-scoped fresh-filter:
  - scope открывается в `ColumnExecutor` только для execution PLC-block шага;
  - перед записью `Start=true` снимается sequence snapshot, который commit'ится только при успешной записи;
  - только terminal `End/Error` текущего PLC-блока требуют `updateSequence > barrier`.
- Callback-path подписки теперь локально логирует и гасит synchronous throw до возврата `Task`, чтобы ошибка обработчика не вываливалась наружу из notification-loop.
- Остальные wait'ы и non-execution контуры сохраняют прежний cache-first контракт.

## Docs Updated

- `Docs/runtime/TagWaiterGuide.md`
- `Docs/execution/RetrySkipGuide.md`
- `Docs/execution/StepsGuide.md`

## Verification

- `dotnet build Final_Test_Hybrid.slnx`
- `dotnet test Final_Test_Hybrid.Tests/Final_Test_Hybrid.Tests.csproj --no-build`
- `GasValveTubeDeferredErrorServiceTests.CompletedDelay_RaisesPlcErrorAndKeepsMessageUntilFalse` переведён с `Task.Yield()` на ожидание факта `RaisePlc`; после правки прошёл `10/10` изолированно и `5/5` в полном suite.
- Добавлены/обновлены runtime regression tests:
  - `TagWaiterFreshSignalGateTests`
  - `TagWaiterWaitAnyAsyncTests`
  - `TagWaiterWaitForFalseAsyncTests`
  - `PreExecutionAutoReadyGateTests`
  - `OpcUaSubscriptionCallbackSafetyTests`

## Notes

- UI, completion-flow, skip-flow и pre-execution retry этим change-set не менялись по поведению.
- Cache не очищается на retry; это сознательная часть решения, чтобы не терять уже пришедший свежий terminal signal.
- Отдельный flaky test `GasValveTubeDeferredErrorServiceTests.CompletedDelay_RaisesPlcErrorAndKeepsMessageUntilFalse` стабилизирован через детерминированное ожидание side effect вместо `Task.Yield()`.
- no new incident
