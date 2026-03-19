# 2026-03-19 test-sequence-grid-stable-autoscroll

## Контур

- UI / Main screen / TestSequenseGrid / autoscroll

## Что изменено

- В `TestSequenseGrid.razor` автоскролл переведён на двухфазный сценарий `render -> grid reload -> render -> scroll`, чтобы убрать гонку между `StateHasChanged`, `RadzenDataGrid.Reload()` и JS-вызовом прокрутки.
- В `TestSequenseGrid.razor` добавлен явный метод `EnsureScrolledToBottomAsync()`, который позволяет безопасно запросить прокрутку вниз без изменения данных грида.
- В `MyComponent.razor` верхние `RadzenTabs` теперь используют `Change`-обработчик; при возврате на вкладку «Главный экран» компонент повторно запрашивает прокрутку `TestSequenseGrid` вниз.
- В `wwwroot/js/grid-helpers.js` `scrollGridToBottom` усилен bounded retry через `requestAnimationFrame`, чтобы переживать поздний пересчёт высоты `.rz-data-grid-data` после reload.
- В `Docs/ui/MainScreenGuide.md` зафиксировано новое правило: последовательность теста на главном экране доводится вниз и при обновлении шагов, и при возврате пользователя на вкладку.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor`
- `Final_Test_Hybrid/MyComponent.razor`
- `Final_Test_Hybrid/wwwroot/js/grid-helpers.js`
- `Final_Test_Hybrid/Docs/ui/MainScreenGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остался внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`, не связан с этой правкой.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/MyComponent.razor;Final_Test_Hybrid/Components/Main/TestSequenseGrid.razor" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-test-sequence-autoscroll.txt" -e=WARNING` — отчёт сформирован, warning по целевым файлам не выявлены.
- `jetbrains_rider` MCP `get_file_problems` в этой сессии недоступен, поэтому точечная проверка предупреждений выполнена через `jb inspectcode`.

## Residual Risks

- В этой сессии не выполнялся ручной прогон desktop UI, поэтому сценарии добавления шагов и возврата на вкладку подтверждены кодом и обязательными проверками, но не интерактивным окном приложения.

## Инциденты

- no new incident
