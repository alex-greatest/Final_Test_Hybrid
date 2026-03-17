# Impact: Переименование текста subscription-loading overlay

## Контекст

- Контур: `cross-cutting`
- Затронутые подсистемы: `UI overlay`, `runtime docs`, `repo docs`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: пользовательский текст спиннера `SubscriptionLoadingOverlay` должен показывать `Выполняется настройка...` вместо прежнего сообщения о подписке во всех местах, где этот overlay и его текст зафиксированы.
- Причина сейчас: старый текст уже не соответствует согласованному копирайту и должен быть синхронно обновлён в коде и документации, чтобы не оставить расхождение между UI и source-of-truth описаниями.

## Что изменили

- Заменили текст в общем компоненте `SubscriptionLoadingOverlay` на `Выполняется настройка...`.
- Обновили `TagWaiterGuide.md`, где этот overlay закреплён как UI-индикация rebuild runtime-подписок.
- Обновили `CLAUDE.md`, чтобы repo-level документация не ссылалась на старый текст.
- Перепроверили repo-wide, что прежняя строка overlay больше не осталась в коде и документации.

## Где изменили

- `Final_Test_Hybrid/Components/Loading/SubscriptionLoadingOverlay.razor` — новый текст общего overlay.
- `Final_Test_Hybrid/Docs/runtime/TagWaiterGuide.md` — синхронизация stable docs для rebuild-индикации.
- `Final_Test_Hybrid/CLAUDE.md` — синхронизация repo-документации с новым копирайтом.

## Когда делали

- Исследование: `2026-03-17 13:29 +04:00`
- Решение: `2026-03-17 13:33 +04:00`
- Правки: `2026-03-17 13:36 +04:00` - `2026-03-17 13:38 +04:00`
- Проверки: `2026-03-17 13:38 +04:00` - `2026-03-17 13:42 +04:00`
- Финализация: `2026-03-17 13:42 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 13:29 +04:00` | Сверили фактический источник текста и usage-path overlay. | Подтвердить, что runtime-источник один и менять нужно точечно. |
| `2026-03-17 13:33 +04:00` | Зафиксировали вариант копирайта `Выполняется настройка...` и объём синхронных docs-правок. | Исключить частичную замену только в UI без doc-sync. |
| `2026-03-17 13:36 +04:00` | Обновили overlay-компонент, `TagWaiterGuide.md` и `CLAUDE.md`. | Синхронно заменить текст в коде и связанных source-of-truth описаниях. |
| `2026-03-17 13:39 +04:00` | Прогнали repo-wide поиск, `dotnet build`, оба `dotnet format --verify-no-changes`, Rider file problems, governance replay и localization replay. | Подтвердить отсутствие старого текста и прохождение обязательных quality gates. |

## Проверки

- Команды / проверки:
  - repo-wide поиск по прежней строке overlay
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
  - `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
  - Rider `get_file_problems` для `Final_Test_Hybrid/Components/Loading/SubscriptionLoadingOverlay.razor`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\agents-operational-guardrails\scripts\replay_governance_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\localization-sync-guard\scripts\replay_localization_sync.ps1 -RepoRoot .`
- Результат:
  - repo-wide поиск по прежней строке overlay после правок не вернул совпадений;
  - `dotnet build Final_Test_Hybrid.slnx` прошёл успешно; остался существующий warning `MSB3277` по конфликту `WindowsBase` версий, не связанный с текущим rename;
  - оба `dotnet format --verify-no-changes` прошли успешно;
  - Rider `get_file_problems` для изменённого `.razor`-файла не вернул ошибок и предупреждений;
  - governance replay прошёл успешно;
  - localization sync replay завершился без ошибок, зафиксировал `UI files changed: 3`, `Resource files changed: 0`.

## Риски

- Живая визуальная проверка overlay в запущенном WinForms + Blazor Hybrid приложении в этой сессии не выполнялась; корректность фактического показа подтверждена кодом и статическими проверками, но не ручным UI-прогоном.
- Worktree уже содержал посторонние несвязанные изменения до этого change-set; обязательные проверки выполнены по текущему дереву и не изолируют только данный rename.

## Открытые хвосты

- Поведение показа/скрытия overlay, `IsInitializing` и reconnect-flow не менялись.
- Новая схема локализации не вводилась: сохранён существующий inline-подход.
- `no new incident`

## Связанные планы и документы

- План: `Переименование текста спиннера подписок на «Выполняется настройка...»`
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/runtime/TagWaiterGuide.md`
  - `Final_Test_Hybrid/CLAUDE.md`
- Related impact:
  - `Final_Test_Hybrid/Docs/impact/cross-cutting/2026-03-17-sequence-clear-mode-doc-sync.md`
  - Отдельного active impact по этому overlay до текущего change-set не было.

## Сводит impact

- `Не применимо`
