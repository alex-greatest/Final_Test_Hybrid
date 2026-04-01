# 2026-03-30 floating-error-panel-drag-and-offset

## Контур

- UI / Main screen / Floating error overlays

## Что изменено

- В `FloatingErrorPanel.razor` старт drag перенесён с заголовка на корневой контейнер панели, поэтому перетаскивание доступно по всей поверхности окна.
- В `FloatingErrorPanel.razor.cs` дефолтная позиция панели изменена с нижнего правого угла на более высокую правую верхнюю зону экрана (`top: 72px`), чтобы стартовое появление не перекрывало нижний сектор `TestSequenseGrid`.
- В `FloatingErrorPanel.razor.css` курсор `move` перенесён на всю панель.
- Общий helper `wwwroot/js/floating-panel.js` переведён с mouse-only drag lifecycle на pointer-aware path с `pointerId`, `pointerup`/`pointercancel` cleanup и попыткой `setPointerCapture(...)`.
- В `FloatingErrorBadgeHost.razor` и `FloatingErrorPanel.razor` старт drag переведён на `@onpointerdown`, а код-behind использует `PointerEventArgs`.
- В `FloatingErrorBadgeHost.razor.css` и `FloatingErrorPanel.razor.css` для touch drag-зон добавлен `touch-action: none`, чтобы touch не залипал в gesture path и не оставлял overlay в подвешенном drag-state.
- В `Docs/runtime/ErrorSystemGuide.md` зафиксированы pointer/touch правила для `FloatingErrorPanel` и `FloatingErrorBadgeHost`.
- Новый failure mode и его root cause/resolution задокументированы в `Docs/changes/2026-04-01-floating-error-overlay-touch-drag-sticky-state.md`.

## Затронутые файлы

- `Final_Test_Hybrid/wwwroot/js/floating-panel.js`
- `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor`
- `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs`
- `Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.css`
- `Final_Test_Hybrid/Components/Errors/FloatingErrorPanel.razor`
- `Final_Test_Hybrid/Components/Errors/FloatingErrorPanel.razor.cs`
- `Final_Test_Hybrid/Components/Errors/FloatingErrorPanel.razor.css`
- `Final_Test_Hybrid/Docs/runtime/ErrorSystemGuide.md`
- `Final_Test_Hybrid/Docs/changes/2026-04-01-floating-error-overlay-touch-drag-sticky-state.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остался внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`, не связан с этой правкой.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Errors/FloatingErrorBadgeHost.razor.cs;Final_Test_Hybrid/Components/Errors/FloatingErrorPanel.razor.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-floating-error-overlay.txt" -e=WARNING` — отчёт сформирован, warning по изменённым overlay-файлам не выявлены.

## Residual Risks

- В этой сессии нет автоматизированной проверки touch gesture path внутри desktop WebView, поэтому отсутствие залипания после `pointercancel` подтверждается кодом и ручной матрицей прогонов, а не UI-автотестом.

## Инциденты

- Failure mode задокументирован в `Docs/changes/2026-04-01-floating-error-overlay-touch-drag-sticky-state.md`; отдельный incident document не создавался.
