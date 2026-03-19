# 2026-03-19 floating-error-badge-startup-blink

## Контур

- UI / Main screen / Error badge / startup error indication

## Что изменено

- В `FloatingErrorBadgeHost.razor.cs` стабилизирован старт мигания для стартовых ошибок:
  - `pending`-флаг больше не сбрасывается до успешного JS-вызова;
  - добавлен bounded retry перезапуска анимации;
  - retry ограничен и выполняется только пока бейдж остаётся видимым.
- В `wwwroot/index.html` `floating-panel.js` перенесён перед `blazor.webview.js`, чтобы helper был доступен к самому первому `OnAfterRender`.
- Обновлены `ErrorSystemGuide.md` и `MainScreenGuide.md`:
  - зафиксировано, что правило немедленного мигания распространяется и на стартовые ошибки, поднятые до полной готовности UI.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs`
- `Final_Test_Hybrid/wwwroot/index.html`
- `Final_Test_Hybrid/Docs/runtime/ErrorSystemGuide.md`
- `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остался внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`, не связан с этой правкой.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно; `dotnet format` вывел workspace warning без влияния на результат.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-floating-error-badge.txt" -e=WARNING` — отчёт сформирован, warning по изменённому файлу не выявлены.

## Residual Risks

- В этой сессии не выполнялся интерактивный запуск desktop UI, поэтому сценарий «ошибка уже активна при старте приложения» подтверждён кодом и сборочными проверками, но не ручным прогоном окна.

## Инциденты

- no new incident
