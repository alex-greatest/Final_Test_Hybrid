## ADDED Requirements

### Requirement: Terminal Handshake Ownership
Система MUST трактовать `completion` и `post-AskEnd` как terminal window, которые владеют PLC-решением до финального outcome.

#### Scenario: PLC loss during completion
- **WHEN** completion-handshake активен после записи `End=true`
- **AND** во время ожидания `Req_Repeat=true` или `End=false` пропадает связь с PLC
- **THEN** normal finish не должен выигрывать
- **AND** runtime должен перейти в существующий HardReset path

#### Scenario: PLC loss during post-AskEnd
- **WHEN** post-AskEnd decision flow активен
- **AND** до финального решения теряется PLC connection
- **THEN** cleanup не должен завершаться как normal full cleanup
- **AND** ownership прерывания должен перейти в reset/HardReset path

### Requirement: Post-AskEnd Fail-Safe Release
Система MUST гарантированно отпускать post-AskEnd terminal owner даже при исключении в cleanup или dialog-path.

#### Scenario: Exception after post-AskEnd activation
- **WHEN** `HandleGridClear()` уже поднял `IsPostAskEndActive`
- **AND** в decision flow или cleanup возникает исключение
- **THEN** release terminal state не должен зависеть только от catch-path event-handler
- **AND** координатор должен завершить flow через `finally` или fail-safe release-path

### Requirement: Safe Terminal Decision Reads
Система MUST принимать terminal PLC-решения только по known значениям runtime-cache.

#### Scenario: Unknown completion cache
- **WHEN** completion decision-loop читает `Req_Repeat` и `End`
- **AND** значение в runtime-cache отсутствует или invalid
- **THEN** это состояние не должно трактоваться как `false`
- **AND** completion должен ждать реальное PLC-значение, reset или cancel

#### Scenario: Unknown post-AskEnd cache
- **WHEN** post-AskEnd decision-loop читает `Req_Repeat` и `AskEnd`
- **AND** значение в runtime-cache отсутствует или invalid
- **THEN** это состояние не должно трактоваться как `AskEnd=false`
- **AND** cleanup не должен завершаться раньше реального PLC outcome

### Requirement: AutoReady Ownership In Terminal Window
Система MUST ограничивать влияние `AutoReady` на terminal window.

#### Scenario: AutoReady off during terminal handshake
- **WHEN** terminal handshake активен
- **AND** `AutoReady` переключается в `false`
- **THEN** `ErrorCoordinator` не должен поднимать `AutoModeDisabled`

#### Scenario: AutoReady on with foreign interrupt
- **WHEN** текущий interrupt не равен `AutoModeDisabled`
- **AND** `AutoReady` переключается в `true`
- **THEN** broad-resume не должен снимать этот interrupt

### Requirement: WaitForFalseAsync Raw Cache Recheck
Система MUST завершать `WaitForFalseAsync` только по реальному известному `false`.

#### Scenario: Empty cache after subscribe or resume
- **WHEN** `WaitForFalseAsync` уже подписался на тег
- **AND** выполняется recheck после `SubscribeAsync()` или `Resume()`
- **AND** runtime-cache ещё пуст
- **THEN** ожидание не должно завершаться

### Requirement: Shared Dispatcher Ownership
Система MUST сохранять корректное владение shared `IModbusDispatcher` в `ConnectionTestPanel`.

#### Scenario: Shared dispatcher ownership preserved
- **WHEN** `ConnectionTestPanel` использует уже запущенный shared dispatcher
- **AND** панель закрывается
- **THEN** `DisposeAsync()` не должен вызывать `StopAsync()`
