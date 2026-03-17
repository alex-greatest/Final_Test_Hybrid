# Impact: Внедрение workflow impact-истории

## Контекст

- Контур: `cross-cutting`
- Затронутые подсистемы: `governance`, `documentation workflow`, `impact history`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: в репозитории не было канонического процесса для чтения existing impact перед новым планом, создания нового impact после выполненного плана и безопасного сжатия истории без потери трассировки изменений.
- Причина сейчас: нужно было закрепить impact как отдельную историю изменений, не смешивая её со stable documentation, и сразу ввести это правило в рабочий цикл проекта.

## Что изменили

- Добавили в `AGENTS.md` обязательный workflow: stable docs -> relevant impact -> план -> реализация -> проверки -> новый impact.
- Зафиксировали в `AGENTS.md`, что `impact` не заменяет stable documentation, обязателен только после реальных repo-tracked изменений и допускает только `lossless rollup + archive`.
- Добавили guide `Final_Test_Hybrid/Docs/impact/ImpactHistoryGuide.md` с правилами чтения, хранения, именования и rollup.
- Добавили шаблон `Final_Test_Hybrid/Docs/impact/impact-template.md` для новых impact-файлов.
- Создали каноническую структуру контуров `impact` с `archive/` подпапками.
- Доработали `AGENTS.md` обязательными governance-заголовками (`Mission`, `Mandatory Read Order`, `Change Workflow`, `Verification Checklist`, `Incident Documentation Rule`, `Done Criteria`), чтобы проектный replay-аудит проходил на обновлённом процессе.
- Добавили короткий стоп-чек `когда impact точно не нужен` и закрепили правило: для того же active workstream обновляется текущий impact, а не создаётся лишний follow-up файл.
- Добавили в `AGENTS.md` правило применять `$lada-runtime-guardrails` только для критичных модулей (`PreExecutionCoordinator*`, `TestSequenseService`, `ScanModeController`, `ColumnExecutor`) и только в части общих C# guardrails, без слепого переноса LADA-специфики.

## Где изменили

- `AGENTS.md` — закрепили workflow impact-истории, новые governance-заголовки и ссылку на guide.
- `AGENTS.md` — закрепили workflow impact-истории, новые governance-заголовки, ссылку на guide и правило ограниченного применения `$lada-runtime-guardrails` для критичных модулей.
- `Final_Test_Hybrid/Docs/impact/ImpactHistoryGuide.md` — описали правила чтения, создания и сжатия impact и добавили стоп-чек на лишние записи.
- `Final_Test_Hybrid/Docs/impact/impact-template.md` — добавили базовый шаблон impact-файла.
- `Final_Test_Hybrid/Docs/impact/runtime/.gitkeep` — подготовили структуру активного контура.
- `Final_Test_Hybrid/Docs/impact/runtime/archive/.gitkeep` — подготовили структуру архива для rollup.
- `Final_Test_Hybrid/Docs/impact/execution/.gitkeep` — подготовили структуру активного контура.
- `Final_Test_Hybrid/Docs/impact/execution/archive/.gitkeep` — подготовили структуру архива для rollup.
- `Final_Test_Hybrid/Docs/impact/diagnostics/.gitkeep` — подготовили структуру активного контура.
- `Final_Test_Hybrid/Docs/impact/diagnostics/archive/.gitkeep` — подготовили структуру архива для rollup.
- `Final_Test_Hybrid/Docs/impact/ui/.gitkeep` — подготовили структуру активного контура.
- `Final_Test_Hybrid/Docs/impact/ui/archive/.gitkeep` — подготовили структуру архива для rollup.
- `Final_Test_Hybrid/Docs/impact/cross-cutting/archive/.gitkeep` — подготовили архив для будущих cross-cutting rollup.

## Когда делали

- Исследование: `2026-03-16 23:19 +04:00`
- Решение: `2026-03-16 23:20 +04:00`
- Правки: `2026-03-16 23:20 +04:00` - `2026-03-16 23:49 +04:00`
- Проверки: `2026-03-16 23:21 +04:00` - `2026-03-16 23:50 +04:00`
- Финализация: `2026-03-16 23:50 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-16 23:19 +04:00` | Проверили текущее состояние репозитория, существующие docs и отсутствие impact-каталога. | Подтвердить факты перед изменением governance-процесса. |
| `2026-03-16 23:20 +04:00` | Сформировали каноническую структуру `Final_Test_Hybrid/Docs/impact/` и добавили `AGENTS.md`-правила для impact workflow. | Зафиксировать единый источник истины для чтения и записи history. |
| `2026-03-16 23:21 +04:00` | Добавили `ImpactHistoryGuide.md` и `impact-template.md`. | Перевести новое правило из краткой governance-записи в исполнимый проектный стандарт. |
| `2026-03-16 23:21 +04:00` | Запустили governance replay и получили нарушение по отсутствующим стандартным заголовкам в `AGENTS.md`. | Проверить, проходит ли обновлённый governance-контур формальную валидацию. |
| `2026-03-16 23:22 +04:00` | Дополнили `AGENTS.md` обязательными governance-заголовками без изменения проектных инвариантов. | Сделать contract совместимым с replay-аудитом и не оставлять процесс в частично валидном состоянии. |
| `2026-03-16 23:23 +04:00` | Повторно прогнали replay, сборку и `dotnet format --verify-no-changes`. | Зафиксировать, что новый workflow не ломает обязательный минимальный check-list. |
| `2026-03-16 23:31 +04:00` | Добавили короткий стоп-чек `когда impact точно не нужен` и закрепили обновление текущего active impact для того же workstream. | Снизить риск лишних записей и не превратить impact-процесс в шум. |
| `2026-03-16 23:33 +04:00` | Повторно прогнали replay, сборку и `dotnet format --verify-no-changes` после добавления стоп-чека. | Подтвердить, что уточнение не ломает governance и обязательный check-list. |
| `2026-03-16 23:49 +04:00` | Добавили правило ограниченного применения `$lada-runtime-guardrails` только для критичных runtime-модулей и только в части общих guardrails. | Закрепить полезные практики skill-а без переноса LADA-специфики в `Final_Test_Hybrid`. |
| `2026-03-16 23:50 +04:00` | Повторно прогнали replay, сборку и `dotnet format --verify-no-changes` после правила про `$lada-runtime-guardrails`. | Подтвердить, что новое ограничение согласовано с governance и quality-gates. |

## Проверки

- Команды / проверки:
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\agents-operational-guardrails\scripts\replay_governance_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers --verify-no-changes`
  - `dotnet format style --verify-no-changes`
- Результат:
  - governance replay после доработки `AGENTS.md` прошёл успешно;
  - `dotnet build Final_Test_Hybrid.slnx` прошёл успешно;
  - `dotnet format analyzers --verify-no-changes` прошёл успешно;
  - `dotnet format style --verify-no-changes` прошёл успешно;
  - после добавления стоп-чека повторный governance replay прошёл успешно;
  - после добавления стоп-чека повторные `dotnet build Final_Test_Hybrid.slnx`, `dotnet format analyzers --verify-no-changes` и `dotnet format style --verify-no-changes` тоже прошли успешно;
  - после добавления правила про `$lada-runtime-guardrails` повторные governance replay, `dotnet build Final_Test_Hybrid.slnx`, `dotnet format analyzers --verify-no-changes` и `dotnet format style --verify-no-changes` тоже прошли успешно;
  - в сборке остались существующие предупреждения `MSB3277` по конфликту `WindowsBase 4.0.0.0` vs `5.0.0.0`; текущий change-set их не вносил.

## Риски

- Новый процесс зависит от дисциплины чтения релевантных impact перед планом; нарушение workflow даст не техническую, а процессную деградацию.
- Если в будущем rollup будут делать без явного списка исходных файлов, история станет формально неполной; это требует жёсткого следования guide и шаблону.

## Открытые хвосты

- Safe кандидаты на rollup не найдены: это первый impact в новой структуре.
- Отдельный новый impact для этого follow-up не создавался: изменение осталось в рамках того же active workstream и было добавлено в текущую запись.
- Отдельный incident document для этой задачи не требовался, так как новый production failure mode не выявлен.

## Связанные планы и документы

- План: `План в пользовательской задаче от 2026-03-16: "внедрить impact-историю и правила её чтения, создания и сжатия"`
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/impact/ImpactHistoryGuide.md`
  - `Final_Test_Hybrid/Docs/impact/impact-template.md`
- Related impact: `Отсутствовали; это первая запись в новой impact-структуре.`

## Сводит impact

- `Не применимо`
