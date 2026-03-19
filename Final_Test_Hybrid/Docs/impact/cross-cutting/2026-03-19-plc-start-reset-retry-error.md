# 2026-03-19 plc-start-reset-retry-error

## Контур

- PLC steps / retry-skip flow / pre-execution retry / execution docs

## Что изменено

- В test-execution retry-path удалён сброс `Start=false` со стороны PC перед повторным выполнением PLC-шага.
- В execution runtime удалён общий pre-start guard ожидания `Block.End=false` перед запуском PLC-шага.
- В pre-execution retry-path для `BlockBoilerAdapterStep` удалён сброс `Start=false` перед повтором.
- В `BlockBoilerAdapterStep` удалён pre-start guard ожидания `End=false`; шаг пишет `Start=true` сразу и на первом запуске, и на retry.
- Skip-path не менялся:
  - coordinator по-прежнему пишет `Start=false` только при `Skip`;
  - затем ожидает, что PLC сам сбросит `Block.Error=false` и `Block.End=false`.
- `Gas/Set_Required_Pressure` исправлен:
  - `Start=false` больше не пишется в общем completion-path;
  - при `Error` шаг возвращает `Fail(...)` без записи `Start=false`;
  - при `End` сброс `Start=false` сохраняется в success-ветке.
- Stable docs синхронизированы с новым контрактом:
  - success-only reset в шаге;
  - no retry-side reset от PC;
  - skip-only reset в coordinator.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/BlockBoilerAdapterStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Gas/SetRequiredPressureStep.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid/Docs/execution/RetrySkipGuide.md`
- `Final_Test_Hybrid/Docs/execution/CancellationGuide.md`
- `Final_Test_Hybrid/Docs/execution/StateManagementGuide.md`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx`
- `dotnet format analyzers --verify-no-changes Final_Test_Hybrid.slnx`
- `dotnet format style --verify-no-changes Final_Test_Hybrid.slnx`
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs;Final_Test_Hybrid/Services/Steps/Steps/BlockBoilerAdapterStep.cs;Final_Test_Hybrid/Services/Steps/Steps/Gas/SetRequiredPressureStep.cs" --no-build --format=Text "--output=artifacts/inspect-warning-plc-start-reset.txt" -e=WARNING`
- `jb inspectcode Final_Test_Hybrid.slnx "--include=Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.ErrorResolution.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.PlcErrorSignals.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ColumnExecutor.cs;Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/PreExecution/PreExecutionCoordinator.Retry.cs;Final_Test_Hybrid/Services/Steps/Steps/BlockBoilerAdapterStep.cs;Final_Test_Hybrid/Services/Steps/Steps/Gas/SetRequiredPressureStep.cs" --no-build --format=Text "--output=artifacts/inspect-hint-plc-start-reset.txt" -e=HINT`

## Инциденты

- no new incident
