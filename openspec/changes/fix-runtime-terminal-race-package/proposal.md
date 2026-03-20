# Change: Fix Runtime Terminal Race Package

## Why

В runtime были подтверждены три связанных failure mode:
- terminal decision-loop в completion/post-AskEnd принимал `unknown cache` за `false`;
- `TagWaiter.WaitForFalseAsync` мог ложно завершаться после subscribe/resume на пустом cache;
- `ConnectionTestPanel.DisposeAsync()` мог затронуть shared `IModbusDispatcher`, даже если панель его не запускала.

Эти ошибки затрагивают owner-границы reset/completion, interrupt semantics и корректное владение shared diagnostic transport.

## What Changes

- Добавить safe-read контракт `OpcUaSubscription.TryGetValue<T>(...)` и перевести на него только safety-critical decision-loop'и completion/post-AskEnd.
- Вынести terminal ownership completion/post-AskEnd в отдельный singleton `RuntimeTerminalState`.
- Сузить ownership `AutoReady` в `ErrorCoordinator`: OFF не поднимает `AutoModeDisabled` в terminal window, ON резюмит только `AutoModeDisabled`.
- Локально исправить `TagWaiter.WaitForFalseAsync`, не меняя `WaitGroup/WaitForAllTrue`.
- Сохранить ownership shared `IModbusDispatcher` в `ConnectionTestPanel`.
- Добавить unit-test проект для helper/runtime инвариантов, не заявляя полное orchestration-покрытие completion/post-AskEnd, и зафиксировать change trail в docs/impact/openspec.

## Impact

- Affected specs: `runtime-terminal-gating`
- Affected code:
  - `Services/OpcUa/Subscription/OpcUaSubscription.Callbacks.cs`
  - `Services/OpcUa/TagWaiter.cs`
  - `Services/Steps/Infrastructure/Execution/*`
  - `Components/Overview/ConnectionTestPanel.razor`
