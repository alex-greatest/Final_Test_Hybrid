# 2026-03-19 engineering-autoready-access-and-connection-test-tab

## Контур

- Engineering UI / manual diagnostics / runtime-gating

## Что изменено

- `Hand Program` и `IO Editor` в `MainEngineering` теперь привязаны только к PLC-сигналу `TestAskAuto` через `AutoReadySubscription`.
- `TestAskAuto = true` (`AutoReady ON`) блокирует оба окна.
- `TestAskAuto = false` (`AutoReady OFF`) разрешает оба окна.
- Для этих двух окон больше не учитываются `PreExecution`, `PlcReset`, `post-AskEnd`, `SettingsAccessState` и `ErrorCoordinator.CurrentInterrupt`.
- `Основные настройки` оставлены на полном runtime-gate без этого исключения.
- В `HandProgramDialog` вкладка `Тест связи` переведена на runtime-контракт через `BoilerState.IsTestRunning`:
  - при активном тесте вкладка disabled;
  - если тест стартует во время открытой вкладки, диалог автоматически переключается на безопасную вкладку;
  - `ConnectionTestPanel` выгружается и завершает ручной diagnostic session штатным dispose-path.
- Stable docs синхронизированы:
  - `Docs/ui/SettingsBlockingGuide.md`
  - `Docs/diagnostics/DiagnosticGuide.md`

## Затронутые файлы

- `Final_Test_Hybrid/Components/Engineer/MainEngineering.razor`
- `Final_Test_Hybrid/Components/Engineer/MainEngineering.razor.cs`
- `Final_Test_Hybrid/Components/Engineer/Modals/HandProgramDialog.razor`
- `Final_Test_Hybrid/Docs/ui/SettingsBlockingGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; compile errors не обнаружены. Остался известный warning `MSB3277` по конфликту `WindowsBase 4.0.0.0` vs `5.0.0.0` из зависимостей `Microsoft.Web.WebView2.Wpf`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно; CLI сообщил только об общих workspace warnings без переписывания файлов.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Engineer/MainEngineering.razor.cs;Final_Test_Hybrid/Components/Engineer/Modals/HandProgramDialog.razor" --no-build --format=Text "--output=inspect-warning-engineering-autoready.txt" -e=WARNING` — новых warning по change-set не выявил; в отчёте остались pre-existing unused handler methods в `MainEngineering.razor.cs`.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Engineer/MainEngineering.razor.cs;Final_Test_Hybrid/Components/Engineer/Modals/HandProgramDialog.razor" --no-build --format=Text "--output=inspect-hint-engineering-autoready.txt" -e=HINT` — без новых hint по change-set; в отчёте остались pre-existing suggestions по unused/static methods и `GC.SuppressFinalize`.

## Инциденты

- no new incident
