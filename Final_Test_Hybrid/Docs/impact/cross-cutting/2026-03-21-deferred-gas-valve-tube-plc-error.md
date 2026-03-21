# 2026-03-21 deferred gas valve tube plc error

## Контур

- PLC error monitoring
- main message source-of-truth
- gas max-level runtime UX

## Что изменено

- Для `П-403-03` и `П-407-03` immediate-path в `PlcErrorMonitorService` отключён.
- Добавлен `GasValveTubeDeferredErrorService`:
  - при `Al_NotConnectSensorPGB=true` сразу включает low-priority main message
    `Не подключена трубка газового клапана`;
  - ждёт 30 секунд и поднимает `RaisePlc(...)` только если тег всё ещё активен;
  - при `false` сразу очищает message-state и снимает активную PLC-ошибку, если она уже была поднята.
- Deferred state дополнительно привязан к runtime active-step через `IStepTimingService`:
  - main message и 30-секундный таймер живут только внутри
    `Gas/Set_Gas_and_P_Burner_Max_Levels` и
    `Gas/Set_Gas_and_P_Burner_Min_Levels`;
  - если target gas-step завершился или reset cleanup убрал active-step,
    pending defer отменяется сразу и main message исчезает немедленно;
  - late start шага при уже активном PLC-теге теперь корректно подхватывает
    deferred-path без immediate raise.
- Возвращён пропущенный блок `Gas/Set_Gas_and_P_Burner_Min_Levels` в `ErrorDefinitions.StepErrors`,
  чтобы `П-407-00 ... П-407-03` снова попадали в `All -> PlcErrors`.
- `RangeSliderDisplay` теперь дублирует тот же операторский текст
  `Не подключена трубка газового клапана` красной строкой над слайдерами,
  без собственного owner-state: видимость привязана к
  `GasValveTubeDeferredErrorService.IsMessageActive`.
- После визуальной проверки геометрии slider-экрана red alert переведён в
  absolute-overlay внутри `RangeSliderDisplay`, чтобы сообщение не растягивало
  контейнер и не сдвигало сами слайдеры по высоте.
- Для operator-attention red alert в `RangeSliderDisplay` дополнительно включено
  мигание через CSS animation; при `prefers-reduced-motion: reduce` анимация
  отключается.
- `MessageService` и `MessageServiceResolver` расширены новым low-priority сценарием.
- Новый операторский текст вынесен в `Form1.resx` / `MessageTextResources`.
- Stable docs синхронизированы:
  - `Docs/runtime/ErrorSystemGuide.md`
  - `Docs/ui/MessageSemanticsGuide.md`
- `no new incident`: change-set меняет UX already-known PLC failure signal и способ его эскалации, но не выявил нового production failure mode.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Errors/GasValveTubeDeferredErrorService.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Timing/StepTimingService.cs`
- `Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs`
- `Final_Test_Hybrid/Services/Errors/PlcErrorValueNormalizer.cs`
- `Final_Test_Hybrid/Services/Main/Messages/MessageService.cs`
- `Final_Test_Hybrid/Services/Main/Messages/MessageServiceResolver.cs`
- `Final_Test_Hybrid/Services/Main/Messages/MessageTextResources.cs`
- `Final_Test_Hybrid/Services/OpcUa/PlcInitializationCoordinator.cs`
- `Final_Test_Hybrid/Models/Errors/ErrorDefinitions.DeferredPlc.cs`
- `Final_Test_Hybrid/Form1.resx`
- `Final_Test_Hybrid.Tests/Runtime/GasValveTubeDeferredErrorServiceTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/PlcErrorMonitorServiceTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/MessageServiceResolverTests.cs`
- `Final_Test_Hybrid/Docs/runtime/ErrorSystemGuide.md`
- `Final_Test_Hybrid/Docs/ui/MessageSemanticsGuide.md`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~GasValveTubeDeferredErrorServiceTests|FullyQualifiedName~MessageServiceResolverTests"` — успешно, 15/15.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~GasValveTubeDeferredErrorServiceTests|FullyQualifiedName~MessageServiceResolverTests"` — успешно, 17/17 после добавления step-scoped regression tests.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~PlcErrorMonitorServiceTests|FullyQualifiedName~GasValveTubeDeferredErrorServiceTests|FullyQualifiedName~MessageServiceResolverTests"` — успешно, 7/7.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~GasValveTubeDeferredErrorServiceTests"` в isolated verify-copy — успешно, 7/7.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~MessageServiceResolverTests"` в isolated verify-copy — успешно, 12/12.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~PlcErrorMonitorServiceTests"` в isolated verify-copy — успешно, 2/2.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~PlcConnectionLostBehaviorNotificationTests|FullyQualifiedName~ErrorCoordinatorOwnershipTests|FullyQualifiedName~EcuErrorSyncServiceTests"` — успешно, 7/7.
- `dotnet build Final_Test_Hybrid.slnx` — успешно; сохранены baseline warnings `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`.
- `dotnet build Final_Test_Hybrid.slnx` в isolated verify-copy — успешно; сохранены baseline warnings `MSB3277` по конфликту `WindowsBase 4.0.0.0/5.0.0.0`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` в isolated verify-copy — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` в isolated verify-copy — успешно.
- `dotnet build Final_Test_Hybrid.slnx` в isolated verify-copy после добавления red alert в `RangeSliderDisplay` — успешно; сохранены baseline warnings `MSB3277`.
- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --no-build --filter "FullyQualifiedName~GasValveTubeDeferredErrorServiceTests|FullyQualifiedName~MessageServiceResolverTests|FullyQualifiedName~PlcErrorMonitorServiceTests"` в isolated verify-copy после slider-UI change — успешно, 21/21.
- `dotnet build Final_Test_Hybrid.slnx` в isolated verify-copy после перевода red alert в absolute-overlay — успешно; сохранены baseline warnings `MSB3277`.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` в isolated verify-copy после overlay-fix — успешно.
- `dotnet build Final_Test_Hybrid.slnx` в isolated verify-copy после добавления blink animation для red alert — успешно; сохранены baseline warnings `MSB3277`.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` в isolated verify-copy после blink animation — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Timing/StepTimingService.cs;Final_Test_Hybrid/Services/Errors/GasValveTubeDeferredErrorService.cs;Final_Test_Hybrid.Tests/Runtime/GasValveTubeDeferredErrorServiceTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-gas-valve-step-scope.txt" -e=WARNING` — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.DeferredPlc.cs;Final_Test_Hybrid/Services/Errors/GasValveTubeDeferredErrorService.cs;Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs;Final_Test_Hybrid/Services/Errors/PlcErrorValueNormalizer.cs;Final_Test_Hybrid/Services/Main/Messages/MessageService.cs;Final_Test_Hybrid/Services/Main/Messages/MessageServiceResolver.cs;Final_Test_Hybrid/Services/Main/Messages/MessageTextResources.cs;Final_Test_Hybrid/Services/OpcUa/PlcInitializationCoordinator.cs;Final_Test_Hybrid/Services/DependencyInjection/StepsServiceExtensions.cs;Final_Test_Hybrid.Tests/TestSupport/TestInfrastructure.cs;Final_Test_Hybrid.Tests/Runtime/MessageServiceResolverTests.cs;Final_Test_Hybrid.Tests/Runtime/GasValveTubeDeferredErrorServiceTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-warning-gas-valve-tube.txt" -e=WARNING` — warning-level чистый отчёт.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.DeferredPlc.cs;Final_Test_Hybrid/Services/Errors/GasValveTubeDeferredErrorService.cs;Final_Test_Hybrid/Services/Errors/PlcErrorMonitorService.cs;Final_Test_Hybrid/Services/Errors/PlcErrorValueNormalizer.cs;Final_Test_Hybrid/Services/Main/Messages/MessageService.cs;Final_Test_Hybrid/Services/Main/Messages/MessageServiceResolver.cs;Final_Test_Hybrid/Services/Main/Messages/MessageTextResources.cs;Final_Test_Hybrid/Services/OpcUa/PlcInitializationCoordinator.cs;Final_Test_Hybrid/Services/DependencyInjection/StepsServiceExtensions.cs;Final_Test_Hybrid.Tests/TestSupport/TestInfrastructure.cs;Final_Test_Hybrid.Tests/Runtime/MessageServiceResolverTests.cs;Final_Test_Hybrid.Tests/Runtime/GasValveTubeDeferredErrorServiceTests.cs" --no-build --format=Text "--output=D:\projects\Final_Test_Hybrid\.codex-build\inspect-hint-gas-valve-tube.txt" -e=HINT` — только низкоприоритетные hints/suggestions без warning.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/GasValveTubeDeferredErrorServiceTests.cs;Final_Test_Hybrid.Tests/Runtime/PlcErrorMonitorServiceTests.cs" --no-build --format=Text "--output=...\\inspect-warning-403-407.txt" -e=WARNING` в isolated verify-copy — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Models/Errors/ErrorDefinitions.Steps.cs;Final_Test_Hybrid.Tests/Runtime/GasValveTubeDeferredErrorServiceTests.cs;Final_Test_Hybrid.Tests/Runtime/PlcErrorMonitorServiceTests.cs" --no-build --format=Text "--output=...\\inspect-hint-403-407.txt" -e=HINT` в isolated verify-copy — только low-priority hints: `StepErrors` can be private, `Simplify LINQ expression (use 'All')`.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Main/RangeSliderDisplay.razor" --no-build --format=Text "--output=...\\inspect-warning-range-slider-alert.txt" -e=WARNING` в isolated verify-copy — warning-level чисто.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Components/Main/RangeSliderDisplay.razor" --no-build --format=Text "--output=...\\inspect-hint-range-slider-alert.txt" -e=HINT` в isolated verify-copy — только low-priority hints: `Loop can be converted into LINQ-expression`, `Redundant parentheses`.
- `powershell -ExecutionPolicy Bypass -File C:\Users\Alexander\.codex\skills\localization-sync-guard\scripts\replay_localization_sync.ps1 -RepoRoot . -RequireResourceSync -RequireCyrillicLogs` — успешно.

## Инциденты

- no new incident
