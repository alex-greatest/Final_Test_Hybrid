# Impact: Вынос текущих размеров floating error badge в appsettings

## Контекст

- Контур: `ui`
- Затронутые подсистемы: `FloatingErrorBadgeHost`, `AppSettingsService`, `appsettings.json`, `error UI docs`
- Тип изменения: `новый impact`
- Статус цепочки: `завершено`

## Почему делали

- Проблема / цель: текущая геометрия плавающего жёлтого треугольника ошибок была жёстко зашита в CSS и не управлялась через конфигурацию.
- Причина сейчас: оператору нужно менять размеры бейджа через `appsettings.json` до запуска приложения, сохранив текущий исторический вид по умолчанию и не вводя отдельный UI-настройщик.

## Что изменили

- Добавили новый конфигурационный объект `Settings:FloatingErrorBadge` с текущими значениями размеров бейджа, иконки и счётчика.
- Протянули эти значения через `AppSettingsService` в `FloatingErrorBadgeHost` как CSS variables без изменения blink/drag/panel логики.
- Зафиксировали safe fallback для некорректных числовых значений и сохранили запись `appsettings.json` в UTF-8 with BOM.
- Синхронизировали stable docs по главному экрану и error system guide.

## Где изменили

- `Final_Test_Hybrid/Settings/App/FloatingErrorBadgeSettings.cs` — новый объект конфигурации размеров.
- `Final_Test_Hybrid/Settings/App/AppSettings.cs` — подключение `FloatingErrorBadgeSettings` в общий `Settings`.
- `Final_Test_Hybrid/Services/Common/Settings/AppSettingsService.cs` — sanitization/fallback размеров и явная запись `appsettings.json` в UTF-8 with BOM.
- `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor` — передача CSS variables в DOM.
- `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs` — сборка style-переменных из конфигурации.
- `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css` — замена жёстких размеров на переменные с дефолтами текущего визуала.
- `Final_Test_Hybrid/appsettings.json` — добавлен раздел `Settings:FloatingErrorBadge` со значениями `84/84/78/24/24/13`.
- `Final_Test_Hybrid/Docs/runtime/ErrorSystemGuide.md` — зафиксирован источник размеров и правило применения только после перезапуска.
- `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md` — зафиксировано, что overlay badge читает размеры из `appsettings.json`.

## Когда делали

- Исследование: `2026-03-17 16:24 +04:00`
- Решение: `2026-03-17 16:40 +04:00`
- Правки: `2026-03-17 16:49 +04:00` - `2026-03-17 17:03 +04:00`
- Проверки: `2026-03-17 17:04 +04:00` - `2026-03-17 17:20 +04:00`
- Финализация: `2026-03-17 17:21 +04:00`

## Хронология

| Дата и время | Что сделали | Зачем |
|---|---|---|
| `2026-03-17 16:24 +04:00` | Проверили stable UI/runtime docs, active impact по floating badge и текущую реализацию `FloatingErrorBadgeHost`. | Исключить ложную гипотезу про уже существующую runtime-настройку размеров. |
| `2026-03-17 16:40 +04:00` | Зафиксировали scope: только `appsettings.json`, без UI-настройки и без hot-reload конфигурации. | Не расширять change-set лишним поведением и не трогать runtime-gating. |
| `2026-03-17 16:49 +04:00` | Добавили объект `FloatingErrorBadgeSettings`, чтение через `AppSettingsService` и CSS variables для бейджа. | Вынести текущие размеры из CSS в конфиг без изменения исторического вида. |
| `2026-03-17 16:58 +04:00` | Добавили явную запись `appsettings.json` в UTF-8 with BOM. | Не ломать кодировку конфигурации при сохранении существующих инженерных флагов. |
| `2026-03-17 17:03 +04:00` | Синхронизировали `ErrorSystemGuide.md` и `MainScreenGuide.md`. | Не оставить source-of-truth документы в старом состоянии. |
| `2026-03-17 17:20 +04:00` | Прогнали Rider/file problems, partial build, `dotnet format`, `inspectcode`, governance replay и дополнительную сборку в альтернативный output-path. | Подтвердить, что change-set компилируется и проходит обязательные quality-gates с учётом lock от запущенного приложения. |

## Проверки

- Команды / проверки:
  - Rider `get_file_problems` для `AppSettingsService.cs`, `AppSettings.cs`, `FloatingErrorBadgeSettings.cs`, `FloatingErrorBadgeHost.razor.cs`
  - Rider `build_project` по изменённым `.cs`
  - `dotnet build Final_Test_Hybrid.slnx`
  - `dotnet build Final_Test_Hybrid.slnx -p:OutDir=D:\projects\Final_Test_Hybrid\.tmp_build_out\`
  - `dotnet format analyzers --verify-no-changes`
  - `dotnet format style --verify-no-changes`
  - `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Common/Settings/AppSettingsService.cs;Final_Test_Hybrid/Settings/App/AppSettings.cs;Final_Test_Hybrid/Settings/App/FloatingErrorBadgeSettings.cs;Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs" --no-build --format=Text "--output=inspectcode-floating-badge-size.txt" -e=WARNING`
  - `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\agents-operational-guardrails\scripts\replay_governance_audit.ps1 -RepoRoot . -RequireDocsOnCodeChange`
- Результат:
  - Rider `get_file_problems` для изменённых `.cs` файлов не вернул ошибок и предупреждений;
  - Rider partial build прошёл успешно;
  - точная команда `dotnet build Final_Test_Hybrid.slnx` дважды упёрлась в lock живого процесса `Final_Test_Hybrid (16888)`, который удерживает `bin\Debug\net10.0-windows\Final_Test_Hybrid.exe`; кроме этого сохранился существующий warning `MSB3277` по конфликту `WindowsBase`;
  - дополнительная сборка `dotnet build Final_Test_Hybrid.slnx -p:OutDir=D:\projects\Final_Test_Hybrid\.tmp_build_out\` прошла успешно и подтвердила компиляцию change-set; warning `MSB3277` остался прежним и не связан с текущими правками;
  - оба `dotnet format --verify-no-changes` прошли успешно;
  - `jb inspectcode` завершился успешно, но оставил warning-level ложные срабатывания на `AppSettings.cs` (`Auto-property accessor ... is never used`) из-за reflective binding `IConfiguration`; новых warning по `FloatingErrorBadgeHost` и `AppSettingsService` не появилось;
  - governance replay прошёл успешно: обязательные AGENTS headers и docs-on-code-change check подтверждены.

## Риски

- Живой WinForms + Blazor Hybrid сценарий на стенде в этой сессии не воспроизводился; изменение подтверждено кодом и статическими проверками.
- Обязательная точная команда `dotnet build Final_Test_Hybrid.slnx` остаётся environment-blocked, пока запущенный `Final_Test_Hybrid.exe` держит debug output.
- При ручной правке `Settings:FloatingErrorBadge` можно задать экстремальные числа; сервис ограничивает их safe fallback/clamp, но визуальная эргономика больших значений всё равно требует отдельной ручной проверки на HMI.

## Открытые хвосты

- Hot-reload `appsettings.json` для размеров floating badge не добавлялся.
- UI-настройка размера в инженерных диалогах не добавлялась; источник истины остаётся `appsettings.json`.
- Blink/drag/panel/resettable-errors логика floating badge не менялась.
- `no new incident`

## Связанные планы и документы

- План: `Вынести текущие размеры floating error badge в appsettings и сохранить текущий визуал по умолчанию`
- Stable docs:
  - `AGENTS.md`
  - `Final_Test_Hybrid/Docs/runtime/ErrorSystemGuide.md`
  - `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md`
- Related impact:
  - `Final_Test_Hybrid/Docs/impact/ui/2026-03-17-floating-error-badge-blink.md` — соседний завершённый change-set по детерминированному старту blink.

## Сводит impact

- `Не применимо`
