# 2026-03-22 ui-debug-log-noise-cleanup

## Контур

- UI / логирование вспомогательных экранов и схем

## Что изменено

- Из `Components/Schemes/HeatingScheme.razor` удалены информационные debug-логи инициализации схемы и callback-лог на каждое изменение клапана.
- Из `Components/Overview/AiCallCheck.razor.cs`, `Components/Overview/PidRegulatorCheck.razor.cs` и `Components/Overview/RtdCalCheck.razor.cs` удалён warning-лог `=== CLICK: IsReadOnly = ... ===`, не несущий операционной ценности.
- Из `Components/Overview/LampOutput.razor` удалены lifecycle/callback debug-логи `init`, `Subscribing to`, `callback: Tag/Value/Type`.
- Warning/Error-логи, сигнализирующие о реальной проблеме, сохранены без изменений.
- Runtime-логика, подписки, UI-поведение, pipeline, reset/reconnect-flow и диагностика не менялись.

## Затронутые файлы

- `Final_Test_Hybrid/Components/Schemes/HeatingScheme.razor`
- `Final_Test_Hybrid/Components/Overview/AiCallCheck.razor.cs`
- `Final_Test_Hybrid/Components/Overview/PidRegulatorCheck.razor.cs`
- `Final_Test_Hybrid/Components/Overview/RtdCalCheck.razor.cs`
- `Final_Test_Hybrid/Components/Overview/LampOutput.razor`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно. Остались внешние warnings `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0` в `Final_Test_Hybrid.csproj` и `Final_Test_Hybrid.Tests.csproj`, не связанные с этой правкой.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Overview/AiCallCheck.razor.cs;Final_Test_Hybrid/Components/Overview/PidRegulatorCheck.razor.cs;Final_Test_Hybrid/Components/Overview/RtdCalCheck.razor.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\inspect-ui-debug-logs-warning.txt" -e=WARNING` — без warning по изменённым C#-файлам.

## Residual Risks

- Интерактивный desktop-прогон не выполнялся, поэтому фактическое исчезновение шума в runtime-логе подтверждено кодом, а не ручным открытием экранов в приложении.

## Инциденты

- no new incident
