# Impact: Стабилизация мерцания floating error badge

## Контекст

- Контур: `ui`
- Затронутые подсистемы: `FloatingErrorBadgeHost`, `floating-panel.js`, `error UI docs`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: жёлтый треугольник активных ошибок появлялся корректно, но не всегда начинал мерцать при первом показе.
- Причина сейчас: CSS-анимация бейджа полагалась только на обычный рендер DOM-элемента без явного restart, из-за чего старт blink оставался недетерминированным.

## Что изменили

- Добавили в `FloatingErrorBadgeHost` отслеживание перехода `нет сбрасываемых ошибок -> есть сбрасываемые ошибки`.
- При первом появлении бейджа теперь выполняется одноразовый JS-restart текущей CSS-анимации.
- В `floating-panel.js` добавили helper для restart анимации без вмешательства в drag/panel логику.
- Синхронизировали stable docs по поведению floating badge на главном экране и в error system guide.

## Где изменили

- `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs` — одноразовый restart blink после первого появления бейджа.
- `Final_Test_Hybrid/wwwroot/js/floating-panel.js` — helper `restartAnimation`.
- `Final_Test_Hybrid/Docs/runtime/ErrorSystemGuide.md` — контракт поведения floating badge.
- `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md` — фиксация overlay-поведения на главном экране.

## Когда делали

- Исследование: `2026-03-17 14:02 +04:00`
- Решение: `2026-03-17 14:18 +04:00`
- Правки: `2026-03-17 14:23 +04:00` - `2026-03-17 14:31 +04:00`
- Проверки: `2026-03-17 14:31 +04:00` - `2026-03-17 14:33 +04:00`
- Финализация: `2026-03-17 14:33 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 14:02 +04:00` | Проверили `FloatingErrorBadgeHost`, CSS и `floating-panel.js`. | Исключить ложную гипотезу про разные ветки поведения для разных типов ошибок. |
| `2026-03-17 14:18 +04:00` | Зафиксировали, что менять нужно только старт мерцания, без изменения набора ошибок и PLC-сигналов. | Сохранить узкий scope change-set. |
| `2026-03-17 14:23 +04:00` | Добавили в компонент флаг первого появления бейджа и вызов restart анимации после рендера. | Сделать старт blink детерминированным. |
| `2026-03-17 14:29 +04:00` | Добавили JS helper restart анимации с уважением `prefers-reduced-motion`. | Не ломать accessibility и не трогать drag/panel механику. |
| `2026-03-17 14:31 +04:00` | Синхронизировали `ErrorSystemGuide.md` и `MainScreenGuide.md`. | Не оставить рассинхрон между кодом и source-of-truth описанием UI. |

## Проверки

- Команды / проверки:
  - Rider `get_file_problems` для `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs`
  - Rider `get_file_problems` для `Final_Test_Hybrid/wwwroot/js/floating-panel.js`
  - Rider `build_project` по `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs`
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet format analyzers --verify-no-changes`
  - `dotnet format style --verify-no-changes`
  - `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs" --no-build --format=Text "--output=inspectcode-floating-badge.txt" -e=WARNING`
- Результат:
  - Rider file problems для изменённых `.cs` и `.js` файлов не вернули ошибок и предупреждений;
  - Rider partial build прошёл успешно;
  - `dotnet build Final_Test_Hybrid.slnx` прошёл успешно; сохранился существующий warning `MSB3277` по конфликту версий `WindowsBase`, не связанный с текущим change-set;
  - оба `dotnet format --verify-no-changes` прошли успешно;
  - `jb inspectcode` завершился успешно, warning-level замечаний по изменённому `.cs` файлу не зафиксировал.

## Риски

- В этой сессии не выполнялся живой прогон WinForms + Blazor Hybrid UI на стенде; поведение подтверждено кодом и статическими проверками, но не ручным HMI-воспроизведением.
- В рабочем дереве уже были посторонние несвязанные изменения до этого change-set; проверки выполнялись поверх текущего worktree.

## Открытые хвосты

- PLC-сигналы, `ErrorService`, count/reset/panel/drag логика floating badge не менялись.
- Визуальный стиль и частота мерцания не менялись; стабилизирован только старт уже существующей анимации.
- `no new incident`

## Связанные планы и документы

- План: `Исправить только мерцание жёлтого треугольника ошибок без изменения остальной логики бейджа`
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/runtime/ErrorSystemGuide.md`
  - `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md`
- Related impact:
  - По контуру `ui` активных impact по floating badge до этого change-set не было.

## Сводит impact

- `Не применимо`
