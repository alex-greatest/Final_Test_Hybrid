# 2026-03-19 modbus-traffic-stabilization

## Контур

- Diagnostics / Modbus command queue / display-only UI polling / step-level Modbus execution steps (`Coms/*`, `CH/*CompareFlowNtc*`, `DHW/*CompareFlowNtc*`) / BoilerLock safety feed

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
- В `TestStepContext` введён единый контракт step-level Modbus:
  - обычные test-step операции идут через `PacedDiagReader` / `PacedDiagWriter`;
  - pacing больше не hardcoded `150 мс`, а берёт окно из `Diagnostic:WriteVerifyDelayMs`;
  - ожидание pacing стало `pause-aware` и `cancellation-aware`, то есть во время `Auto OFF` countdown не тикает и прерывается тем же `CancellationToken`.
- Из активных test steps удалены ручные `DelayAsync(...WriteVerifyDelayMs...)`, которые дублировали pacing перед соседними Modbus `read/write`.
- На paced wrappers переведены активные step-level Modbus шаги:
  - `Write_Test_Byte_ON`, `Write_Test_Byte_OFF`, `Check_Test_Byte_ON`, `Check_Test_Byte_OFF`;
  - `Write_Soft_Code_Plug`, `Read_Soft_Code_Plug`, `Safety_Time`;
  - `CH_Pump_Start`, `CH_Reset`, `Pump_Start_Func_Reset`, `Set_DHW_Tank_Mode`;
  - `CH_Start_Max_Heatout`, `CH_Start_Min_Heatout`, `CH_Start_ST_Heatout`;
  - `Delete_Error_History`, `Factory_Reset`, `Read_CH_Poti_Setpoint`, `Read_DHW_Poti_Setpoint`, `Read_ECU_Version`, `Set_Fan_Map`, `Set_To_Zero_CH_Pump_Overrun`;
  - `CH/*CompareFlowNtc*`, `DHW/*CompareFlowNtc*`.
- `CheckCommsStep` и polling-циклы бизнес-ожидания не менялись: их wall-clock/business waits не считаются pacing-дублем и сохранены без ослабления existing safety-flow.
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
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ChStartStHeatoutStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/DeleteErrorHistoryStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/FactoryResetStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadChPotiSetpointStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadDhwPotiSetpointStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/ReadEcuVersionStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/SetFanMapStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/Coms/SetToZeroChPumpOverrunStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/CH/CompareFlowNtcTemperatureColdStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/CH/CompareFlowNtcTemperaturesHotStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/DHW/CompareFlowNtcTemperatureColdStep.cs`
- `Final_Test_Hybrid/Services/Steps/Steps/DHW/CompareFlowNtcTemperatureHotStep.cs`
- `Final_Test_Hybrid/Components/Main/Parameter/CH.razor`
- `Final_Test_Hybrid/Components/Main/Parameter/DHW.razor`
- `Final_Test_Hybrid/Services/Diagnostic/Connection/DiagnosticSettings.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/Coordinator/TestExecutionCoordinator.cs`
- `Final_Test_Hybrid/Docs/execution/StepsGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/DiagnosticGuide.md`
- `Final_Test_Hybrid/Docs/diagnostics/BoilerLockGuide.md`
- `Final_Test_Hybrid/appsettings.json`

## Проверки

- `dotnet build Final_Test_Hybrid.slnx` — успешно; остаётся один внешний warning `MSB3277` по конфликту `WindowsBase 4.0.0.0 / 5.0.0.0`.
- `dotnet format analyzers Final_Test_Hybrid.slnx --verify-no-changes` — успешно.
- `dotnet format style Final_Test_Hybrid.slnx --verify-no-changes` — успешно; workspace warning без влияния на результат.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=<changed.cs>" --no-build --format=Text "--output=D:\\projects\\Final_Test_Hybrid\\.codex-build\\inspect-warning-modbus-step-pacing.txt" -e=WARNING` — отчёт сформирован, новых блокирующих warning по этому change-set не выявлено.
- `jb inspectcode Final_Test_Hybrid.slnx "--include=<changed.cs>" --no-build --format=Text "--output=D:\\projects\\Final_Test_Hybrid\\.codex-build\\inspect-hint-modbus-step-pacing.txt" -e=HINT` — отчёт сформирован; suggestion/hint уровня cleanup есть, но новых safety-регрессий по pacing/пауза/отмена не найдено.

## Residual Risks

- Hidden legacy-файлы `*Old*.cs` в `Coms/*` сознательно не менялись в этом change-set; при их возврате в runtime они не получат новый step-level contract автоматически.
- В worktree есть несвязанные с этой задачей изменённые файлы в execution/runtime/docs; они не ревьюились как часть данного change-set и не откатывались.

## Инциденты

- no new incident
