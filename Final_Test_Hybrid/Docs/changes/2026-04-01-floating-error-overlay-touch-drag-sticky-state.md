# 2026-04-01 floating error overlay touch drag sticky state

## Failure mode

- На сенсорном экране жёлтый треугольник активных ошибок не перетаскивался пальцем.
- После touch-жеста drag-state мог остаться активным, и при следующем mouse input overlay начинал двигаться за курсором до случайного `mouseup`.
- Та же базовая проблема затрагивала любой overlay, использующий общий helper `wwwroot/js/floating-panel.js`, а не только badge.

## Root cause

- Общий helper `floating-panel.js` был реализован только через `mousedown` / `mousemove` / `mouseup` и не знал про pointer lifecycle.
- Drag-state не связывался с конкретным `pointerId`, поэтому helper не мог корректно игнорировать чужие события и завершать перенос на `pointercancel`.
- Для drag-зон не был зафиксирован `touch-action: none`, из-за чего touch-жест мог уходить в браузерный gesture path вместо полного drag lifecycle.

## Resolution

- Общий helper `floating-panel.js` переведён на pointer-events: `startDrag(elementId, pointerId, clientX, clientY)`, `pointermove`, `pointerup`, `pointercancel`.
- Drag-state теперь хранит `pointerId`, очищается перед новым стартом и завершается на любом `pointerup`/`pointercancel` того же указателя.
- При старте helper пытается захватить pointer через `setPointerCapture`, а при cleanup освобождает его, если capture ещё активен.
- `FloatingErrorBadgeHost` и `FloatingErrorPanel` переведены с `MouseEventArgs` / `@onmousedown` на `PointerEventArgs` / `@onpointerdown`.
- Для touch drag-зон добавлен `touch-action: none`, чтобы touch-перенос не перехватывался gesture/scroll path.
- Логика `recent drag` и порог `5px` сохранены, поэтому tap по бейджу продолжает открывать окно, а drag не провоцирует ложный toggle.

## Verification

- `dotnet build Final_Test_Hybrid.slnx`
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes`
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes`
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs;Final_Test_Hybrid/Components/Errors/FloatingErrorPanel.razor.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-floating-error-overlay.txt" -e=WARNING`
- Ручной repro matrix для touch/mouse overlay drag остаётся обязательной, потому что автоматизированных UI-тестов для WebView gesture path в репозитории нет.

## Notes

- Failure mode закрыт в рамках существующего UI-workstream по плавающим error overlay; соответствующий active impact обновляется, а не дублируется новой UI impact-записью.
