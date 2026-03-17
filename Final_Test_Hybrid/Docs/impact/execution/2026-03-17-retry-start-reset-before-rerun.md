# Impact: Сброс Start перед retry PLC-шага без hard reset

## Контекст

- Контур: `execution`
- Затронутые подсистемы: `TestExecutionCoordinator`, `PreExecutionCoordinator`, `ExecutionStateManager`, `ColumnExecutor`, `execution docs`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: перед повтором PLC-шага runtime не сбрасывал `Start=false`, из-за чего retry не был выровнен со skip-path и оставлял неоднозначный PLC-state между попытками.
- Причина сейчас: нужно было добавить `Start=false` перед rerun и при этом не вводить регресс в error-flow, не терять текущую ошибку из очереди и не переводить неуспешную запись `Start=false` в `HardReset`.

## Что изменили

- В runtime retry-flow добавили подготовительный шаг `Start=false` после `AskRepeat` и после ожидания `Req_Repeat=false`, но до `RetryRequested` и `DequeueError()`.
- Для runtime-ветки добавили обычную обработку ошибки `PLC не сбросил Start перед повтором`: без rerun, без `HardReset`, с сохранением ошибки в голове очереди и повторным входом в стандартный Retry/Skip цикл.
- В `ExecutionStateManager` добавили безопасное обновление head-элемента очереди ошибок без `Dequeue/Enqueue`, чтобы не терять текущий failed-step.
- В `ColumnExecutor` добавили обновление текста уже failed шага без смены остального состояния исполнителя.
- В pre-execution retry для `BlockBoilerAdapterStep` добавили `Start=false` перед повтором; ошибка записи теперь возвращается как retryable step error с тем же операторским сообщением `PLC не сбросил Start перед повтором`.
- Обновили stable docs `RetrySkipGuide.md` и `StateManagementGuide.md` под новый retry-flow и под pre-execution поведение.
- Новый incident не выявлен: `no new incident`.

## Где изменили

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs` — вставили `Start=false` перед rerun и обработку step-level ошибки без `HardReset`.
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs` — добавили helper записи `Start=false` для retry-path.
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs` — добавили `Start=false` перед retry `BlockBoilerAdapterStep` и retryable error при неуспешной записи.
- `Final_Test_Hybrid/Models/Steps/ExecutionStateManager.cs` — добавили обновление текущей ошибки в голове очереди.
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs` — добавили обновление текста уже failed шага.
- `Final_Test_Hybrid/Docs/execution/RetrySkipGuide.md` — зафиксировали новый runtime retry-flow и правило `DequeueError()` только после успешного `Start=false`.
- `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md` — зафиксировали pre-execution retry для `BlockBoilerAdapterStep` без перевода ошибки в `HardReset`.

## Когда делали

- Исследование: `2026-03-17 13:55 +04:00`
- Решение: `2026-03-17 14:10 +04:00`
- Правки: `2026-03-17 14:15 +04:00` - `2026-03-17 14:58 +04:00`
- Проверки: `2026-03-17 14:25 +04:00` - `2026-03-17 15:11 +04:00`
- Финализация: `2026-03-17 15:11 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 13:55 +04:00` | Проверили stable docs execution-контура и отсутствие active impact в `Docs/impact/execution/`. | Подтвердить исходный workflow и не опираться на предположения. |
| `2026-03-17 14:10 +04:00` | Зафиксировали правило: `Start=false` перед retry, но ошибка записи остаётся обычной step error без `HardReset`. | Не допустить регресс в error-flow и не потерять операторский Retry/Skip сценарий. |
| `2026-03-17 14:15 +04:00` | Добавили `TryResetBlockStartBeforeRetryAsync`, обновление текста failed-шага и обновление head-ошибки в очереди. | Реализовать retry-path без раннего `DequeueError()` и без потери текущей ошибки. |
| `2026-03-17 14:41 +04:00` | Добавили `Start=false` в pre-execution retry `BlockBoilerAdapterStep` и возврат retryable error при неуспешной записи. | Синхронизировать pre-execution поведение с runtime по смыслу операторской ошибки. |
| `2026-03-17 14:47 +04:00` | Обновили `RetrySkipGuide.md` и `StateManagementGuide.md`. | Зафиксировать новый source-of-truth в том же change-set. |
| `2026-03-17 14:58 +04:00` | Уплотнили `ProcessRetryAsync` и `HandleErrorsIfAny`, чтобы убрать новый выход за лимит `<= 50` строк по методу. | Снять новый method-length regression без дополнительного runtime-рефакторинга. |
| `2026-03-17 15:11 +04:00` | Прогнали build, format, Rider inspections, governance replay и decomposition replay; зафиксировали legacy tails и ограничение среды для anti-overengineering script. | Завершить change-set с явной верификацией и остаточными рисками. |

## Проверки

- Команды / проверки:
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\agents-operational-guardrails\scripts\replay_governance_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\lada-runtime-guardrails\scripts\replay_decomposition_audit.ps1 -RepoRoot . -Files "Final_Test_Hybrid/Models/Steps/ExecutionStateManager.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs"`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\lada-runtime-guardrails\scripts\replay_line_audit.ps1 -Files "Final_Test_Hybrid/Models/Steps/ExecutionStateManager.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs"`
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
  - `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
  - `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Steps/ExecutionStateManager.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs" --no-build --format=Text -e=WARNING`
  - `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Steps/ExecutionStateManager.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs" --no-build --format=Text -e=HINT`
  - Rider `get_file_problems` для всех изменённых `*.cs`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\overengineering-detector\scripts\replay_overengineering_audit.ps1 -RepoRoot . -Files "Final_Test_Hybrid/Models/Steps/ExecutionStateManager.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs,Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs"`
- Результат:
  - governance replay прошёл успешно;
  - `dotnet build Final_Test_Hybrid.slnx` прошёл успешно;
  - `dotnet format analyzers ... --verify-no-changes` прошёл успешно;
  - `dotnet format style ... --verify-no-changes` прошёл успешно;
  - Rider `get_file_problems` по изменённым `*.cs` вернул пустой список ошибок и предупреждений;
  - `jb inspectcode ... -e=WARNING` не дал warning findings по change-set;
  - `jb inspectcode ... -e=HINT` дал только hint/suggestion уровня legacy cleanup, без блокирующих дефектов по новой логике;
  - line audit: новых нарушений длины методов нет;
  - decomposition replay остался красным только из-за существующих oversized partial-файлов (`ColumnExecutor.cs`, `TestExecutionCoordinator.ErrorResolution.cs`) и heuristic control-block флагов в legacy/runtime методах;
  - anti-overengineering script не смог выполниться в этой среде: вызов `rg.exe` блокируется политикой ОС с `Access is denied`; change-set был дополнительно проверен вручную по diff и не содержит новых passthrough-wrapper абстракций или дублирующих слоёв;
  - в сборке остались существующие предупреждения `MSB3277` по конфликту `WindowsBase 4.0.0.0` vs `5.0.0.0`; текущий change-set их не вносил.

## Риски

- Runtime retry теперь делает одну дополнительную PLC-запись `Start=false` перед rerun; на стенде нужно подтвердить, что PLC-контракт не требует удерживать старый `Start=true` до собственного внутреннего сброса.
- При неуспешном `Start=false` сообщение шага заменяется на обобщённое `PLC не сбросил Start перед повтором`; деталь PLC write error остаётся в логах, но не показывается оператору.
- В execution-контуре остаются legacy oversized/runtime partial-файлы и heuristic control-block хвосты; этот change-set их не расширил, но и не устранил полностью.

## Открытые хвосты

- Нужна отдельная безопасная задача на декомпозицию `ColumnExecutor.cs` и `TestExecutionCoordinator.ErrorResolution.cs` до проектных лимитов по размеру partial-файлов.
- Anti-overengineering replay script нужно либо адаптировать под среду без исполнимого `rg.exe`, либо запускать в окружении без policy-блокировки.
- Стендовая проверка PLC-handshake после нового `Start=false` перед retry остаётся обязательной; автоматического стендового теста в этом change-set нет.

## Связанные планы и документы

- План: `План пользователя от 2026-03-17: безопасный Start=false перед retry без hard reset и без регресса`
- Stable docs:
  - `Final_Test_Hybrid/Docs/execution/RetrySkipGuide.md`
  - `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md`
  - `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
  - `Final_Test_Hybrid/Docs/runtime/ErrorCoordinatorGuide.md`
  - `AGENTS.md`
- Related impact: `Отсутствовали; это первая запись active execution-контура.`

## Сводит impact

- `Не применимо`
