# 2026-04-15 io-editor-ai-three-decimal-display

## Контур

- UI / Engineer / `IoEditorDialog`
- Вкладка `AI Calibration`

## Что изменено

- В `IoEditorDialog.razor` отображение `AI Calibration` для `Min`, `Max`, `Multiplier`, `Offset` переведено с `F2` на `F3`.
- Значения после загрузки из OPC теперь показываются с тремя знаками после запятой, как уже было сделано в `Analog Outputs`, `RTD Calibration`, `PID Regulator` и overview IO-таблицах.
- Чтение OPC, snapshot, проверка несохранённых изменений и запись обратно в PLC не менялись.
- Удалены неиспользуемые `@using` в `IoEditorDialog.razor`, найденные точечным `inspectcode`.

## Что сознательно не менялось

- Runtime pipeline, reset/reconnect/error-flow, OPC write/read contract.
- DataGrid-профили и CSS.
- `RadzenNumeric` edit-контролы: изменение касается display-шаблонов таблицы.
- Stable UI docs: новый профиль или новый UI-контракт не вводился.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Engineer/Modals/IoEditorDialog.razor`

## Проверки

- Поиск по `Components/Engineer/Modals`: `ToString("F2")` / `ToString("N2")` больше не найден.
- `dotnet build Final_Test_Hybrid.slnx` — успешно; остались baseline warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Engineer/Modals/IoEditorDialog.razor" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-io-editor-precision-warning.txt" -e=WARNING` — отчёт пуст (`Solution Final_Test_Hybrid.slnx`).

## Residual Risks

- Интерактивная проверка в desktop UI не выполнялась; корректность подтверждена кодом, сборкой, форматтерами и точечным `inspectcode`.
- Rider MCP `get_file_problems` в этом окружении недоступен как tool; вместо него выполнен CLI `jb inspectcode` по изменённому Razor-файлу.

## Инциденты

- no new incident
