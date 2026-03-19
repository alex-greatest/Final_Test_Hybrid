# 2026-03-19 modbus-traffic-stabilization

## Контур

- Diagnostics / Modbus command queue / display-only UI polling / `Coms/*` execution steps / BoilerLock safety feed

## Что изменено

- В `DiagnosticReadResult<T>` и `DiagnosticWriteResult` добавлен `FailureKind` (`None`, `Communication`, `Functional`).
- `RegisterReader` и `RegisterWriter` теперь классифицируют communication-path отдельно от business/protocol fail и сохраняют прежний контракт `Success/Error`.
- В dispatcher введена внутренняя traffic-class policy:
  - `UI.*` считается `non-critical`;
  - во время `IsReconnecting` такой трафик не попадает в очередь и завершается предсказуемым communication-fail;
  - `PingLoop`, `Coms/*`, recovery/reset flow и `BoilerLock` остаются critical.
- `ModbusPingLoop` переведён на profile-based cadence:
  - active execution: `5000 мс`;
  - idle: `10000 мс`.
- В `TestStepContext` добавлены `PacedDiagReader` / `PacedDiagWriter` и per-column pacing `150 мс` только для opt-in Modbus-heavy шагов.
- На paced wrappers переведены `Coms/*` шаги с последовательными Modbus read/write и retry-mode-restore:
  - `Write_Test_Byte_ON`, `Write_Test_Byte_OFF`, `Check_Test_Byte_ON`, `Check_Test_Byte_OFF`;
  - `Write_Soft_Code_Plug`, `Read_Soft_Code_Plug`, `Safety_Time`;
  - `CH_Pump_Start`, `CH_Reset`, `Pump_Start_Func_Reset`, `Set_DHW_Tank_Mode`;
  - `CH_Start_Max_Heatout`, `CH_Start_Min_Heatout`.
- Для selected `Coms/*` шагов введён единый helper communication-vs-functional сообщений без добавления новых error codes.
- `CH.razor` и `DHW.razor` переведены на guarded polling:
  - idle: `2000 мс`;
  - active execution: `3000 мс`;
  - чтение выполняется только при `Dispatcher.IsStarted && Dispatcher.IsConnected && !Dispatcher.IsReconnecting`;
  - перезапуск polling происходит только после `Connected`;
  - фоновые ошибки display-only polling больше не пишутся per-tick из `BoilerTemperatureService`; подробный лог с источником `Котёл/Modbus` и `FailureKind` пишется в `CH/DHW` с throttling повторяющейся ошибки.
- Обновлены `DiagnosticGuide.md`, `BoilerLockGuide.md` и `appsettings.json` под новые ping profile settings и step-level pacing.

## Затронутые файлы

- `Final_Test_Hybrid/Services/Diagnostic/Models/DiagnosticFailureKind.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Models/DiagnosticReadResult.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Models/DiagnosticWriteResult.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/DiagnosticFailureClassifier.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/RegisterReader.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/RegisterWriter.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/PacedRegisterReader.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/PacedRegisterWriter.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/ModbusDispatcher.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/ModbusDispatcherOptions.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/Internal/ModbusPingLoop.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/Internal/ModbusTrafficClass.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Protocol/CommandQueue/Internal/ModbusTrafficClassifier.cs`
- `Final_Test_Hybrid/Services/Diagnostic/Access/AccessLevelManager.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Registrator/TestStepContext.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Registrator/TestStepModbusPacing.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ComsStepFailureHelper.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/WriteTestByteOnStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/WriteTestByteOffStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/CheckTestByteOnStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/CheckTestByteOffStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/WriteSoftCodePlugStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/WriteSoftCodePlugStep.Parameters.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadSoftCodePlugStep.Actions.Execution.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/SafetyTimeStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChPumpStartStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChResetStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/PumpStartFuncResetStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/SetDhwTankModeStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMaxHeatoutStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartMinHeatoutStep.cs`
- `Final_Test_Hybrid/Components/Main/Parameter/CH.razor`
- `Final_Test_Hybrid/Components/Main/Parameter/DHW.razor`
- `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/BoilerLockGuide.md`
- `Final_Test_Hybrid/appsettings.json`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — не выполнен: `bin\\Debug\\net10.0-windows\\Final_Test_Hybrid.exe` заблокирован запущенным процессом `Final_Test_Hybrid (16436)`.
- `dotnet build Final_Test_Hybrid.slnx -p:UseAppHost=false -p:OutDir=D:\\projects\\Final_Test_Hybrid\\.codex-build\\modbus-stabilization\\` — успешно; остаётся один внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=<changed.cs>" --no-build --format=Text "--output=inspect-warning-modbus-stabilization.txt" -e=WARNING` — новых блокирующих warning по внедрённой логике не выявлено; в отчёте остаются legacy/non-blocking замечания, включая локализуемую строку fallback в `AccessLevelManager`, неиспользуемые positional properties records и историческое сравнение `float` в `ReadSoftCodePlugStep.Actions.Execution.cs`.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=<changed.cs>" --no-build --format=Text "--output=inspect-hint-modbus-stabilization.txt" -e=HINT` — отчёт сформирован; остаются suggestion/hint уровня cleanup по существующим конструкциям (`Logger`/`PauseToken` в `TestStepContext`, рекомендации по object initializer и т.п.), без новых safety-регрессий по reconnect/pacing policy.

## Инциденты

- no new incident
