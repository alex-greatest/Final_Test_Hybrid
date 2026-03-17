# Impact: Увеличение окна ожидания AskEnd до 120 секунд

## Контекст

- Контур: `runtime`
- Затронутые подсистемы: `PLC reset flow`, `OpcUa settings`, `runtime docs`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: repo default reset-flow ждал `Ask_End` только `60` секунд, что стало недостаточным для целевого сценария мягкого PLC reset.
- Причина сейчас: требовалось увеличить именно окно ожидания `Ask_End`, не меняя порядок reset-flow и не трогая fail-fast ветки по потере связи.

## Что изменили

- Подняли кодовый default `AskEndTimeoutSec` с `60` до `120` секунд.
- Подняли кодовый default `ResetHardTimeoutSec` с `60` до `120` секунд, чтобы сохранить валидность конфигурации (`Hard >= AskEnd`).
- Подняли repo default в `appsettings.json` до `AskEnd=120`, `ReconnectWait=15`, `Hard=120`.
- Не меняли runtime-логику `PlcResetCoordinator`: порядок `ForceStop -> Write Reset -> Wait AskEnd -> timeout/fail-fast` остался прежним.
- Синхронизировали stable docs и архитектурное описание под новый timeout-контракт.

## Где изменили

- `Final_Test_Hybrid/Settings/OpcUa/ResetFlowTimeoutsSettings.cs` — новые кодовые default для `AskEndTimeoutSec` и `ResetHardTimeoutSec`.
- `Final_Test_Hybrid/appsettings.json` — новые repo default таймаутов reset-flow.
- `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md` — обновлены значения default таймаутов.
- `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md` — добавлен repo default timeout-контракт reset-flow.
- `Final_Test_Hybrid/CLAUDE.md` — синхронизирован runtime summary по timeout-конфигурации.
- `Final_Test_Hybrid/ARCHITECTURE.md` — обновлён timeout в диаграмме reset-flow.

## Когда делали

- Исследование: `2026-03-17 15:05 +04:00`
- Решение: `2026-03-17 15:12 +04:00`
- Правки: `2026-03-17 15:17 +04:00` - `2026-03-17 15:18 +04:00`
- Проверки: `2026-03-17 15:18 +04:00` - `2026-03-17 15:21 +04:00`
- Финализация: `2026-03-17 15:21 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 15:05 +04:00` | Сверили `PlcResetCoordinator`, `ResetFlowTimeoutsSettings`, `appsettings.json` и stable docs по reset-flow. | Подтвердить фактический источник timeout-значений перед изменением. |
| `2026-03-17 15:12 +04:00` | Зафиксировали минимальное решение: `AskEnd=120`, `Hard=120`, `ReconnectWait=15` без изменения reset-логики. | Увеличить окно ожидания `Ask_End`, не ломая валидацию конфигурации. |
| `2026-03-17 15:17 +04:00` | Обновили кодовые defaults, repo config и source-of-truth docs. | Синхронно изменить runtime default и документацию. |
| `2026-03-17 15:18 +04:00` | Прогнали build, оба `dotnet format --verify-no-changes`, Rider inspection, `inspectcode` и replay-аудиты. | Подтвердить, что change-set не ломает сборку, проходит process gates и не создаёт новых IDE-проблем. |
| `2026-03-17 15:21 +04:00` | Проверили значения `appsettings.json` по тем же условиям, что использует `ResetFlowTimeoutsSettings.Validate()`. | Зафиксировать, что новая пара `AskEnd=120` / `Hard=120` проходит текущие validation rules. |

## Проверки

- Команды / проверки:
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
  - `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
  - Rider `get_file_problems` по `Final_Test_Hybrid/Settings/OpcUa/ResetFlowTimeoutsSettings.cs`
  - `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Settings/OpcUa/ResetFlowTimeoutsSettings.cs" --no-build --format=Text "--output=<temp>" -e=WARNING`
  - `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Settings/OpcUa/ResetFlowTimeoutsSettings.cs" --no-build --format=Text "--output=<temp>" -e=HINT`
  - Проверка `appsettings.json` по тем же numeric rules, что в `ResetFlowTimeoutsSettings.Validate()` (`AskEnd 5..300`, `ReconnectWait 1..120`, `Hard 5..300`, `Hard >= AskEnd`, `Hard >= ReconnectWait`)
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\agents-operational-guardrails\scripts\replay_governance_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\source-of-truth-doc-sync\scripts\replay_doc_sync_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
- Результат:
  - `dotnet build` прошёл успешно; остался только известный warning `MSB3277` по `WindowsBase`.
  - Оба `dotnet format --verify-no-changes` прошли.
  - Rider `get_file_problems` по `ResetFlowTimeoutsSettings.cs` вернул пустой список.
  - `jb inspectcode -e=WARNING` по `ResetFlowTimeoutsSettings.cs` вернул пустой report.
  - `jb inspectcode -e=HINT` показал только suggestions `Auto-property can be made get-only` для трёх auto-property; изменения не вносились, потому что для config binding нужны set-аксессоры.
  - Проверка значений `appsettings.json` по текущим validation rules прошла: `AskEnd=120`, `ReconnectWait=15`, `Hard=120`.
  - governance replay прошёл успешно.
  - doc-sync replay прошёл успешно.

## Риски

- Окно ожидания `Ask_End` стало вдвое длиннее, поэтому сценарий с отсутствующим подтверждением PLC теперь дольше удерживает reset-flow до timeout-path.
- `ResetHardTimeoutSec` поднят ровно до `120` секунд без дополнительного запаса; при будущем увеличении `AskEndTimeoutSec` этот инвариант снова нужно менять синхронно.
- Fail-fast по `PlcConnectionLost` не трогался; если оператор ожидал более ранний timeout при живом PLC, поведение изменится только по длительности ожидания.

## Открытые хвосты

- Ручной app-level прогон сценария `нет Ask_End` и сценария `PlcConnectionLost` в этой задаче не выполнялся; это остаётся рекомендуемой операционной проверкой перед выкладкой.
- Прямой вызов `ResetFlowTimeoutsSettings.Validate()` из собранной app-сборки через PowerShell не удалось использовать из-за ограничений загрузки/компиляции вне runtime host; вместо этого зафиксирована автоматическая проверка по тем же условиям, что в текущем методе `Validate()`.
- `no new incident`

## Связанные планы и документы

- План: `Пользовательская задача от 2026-03-17: увеличить окно ожидания AskEnd до 120 секунд.`
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/runtime/PlcResetGuide.md`
  - `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md`
  - `Final_Test_Hybrid/CLAUDE.md`
  - `Final_Test_Hybrid/ARCHITECTURE.md`
- Related impact:
  - `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-17-sequence-clear-mode-doc-sync.md`
  - `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-17-reset-history-ui-without-export.md`

## Сводит impact

- `Не применимо`
