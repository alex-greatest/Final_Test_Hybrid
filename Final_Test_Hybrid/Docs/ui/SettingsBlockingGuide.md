# Settings Blocking — Блокировка настроек и инженерных окон

## Обзор

Элементы в панели Engineer блокируются во время критических операций для предотвращения небезопасных изменений конфигурации и ручных действий вне разрешённого runtime-контекста.

## Галочки настроек

| Компонент | Файл | Что переключает |
|-----------|------|-----------------|
| SwitchMes | `Components/Engineer/SwitchMes.razor.cs` | Режим MES |
| OperatorAuthorizationQr | `Components/Engineer/OperatorAuthorizationQr.razor.cs` | QR авторизация оператора |
| AdminAuthorizationQr | `Components/Engineer/AdminAuthorizationQr.razor.cs` | QR авторизация админа |

## Условия блокировки галочек

### Все компоненты (4 условия)
```csharp
private bool IsDisabled => PreExecution.IsProcessing
    || !SettingsAccessState.CanInteract
    || PlcResetCoordinator.IsActive
    || ErrorCoordinator.CurrentInterrupt != null;
```

## Инженерные окна MainEngineering

### Компоненты

| Компонент | Файл | Блокировка |
|-----------|------|------------|
| Основные настройки | `Components/Engineer/MainEngineering.razor.cs` | Полный runtime-gate |
| Hand Program | `Components/Engineer/MainEngineering.razor.cs` | Только PLC `TestAskAuto` |
| IO Editor | `Components/Engineer/MainEngineering.razor.cs` | Только PLC `TestAskAuto` |

### Основные настройки

```csharp
private bool IsMainSettingsDisabled => PreExecution.IsProcessing
    || !SettingsAccessState.CanInteract
    || PlcResetCoordinator.IsActive
    || PreExecution.IsPostAskEndFlowActive()
    || ErrorCoordinator.CurrentInterrupt != null;
```

### Hand Program / IO Editor

Для этих двух окон используется только PLC-сигнал `BaseTags.TestAskAuto`, приходящий через `AutoReadySubscription`.

```csharp
private bool IsHandProgramDisabled => AutoReady.IsReady;
private bool IsIoEditorDisabled => AutoReady.IsReady;
```

- `TestAskAuto = true` (`AutoReady ON`) -> окна заблокированы.
- `TestAskAuto = false` (`AutoReady OFF`) -> окна доступны.
- `PreExecution`, `PlcReset`, `post-AskEnd`, `SettingsAccessState` и `ErrorCoordinator.CurrentInterrupt` для этих двух окон больше не участвуют в решении.
- На `Основные настройки` это правило не распространяется.

## Сервисы-источники блокировки

| Сервис | Свойство | Когда блокирует |
|--------|----------|-----------------|
| `PreExecutionCoordinator` | `IsProcessing` | Pre-execution шаги выполняются |
| `SettingsAccessStateManager` | `!CanInteract` | Продолжает блокировать `Основные настройки` и галочки настроек |
| `AutoReadySubscription` | `IsReady` | Для `Hand Program` / `IO Editor` является единственным источником блокировки |
| `PlcResetCoordinator` | `IsActive` | Во время сброса PLC |
| `PreExecutionCoordinator` | `IsPostAskEndFlowActive()` | Во время post-AskEnd cleanup/reset decision |
| `ErrorCoordinator` | `CurrentInterrupt != null` | Есть активное прерывание |

### SettingsAccessState логика
```csharp
State = hasNoTests || isOnScanStep
    ? SettingsAccessState.Allowed
    : SettingsAccessState.Blocked;
```

**Разрешено:** нет тестов ИЛИ на scan step
**Заблокировано:** тест выполняется И не на scan step

### Источник истины для scan step

- Runtime-определение scan шага выполняется через внутренний marker (`scanStepId`) в `TestSequenseService`.
- Проверка `IsOnActiveScanStep` больше не зависит от текстового `Module` (переименования шага не ломают блокировки).
- Marker обновляется только в lifecycle scan-строки (`EnsureScanStepExists`, `ClearAll`, `ClearAllExceptScan(SequenceClearMode mode)`).

## Подписки на события

Все компоненты подписываются на события изменения состояния:
- `PreExecution.OnStateChanged`
- `SettingsAccessState.OnStateChanged`
- `AutoReady.OnStateChanged` — только `MainEngineering`
- `PlcResetCoordinator.OnActiveChanged`
- `ErrorCoordinator.OnInterruptChanged`

## Диаграмма

```
┌─────────────────────────────────────────────────────────────┐
│                    Settings Panel                           │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌──────────────────┐  ┌────────────────┐  │
│  │  SwitchMes  │  │ OperatorAuthQr   │  │  AdminAuthQr   │  │
│  │  (4 cond.)  │  │   (4 cond.)      │  │   (4 cond.)    │  │
│  └──────┬──────┘  └────────┬─────────┘  └───────┬────────┘  │
└─────────┼──────────────────┼────────────────────┼───────────┘
          │                  │                    │
          ▼                  ▼                    ▼
    ┌─────────────────────────────────────────────────────┐
    │              Blocking Services                      │
    ├─────────────────────────────────────────────────────┤
    │ PreExecutionCoordinator ←─────┐                     │
    │ SettingsAccessStateManager ←──┼── Общие для всех    │
    │ PlcResetCoordinator ←─────────┤                     │
    │ ErrorCoordinator ←────────────┘                     │
    └─────────────────────────────────────────────────────┘
```
