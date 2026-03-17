# Impact: Синхронизация stable docs под SequenceClearMode и reset-header контракт

## Контекст

- Контур: `cross-cutting`
- Затронутые подсистемы: `execution guides`, `runtime docs`, `ui docs`, `impact history`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: stable docs отстали от уже внесённого code change, где `ClearAllExceptScan` получил режимы `CompletedTest` и `OperationalReset`, а `BoilerState.LastSerialNumber` / `LastTestCompletedAt` перестали быть признаком только штатно завершённого теста.
- Причина сейчас: без синхронизации guide-документы продолжали описывать старый безрежимный cleanup-контракт и вводили бы в заблуждение при следующем изменении reset/completion flow.

## Что изменили

- Обновили `StateManagementGuide.md` под новый контракт `ClearAllExceptScan(SequenceClearMode mode)` и задокументировали разницу `CompletedTest` vs `OperationalReset`.
- Зафиксировали в stable docs фактическую семантику `BoilerState.Clear()`, `BoilerState.SaveLastTestInfo()` и `BoilerState.ClearLastTestInfo()` для header result/history/timer вкладок.
- Обновили `CycleExitGuide.md`, чтобы exit-path явно показывали, какой режим очистки sequence используется при completion, repeat и reset.
- Обновили `ScanModeControllerGuide.md`, чтобы full deactivation при logout вне PLC reset описывалась как `OperationalReset` без completed-history.
- Добавили в `Docs/ui/README.md` краткий контракт result/history/timer экранов и связали header с `BoilerState.Last*`, а содержимое таблиц — с отдельными data-сервисами.
- Уточнили UI-guide по scan marker, чтобы stable docs больше не держали старую безрежимную сигнатуру `ClearAllExceptScan`.

## Где изменили

- `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md` — зафиксировали `SequenceClearMode`, `BoilerState` семантику и runtime cleanup paths.
- `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md` — задокументировали режимы очистки sequence для completion/repeat/reset.
- `Final_Test_Hybrid/Docs/runtime/ScanModeControllerGuide.md` — уточнили full deactivation и отсутствие completed-history в `OperationalReset`.
- `Final_Test_Hybrid/Docs/ui/README.md` — добавили контракт header/result/history/timer вкладок и расширили список source-of-truth компонентов.
- `Final_Test_Hybrid/Docs/ui/SettingsBlockingGuide.md` — обновили упоминание lifecycle метода до `ClearAllExceptScan(SequenceClearMode mode)`.

## Когда делали

- Исследование: `2026-03-17 12:03 +04:00`
- Решение: `2026-03-17 12:12 +04:00`
- Правки: `2026-03-17 12:16 +04:00` - `2026-03-17 12:22 +04:00`
- Проверки: `2026-03-17 12:22 +04:00` - `2026-03-17 12:30 +04:00`
- Финализация: `2026-03-17 12:30 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 12:03 +04:00` | Сверили code paths `TestSequenseService`, `PreExecutionCoordinator`, `ScanModeController`, `BoilerState`, `StepHistoryGrid`, `TestResultsGrid`, `ActiveTimersGrid`. | Подтвердить фактический контракт перед правкой docs. |
| `2026-03-17 12:12 +04:00` | Зафиксировали рабочее решение: header-after-reset считается нормальным поведением и должен быть описан, а не исправлен кодом. | Исключить ложный follow-up на runtime-фикс и синхронизировать именно source-of-truth docs. |
| `2026-03-17 12:16 +04:00` | Обновили execution/runtime/ui guides под `SequenceClearMode` и новый `Last*` header-контракт. | Убрать расхождение между stable docs и реальным кодом. |
| `2026-03-17 12:22 +04:00` | Добавили отдельный impact по doc-sync для текущего workstream. | Сохранить traceability change-set и не смешивать его с предыдущими cross-cutting задачами. |
| `2026-03-17 12:24 +04:00` | Прогнали governance replay и doc-sync replay. | Подтвердить, что docs change-set проходит формальные process checks. |
| `2026-03-17 12:27 +04:00` | Сверили, что текущие зелёные build/format и inspection результаты остаются валидным evidence, так как код не менялся. | Закрыть task report без повторного искажения code verification. |

## Проверки

- Команды / проверки:
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\agents-operational-guardrails\scripts\replay_governance_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\source-of-truth-doc-sync\scripts\replay_doc_sync_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
  - `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
  - Rider `get_file_problems` по изменённым runtime-файлам
  - `jb inspectcode Final_Test_Hybrid.slnx ... -e=WARNING`
- Результат:
  - governance replay прошёл успешно;
  - doc-sync replay прошёл успешно;
  - code verification evidence из того же worktree остаётся зелёным: `dotnet build`, оба `dotnet format --verify-no-changes`, Rider file problems и `inspectcode -e=WARNING` без новых замечаний;
  - `inspectcode -e=HINT` ранее упирался в `UnauthorizedAccessException` JetBrains cache; это инфраструктурное ограничение не связано с текущим docs-only change-set.

## Риски

- После sync stable docs теперь явно закрепляют, что header result/history/timer вкладок не равен строгому признаку штатного completion. Если product-решение изменится, придётся синхронно менять и код, и docs.
- Следующий change-set в reset/completion flow должен обновлять именно эти guide-файлы, иначе расхождение вернётся в execution/runtime/ui одновременно.

## Открытые хвосты

- Код не менялся: change-set ограничен stable docs и traceability.
- Active impact по execution/runtime для самого исходного code change до этой задачи отсутствовал; текущая запись покрывает именно doc-sync и подтверждённую норму поведения.
- `no new incident`

## Связанные планы и документы

- План: `Пользовательская задача от 2026-03-17: синхронизировать stable docs и impact под SequenceClearMode и новый reset-header контракт.`
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md`
  - `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md`
  - `Final_Test_Hybrid/Docs/runtime/ScanModeControllerGuide.md`
  - `Final_Test_Hybrid/Docs/ui/README.md`
  - `Final_Test_Hybrid/Docs/ui/SettingsBlockingGuide.md`
- Related impact:
  - `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-16-impact-history-workflow.md`
  - `Execution/runtime-specific active impact по этому workstream до текущего doc-sync отсутствовал.`

## Сводит impact

- `Не применимо`
