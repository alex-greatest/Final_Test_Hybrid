# 2026-03-21 wide-view-grid-resize-and-ellipsis

## Контур

- UI / Results and errors / wide read-only grid

## Что изменено

- В `wwwroot/css/app.css` добавлен opt-in модификатор `grid-wide-view-host` + `grid-wide-view` поверх `grid-unified` для широких read-only grid.
- Новый профиль переводит body display-ячейки на `ellipsis`-обрезание и убирает выплывание текста поверх соседних колонок, не включая frozen action-column и edit-специфику `grid-wide-editor`.
- Для `TestResultsGrid`, `ActiveErrorsGrid`, `ErrorHistoryGrid` и `StepHistoryGrid` включён `AllowColumnResize`, чтобы пользователь мог подстраивать ширину колонок.
- Для колонок этих grid заданы `MinWidth`, чтобы resize не сжимал читаемые поля до нефункционального состояния.
- В `Docs/ui/GridProfilesGuide.md` добавлен новый профиль `Wide View` и карта его использования.

## Затронутые файлы

- `Final_Test_Hybrid/wwwroot/css/app.css`
- `Final_Test_Hybrid/Components/Results/TestResultsGrid.razor`
- `Final_Test_Hybrid/Components/Errors/ActiveErrorsGrid.razor`
- `Final_Test_Hybrid/Components/Errors/ErrorHistoryGrid.razor`
- `Final_Test_Hybrid/Components/Results/StepHistoryGrid.razor`
- `Final_Test_Hybrid/Docs/ui/GridProfilesGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx -p:UseAppHost=false -p:OutputPath=D:\projects\Final_Test_Hybrid\.codex-build\wide-view-grid-build\` — успешно. Остались внешние warnings `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0` в `Final_Test_Hybrid.csproj` и `Final_Test_Hybrid.Tests.csproj`, не связанные с этой правкой.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- В change-set нет изменённых `*.cs`, поэтому точечный `jb inspectcode` по C#-файлам не требовался.

## Residual Risks

- Без интерактивного desktop-прогона остаётся риск мелкой подстройки отдельных `MinWidth` и визуальной плотности колонок на реальных данных.

## Инциденты

- no new incident
