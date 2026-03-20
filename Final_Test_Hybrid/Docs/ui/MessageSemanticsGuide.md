# MessageSemanticsGuide.md

## Назначение

Этот документ фиксирует source-of-truth для нижней основной строки (`MessageHelper` -> `MessageService`) и её согласования с runtime ownership.

Отдельно:

- toast-уведомления идут через interrupt behaviors и `NotificationServiceWrapper`;
- блокировки header/settings живут в своих UI-компонентах;
- result image может временно скрывать `MessageHelper`, но не отменяет внутреннюю семантику `MessageService`.

## Источники истины в коде

- `Final_Test_Hybrid/Services/Main/Messages/MessageService.cs`
- `Final_Test_Hybrid/Services/Main/Messages/MessageServiceResolver.cs`
- `Final_Test_Hybrid/Services/Main/Messages/MessageTextResources.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/RuntimeTerminalState.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/ErrorCoordinator.cs`
- `Final_Test_Hybrid/Services/Steps/Infrastructure/Execution/ErrorCoordinator/Behaviors/PlcConnectionLostBehavior.cs`
- `Final_Test_Hybrid/Components/Main/MessageHelper.razor`
- `Final_Test_Hybrid/MyComponent.razor`
- `Final_Test_Hybrid/Form1.resx`

## Контракт слоя сообщений

- `MessageService` является source-of-truth только для main message нижней строки.
- `MessageService` собирает snapshot из `OperatorState`, `AutoReadySubscription`, `OpcUaConnectionState`, `ScanModeController`, `ExecutionPhaseState`, `ErrorCoordinator`, `PlcResetCoordinator`, `PreExecutionCoordinator`, `RuntimeTerminalState`, `BoilerState`.
- Тексты main message и `PlcConnectionLost` toast читаются через `MessageTextResources` из `Form1.resx`; новые операторские строки в этом контуре нельзя добавлять только literal-ами в C#.
- Terminal ownership приходит из `RuntimeTerminalState`:
  - `IsCompletionActive` — completion-handshake после result image;
  - `IsPostAskEndActive` — post-AskEnd decision flow после PLC reset.
- `Reset UI busy` для main message считается по правилу:
  `PlcResetCoordinator.IsActive || PreExecutionCoordinator.IsPostAskEndFlowActive()`.
- `PreExecutionCoordinator` остаётся источником только для expanded reset-gate; terminal-owner post-AskEnd для main message читается из `RuntimeTerminalState.IsPostAskEndActive`.
- Raw `AutoReady` и raw connection не должны перебивать terminal ownership и interrupt ownership.

## Порядок приоритетов

| Порядок | Сценарий | Сообщение |
|--------|----------|-----------|
| 1 | `CurrentInterrupt == PlcConnectionLost && ResetUiBusy` | `Потеря связи с PLC. Выполняется сброс...` |
| 2 | `CurrentInterrupt == TagTimeout && ResetUiBusy` | `Нет ответа от ПЛК. Выполняется сброс...` |
| 3 | raw `!IsConnected && ResetUiBusy` fallback | `Потеря связи с PLC. Выполняется сброс...` |
| 4 | `CurrentInterrupt == PlcConnectionLost` до старта reset | `Потеря связи с PLC. Ожидание сброса...` |
| 5 | `CurrentInterrupt == TagTimeout` | `Нет ответа от ПЛК` |
| 6 | `CurrentInterrupt == BoilerLock` | `Блокировка котла. Ожидание восстановления` |
| 7 | `RuntimeTerminalState.IsCompletionActive` | `Тест завершён. Ожидание решения PLC...` |
| 8 | `RuntimeTerminalState.IsPostAskEndActive` | `Сброс подтверждён. Ожидание решения PLC...` |
| 9 | generic `ResetUiBusy` | `Сброс теста...` |
| 10 | raw `!IsConnected` вне interrupt/terminal/reset | `Нет связи с PLC` |
| 11 | `CurrentInterrupt == AutoModeDisabled` | `Ожидание автомата` |
| 12 | raw `!AutoReady` idle/pre-start fallback | `Ожидание автомата` |
| 13 | `!IsAuthenticated` | `Войдите в систему` |
| 14 | `ScanModeEnabled && !IsTestRunning && Phase == null` | `Отсканируйте серийный номер котла` |
| 15 | `Phase != null` | Сообщение фазы выполнения |
| 16 | Иначе | `""` |

## Обязательные сценарии

### post-AskEnd + AutoReady OFF

- Во время active post-AskEnd окна raw `!AutoReady` не должен производить auto-message.
- Ожидаемый текст: `Сброс подтверждён. Ожидание решения PLC...`
- Текст `Нет автомата. Выполняется сброс...` для этого окна запрещён.

### PlcConnectionLost + brief reconnect before delayed reset

- Если `CurrentInterrupt == PlcConnectionLost`, main message обязан остаться в interrupt narrative даже после краткого восстановления связи.
- До фактического `ResetUiBusy` используется `Потеря связи с PLC. Ожидание сброса...`
- После входа в reset-path используется `Потеря связи с PLC. Выполняется сброс...`
- Возврат к raw `Нет связи с PLC` или к normal/empty message допустим только после очистки interrupt.

### completion window

- Во время `IsCompletionActive` main message семантически считается terminal-state, даже если `MessageHelper` скрыт result image.
- Ожидаемый текст: `Тест завершён. Ожидание решения PLC...`
- Изменение layout не требуется: result image остаётся выше по приоритету рендера.

### Raw AutoReady fallback

- Raw `!AutoReady` допускается только как idle/pre-start fallback:
  - оператор авторизован;
  - активного interrupt нет;
  - terminal window нет;
  - reset busy нет;
  - фазы выполнения нет;
  - тест не идёт.

## Toast и main message

- Нижняя строка и toast не обязаны рендериться одним компонентом, но должны рассказывать одну и ту же runtime-историю.
- Для `PlcConnectionLost` toast показывает `Сброс через 5 сек` и живёт те же 5 секунд через явный `duration`.
- Main message в это окно показывает pending-reset или reset-busy сценарий, а не normal/raw connection state.

## Что не покрывает этот guide

- Блокировки `OperatorInfo`, `BoilerInfo`, engineer/settings.
- Порядок рендера result image и `RangeSliderDisplay`.
- Toast semantics для interrupt-ов, кроме `PlcConnectionLost`.
