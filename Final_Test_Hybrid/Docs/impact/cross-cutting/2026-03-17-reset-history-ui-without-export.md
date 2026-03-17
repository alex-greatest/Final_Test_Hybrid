# Impact: reset-history в UI без Excel export и без возврата `Unknown`

## Контекст

- Контур: `cross-cutting`
- Затронутые подсистемы: `execution runtime`, `scan deactivation`, `step history ui`, `stable docs`
- Тип изменения: `поведенческий fix`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: после soft/hard reset sequence UI очищался без сохранения истории шагов, хотя для оператора полезно видеть snapshot прерванного прогона в `StepHistoryGrid`.
- Ограничение: нельзя было возвращать старый дефект, когда history уходила в Excel с именем `Unknown`.
- Причина сейчас: потребовалось разделить reset-history для UI и completion-history для Excel, не ломая действующий completion/repeat flow.

## Что изменили

- Расширили `SequenceClearMode` до трёх режимов: `CompletedTest`, `OperationalReset`, `ClearOnly`.
- Оставили `CompletedTest` completion-only веткой: `BoilerState.SaveLastTestInfo()`, snapshot в `StepHistoryService`, затем `StepHistoryExcelExporter.ExportIfEnabledAsync(...)`.
- Перевели `OperationalReset` в reset-only ветку для soft/hard reset: сохраняет snapshot только в `StepHistoryService`, не вызывает `SaveLastTestInfo()` и не обращается к exporter.
- Добавили защиту от пустой reset-history: snapshot на reset создаётся только если в sequence есть meaningful шаги помимо scan-строки.
- После hint-level проверки добавили fail-fast `default` в `switch(mode)`, чтобы новый enum-контракт не деградировал при будущем расширении `SequenceClearMode`.
- Перевели logout/full deactivation в `SequenceClearMode.ClearOnly`, чтобы этот путь не создавал ни history snapshot, ни Excel export.
- Обновили stable docs по execution/runtime/UI, чтобы source-of-truth явно различал completion-history, reset-history и clear-only cleanup.

## Где изменили

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/TestSequenseService.cs` — новый режим `ClearOnly`, reset-only snapshot helper, явное разделение completion/reset cleanup.
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs` — full deactivation переведена на `SequenceClearMode.ClearOnly`.
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/StepHistoryService.cs` — обновлён комментарий к контракту snapshot history.
- `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md` — зафиксирован трёхрежимный контракт `ClearAllExceptScan(mode)`.
- `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md` — soft/hard reset описаны как UI-only history без Excel export.
- `Final_Test_Hybrid/Docs/runtime/ScanModeControllerGuide.md` — full deactivation синхронизирована с `ClearOnly`.
- `Final_Test_Hybrid/Docs/ui/README.md` — уточнён контракт `StepHistoryGrid` после reset и запрет auto-export на reset.

## Когда делали

- Исследование: `2026-03-17 12:41 +04:00`
- Решение: `2026-03-17 12:49 +04:00`
- Правки: `2026-03-17 12:55 +04:00` - `2026-03-17 13:08 +04:00`
- Проверки: `2026-03-17 13:08 +04:00` - `2026-03-17 13:20 +04:00`
- Финализация: `2026-03-17 13:20 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 12:41 +04:00` | Повторно сверили runtime-код `TestSequenseService`, reset paths `PreExecutionCoordinator*`, `ScanModeController` и `StepHistoryExcelExporter`. | Подтвердить, где можно сохранить UI history, не затронув Excel export. |
| `2026-03-17 12:49 +04:00` | Зафиксировали решение: soft/hard reset должны сохранять history только в UI, а auto-export на reset запрещён даже при включённой галочке. | Удовлетворить операторский сценарий и не вернуть баг с `Unknown`. |
| `2026-03-17 12:55 +04:00` | Добавили `SequenceClearMode.ClearOnly` и вынесли reset-history в отдельную ветку без exporter. | Развязать completion-flow и reset-flow по побочным эффектам. |
| `2026-03-17 13:02 +04:00` | Перевели logout/full deactivation на `ClearOnly`. | Исключить ложное сохранение history при деактивации вне reset. |
| `2026-03-17 13:05 +04:00` | Обновили stable docs по execution/runtime/UI. | Синхронизировать source-of-truth с новым runtime-контрактом. |
| `2026-03-17 13:08 +04:00` | Запустили build/format/inspection и governance/doc-sync replay. | Проверить, что change-set не ломает код и проходит process gates. |
| `2026-03-17 13:17 +04:00` | После `inspectcode -e=HINT` добавили fail-fast `default` для `switch(mode)` и повторно прогнали проверки. | Закрыть новый enum-path без остаточного hint по неполному `switch`. |

## Проверки

- Команды / проверки:
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
  - `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
  - Rider `get_file_problems` по `TestSequenseService.cs`, `ScanModeController.cs`, `StepHistoryService.cs`
  - `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/TestSequenseService.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/StepHistoryService.cs" --no-build --format=Text "--output=<path>" -e=WARNING`
  - `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/TestSequenseService.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Scanning/ScanModeController.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/StepHistoryService.cs" --no-build --format=Text "--output=<path>" -e=HINT`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\agents-operational-guardrails\scripts\replay_governance_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\source-of-truth-doc-sync\scripts\replay_doc_sync_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
- Результат:
  - `dotnet build` прошёл успешно; остался только известный warning `MSB3277` по `WindowsBase`;
  - оба `dotnet format --verify-no-changes` прошли;
  - Rider `get_file_problems` по `TestSequenseService.cs`, `ScanModeController.cs`, `StepHistoryService.cs` вернул пустой список;
  - `jb inspectcode -e=WARNING` по трём затронутым runtime-файлам вернул пустой report;
  - `jb inspectcode -e=HINT` после добавления fail-fast `default` больше не показывает hint по неполному `switch`; в отчёте остались только pre-existing suggestions (`IsInScanningPhase`, `Dispose`, `Count`, `AddStep`, pattern merge, return simplification, `GetStepsCopy`);
  - governance replay прошёл успешно;
  - doc-sync replay прошёл успешно.

## Риски

- Семантика `OperationalReset` стала шире, чем просто «очистить UI»: теперь она сохраняет reset-history в `StepHistoryService`. Любой новый reset-path должен осознанно выбирать между `OperationalReset` и `ClearOnly`.
- Header result/history/timer вкладок по-прежнему опирается на `BoilerState.Last*`, поэтому после reset header и history snapshot могут описывать прерванный, а не завершённый прогон. Это подтверждённое product-решение, не баг.
- Ручной export из `StepHistoryGrid` после reset по-прежнему зависит от guard в `StepHistoryExcelExporter`; этот guard нельзя ослаблять без отдельного инцидента и source-of-truth change.

## Открытые хвосты

- Отдельной автоматизированной UI/Excel интеграционной проверки в repo нет; отсутствие файла `Unknown` подтверждается тем, что reset-ветка больше не вызывает exporter вообще.
- Ручной app-level прогон soft/hard reset с включённой галочкой автоэкспорта в этой задаче не выполнялся; это остаётся рекомендуемой операционной проверкой перед выкладкой.
- Если `jb inspectcode -e=HINT` снова упрётся в инфраструктурное ограничение JetBrains cache, это остаётся внешним ограничением процесса, а не дефектом change-set.
- `no new incident`

## Связанные планы и документы

- План: `Пользовательская задача от 2026-03-17: сохранять историю шагов в UI при soft/hard reset без Excel и без возврата бага Unknown.`
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md`
  - `Final_Test_Hybrid/Docs/execution/CycleExitGuide.md`
  - `Final_Test_Hybrid/Docs/runtime/ScanModeControllerGuide.md`
  - `Final_Test_Hybrid/Docs/ui/README.md`
- Related impact:
  - `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-17-sequence-clear-mode-doc-sync.md`
  - `Execution/runtime-specific active impact по этому workstream до текущего change-set отсутствовал.`

## Сводит impact

- `Не применимо`
