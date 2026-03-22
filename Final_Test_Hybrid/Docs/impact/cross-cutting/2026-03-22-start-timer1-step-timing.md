# 2026-03-22 start timer1 step timing

## Контур

- pre-execution step timing
- step timing persistence
- log viewer step timings

## Что изменено

- В `IStepTimingService` добавлен отдельный completed-timing path:
  `AddCompletedStepTiming(name, description, duration)`.
- `StepTimingService` теперь умеет добавлять завершённую запись напрямую в `_records`
  без запуска active timer lifecycle.
- `StartTimer1Step` не переводился на `StartCurrentStepTiming/StopCurrentStepTiming`:
  runtime ownership active-step и pause/reset semantics не менялись.
- После успешного `ExecuteStartTimer1Async(...)` pre-execution coordinator
  добавляет completed-запись:
  - `Name = Misc/StartTimer1`
  - `Description = Запуск таймера 1`
  - `Duration = 00.00`
- Запись штатно остаётся видимой для всех текущих consumers `StepTimingService`:
  - `StepTimingsGrid`
  - `TB_STEP_TIME`
  - `MES time[]`
- Stable docs синхронизированы:
  - `Docs/execution/StepTimingGuide.md`
  - `Docs/execution/StepsGuide.md`
- `no new incident`: change-set расширяет историю времени шагов, но не вводит новый production failure mode.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Timing/StepTimingService.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Timing/StepTimingService.Completed.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Pipeline.cs`
- `Final_Test_Hybrid.Tests/TestSupport/PreExecutionTestContextFactory.cs`
- `Final_Test_Hybrid.Tests/Runtime/StepTimingServiceTests.cs`
- `Final_Test_Hybrid.Tests/Runtime/StartTimer1StepTimingTests.cs`
- `Final_Test_Hybrid/Docs/execution/StepTimingGuide.md`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`

## Проверки

- `dotnet test Final_Test_Hybrid.Tests\\Final_Test_Hybrid.Tests.csproj --filter "FullyQualifiedName~StepTimingServiceTests|FullyQualifiedName~StartTimer1StepTimingTests"` — успешно, `3/3`; сохранены baseline warnings `MSB3277` по конфликту `WindowsBase`.
- `dotnet build Final_Test_Hybrid.slnx` — первый прогон упёрся в внешний lock `Final_Test_Hybrid.dll` от `VBCSCompiler`; повторный прогон с `-p:UseSharedCompilation=false` выполнен успешно, сохранены baseline warnings `MSB3277`.
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Timing/StepTimingService.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Timing/StepTimingService.Completed.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Pipeline.cs;Final_Test_Hybrid.Tests/TestSupport/PreExecutionTestContextFactory.cs;Final_Test_Hybrid.Tests/Runtime/StepTimingServiceTests.cs;Final_Test_Hybrid.Tests/Runtime/StartTimer1StepTimingTests.cs" --no-build --format=Text "--output=D:\\projects\\Final_Test_Hybrid\\.codex-build\\inspect-warning-starttimer1-step-timing.txt" -e=WARNING` — warning-level чистый отчёт.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Timing/StepTimingService.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Timing/StepTimingService.Completed.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Pipeline.cs;Final_Test_Hybrid.Tests/TestSupport/PreExecutionTestContextFactory.cs;Final_Test_Hybrid.Tests/Runtime/StepTimingServiceTests.cs;Final_Test_Hybrid.Tests/Runtime/StartTimer1StepTimingTests.cs" --no-build --format=Text "--output=D:\\projects\\Final_Test_Hybrid\\.codex-build\\inspect-hint-starttimer1-step-timing.txt" -e=HINT` — только низкоприоритетные hints: неполный `switch` в существующем scan-result коде, рекомендация `GC.SuppressFinalize(this)` в `StepTimingService.Dispose()` и suggestions по видимости test stubs; runtime-рисков по change-set не найдено.

## Инциденты

- no new incident
