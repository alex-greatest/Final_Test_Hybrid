# Impact: Безопасная строковая запись в панели `Тест связи`

## Контекст

- Контур: `cross-cutting`
- Затронутые подсистемы: `diagnostics`, `ui`, `stable docs`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: в `ConnectionTestPanel` была только numeric-запись. Для строковых полей протокола оператор мог работать только через ручную упаковку по регистрам, что повышало риск ошибочного ввода и записи мусора в котёл.
- Причина сейчас: требовалось добавить строковую запись именно в ручную панель `Тест связи`, но без расширения write-поведения до произвольных диапазонов и без вмешательства в runtime-потоки.

## Что изменили

- Добавили в write-панель `Тест связи` новый тип `String` только для ручного режима `Вручную...`.
- Ограничили строковую запись whitelist-диапазонами протокола: `1133..1136`, `1139..1145`, `1175..1181`, `1182..1188`.
- Добавили pre-write валидацию диапазона, ASCII-формата и длины строки.
- Добавили confirm-диалог перед записью строки.
- После успешной записи обязали панель выполнять read-back verify через `ReadStringAsync` и считать операцию успешной только при точном совпадении значения.
- Сохранили существующую numeric-логику write-панели без поведенческих изменений.
- Обновили stable docs `DiagnosticGuide.md` под новый UI-контракт строковой записи.

## Где изменили

- `Final_Test_Hybrid/Components/Overview/ConnectionTestPanel.razor` — добавили безопасный строковый режим записи, confirm и read-back verify.
- `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md` — задокументировали новый строковый режим, автоматическое применение `BaseAddressOffset`, whitelist-диапазоны и verify-контракт.
- `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-17-connection-test-safe-string-write.md` — сохранили traceability change-set.

## Когда делали

- Исследование: `2026-03-17 15:24 +04:00`
- Решение: `2026-03-17 15:53 +04:00`
- Правки: `2026-03-17 15:58 +04:00` - `2026-03-17 16:12 +04:00`
- Проверки: `2026-03-17 16:12 +04:00` - `2026-03-17 16:17 +04:00`
- Финализация: `2026-03-17 16:17 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 15:24 +04:00` | Сверили `DiagnosticGuide`, `UiPrinciplesGuide`, `ImpactHistoryGuide`, протокол строковых регистров и текущую реализацию `ConnectionTestPanel`. | Подтвердить реальный безопасный контур перед правками. |
| `2026-03-17 15:53 +04:00` | Зафиксировали дизайн: только manual `String`, whitelist диапазонов, confirm, read-back verify, без изменений backend и runtime. | Исключить опасный сценарий произвольной строковой записи. |
| `2026-03-17 15:58 +04:00` | Внесли точечные правки в `ConnectionTestPanel.razor`. | Добавить новую функцию без регрессии numeric-ветки. |
| `2026-03-17 16:08 +04:00` | Обновили `DiagnosticGuide.md`. | Синхронизировать source-of-truth docs в том же change-set. |
| `2026-03-17 16:12 +04:00` | Прогнали file problems, build, format и governance/localization checks. | Подтвердить корректность change-set и зафиксировать residual risks. |

## Проверки

- Команды / проверки:
  - Rider `get_file_problems` для `Components/Overview/ConnectionTestPanel.razor`
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
  - `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\agents-operational-guardrails\scripts\replay_governance_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\localization-sync-guard\scripts\replay_localization_sync.ps1 -RepoRoot . -RequireResourceSync`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\localization-sync-guard\scripts\replay_localization_sync.ps1 -RepoRoot .`
- Результат:
  - `get_file_problems` для изменённого `.razor` не нашёл ошибок и предупреждений.
  - `dotnet build` прошёл успешно.
  - `dotnet format analyzers --verify-no-changes` прошёл успешно.
  - `dotnet format style --verify-no-changes` прошёл успешно.
  - governance replay прошёл успешно.
  - strict localization replay завершился violation: `UI files changed but no localization resource files changed`.
  - non-strict localization replay подтвердил факт: `UI files changed: 2`, `Resource files changed: 0`.
  - Дополнительно: `dotnet build` продолжает показывать существующий warning `MSB3277` по конфликту `WindowsBase`; change-set его не вводил.
  - `jb inspectcode` по changed `*.cs` не запускался: в этом change-set нет новых изменённых `*.cs`, только `.razor` и docs.

## Риски

- Строгая localization policy для UI-изменений не выполнена: в репозитории есть `Form1.resx`, но действующего resource-контра for Blazor/Radzen UI не обнаружено. Это process-gap, а не runtime-дефект текущей функции.
- Для диапазонов `RW1` доступность записи по-прежнему определяется ECU-сессией. UI только предупреждает об ограничении и не пытается обходить ECU-side отказ.
- Новая строковая ветка добавлена в большой компонент `ConnectionTestPanel`; следующий change-set по этой панели должен сохранять текущую декомпозицию helper-методами, иначе читабельность быстро просядет.

## Открытые хвосты

- Отдельного resource-sync решения для Blazor UI в репозитории по-прежнему нет; strict localization replay остаётся красным до явного process decision по локализации UI.
- При необходимости дальнейшего расширения `Тест связи` под строковое чтение лучше делать отдельным change-set, не смешивая его с текущим write-only контрактом.
- `no new incident`

## Связанные планы и документы

- План: `Пользовательский план от 2026-03-17: добавить безопасную строковую запись в панель Тест связи.`
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`
  - `Final_Test_Hybrid/Docs/ui/UiPrinciplesGuide.md`
  - `Final_Test_Hybrid/Docs/impact/ImpactHistoryGuide.md`
- Related impact:
  - `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-17-sequence-clear-mode-doc-sync.md`
  - `Active impact по diagnostics-контурy для ConnectionTestPanel до этой задачи отсутствовал.`

## Сводит impact

- `Не применимо`
