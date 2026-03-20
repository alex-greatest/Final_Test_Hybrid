# 2026-03-19 wide-editor-grid-sticky-actions-and-resize

## Контур

- UI / Engineer / Stand database / wide editor-grid

## Что изменено

- В `wwwroot/css/app.css` добавлен opt-in модификатор `grid-wide-editor-host` + `grid-wide-editor` поверх `grid-unified` для широких editor-grid.
- Новый профиль переводит body display-ячейки на `ellipsis`-обрезание и убирает выплывание текста поверх соседних колонок, сохраняя anti-clipping поведение заголовков.
- Для `RecipesGrid`, `ErrorSettingsTemplatesGrid` и трёх `ResultSettings*Grid` включён `AllowColumnResize`, чтобы рабочие колонки можно было подстраивать по ширине.
- В тех же grid служебные колонки (`select`, `actions`) оставлены фиксированными через `Resizable="false"`, а action-column закреплена справа через `Frozen="true"` + `FrozenPosition="Right"`.
- Для рабочих колонок заданы `MinWidth`, чтобы resize не сжимал их до нечитабельного состояния.
- Checkbox-ячейки (`select`, `PLC`) выведены из body-ellipsis правила, чтобы содержимое не подрезалось по нижней границе после включения overflow-контроля в wide-editor профиле.
- В `ErrorSettingsTemplatesGrid.razor.css` блок выбора шага переведён на `min-width:0` + `ellipsis`, чтобы в edit-режиме имя шага не наезжало на кнопку выбора.
- В `Docs/ui/GridProfilesGuide.md` добавлен новый профиль `Wide Editor` и карта его использования.

## Затронутые файлы

- `Final_Test_Hybrid/wwwroot/css/app.css`
- `Final_Test_Hybrid/Components/Engineer/StandDatabase/Recipe/RecipesGrid.razor`
- `Final_Test_Hybrid/Components/Engineer/StandDatabase/ErrorSettingsTemplatesGrid.razor`
- `Final_Test_Hybrid/Components/Engineer/StandDatabase/ErrorSettingsTemplatesGrid.razor.css`
- `Final_Test_Hybrid/Components/Engineer/StandDatabase/ResultSettings/ResultSettingsSimpleGrid.razor`
- `Final_Test_Hybrid/Components/Engineer/StandDatabase/ResultSettings/ResultSettingsRangeGrid.razor`
- `Final_Test_Hybrid/Components/Engineer/StandDatabase/ResultSettings/ResultSettingsBoardGrid.razor`
- `Final_Test_Hybrid/Docs/ui/GridProfilesGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — первый прогон был успешным; повторный прогон после follow-up фикса checkbox-ячеек упёрся в внешний lock файла `bin\Debug\net10.0-windows\Final_Test_Hybrid.exe`, который удерживал процесс `Final_Test_Hybrid (PID 15816)`.
- `dotnet build Final_Test_Hybrid.slnx -p:UseAppHost=false` — успешно; компиляция подтверждена без копирования заблокированного `exe`. Остался тот же внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`, не связанный с этой правкой.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- В change-set нет изменённых `*.cs`, поэтому точечный `jb inspectcode` по C#-файлам не требовался.

## Residual Risks

- Поведение frozen-column, resize и checkbox-fix подтверждено по коду и проверкам сборки/format, но без интерактивного desktop-прогона остаётся риск мелкой визуальной подстройки `z-index`/фона/вертикального выравнивания на конкретной теме.

## Инциденты

- no new incident
