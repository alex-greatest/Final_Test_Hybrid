# Settings Blocking — Блокировка галочек настроек

## Обзор

Галочки в панели настроек (Engineer tab) блокируются во время критических операций для предотвращения изменений конфигурации.

## Компоненты

| Компонент | Файл | Что переключает |
|-----------|------|-----------------|
| SwitchMes | `Engineer/SwitchMes.razor.cs` | Режим MES |
| OperatorAuthorizationQr | `Engineer/OperatorAuthorizationQr.razor.cs` | QR авторизация оператора |
| AdminAuthorizationQr | `Engineer/AdminAuthorizationQr.razor.cs` | QR авторизация админа |

## Условия блокировки

### Все компоненты (4 условия)
```csharp
private bool IsDisabled => PreExecution.IsProcessing
    || !SettingsAccessState.CanInteract
    || PlcResetCoordinator.IsActive
    || ErrorCoordinator.CurrentInterrupt != null;
```

## Сервисы-источники блокировки

| Сервис | Свойство | Когда блокирует |
|--------|----------|-----------------|
| `PreExecutionCoordinator` | `IsProcessing` | Pre-execution шаги выполняются |
| `SettingsAccessStateManager` | `!CanInteract` | Тест выполняется И не на scan step |
| `PlcResetCoordinator` | `IsActive` | Во время сброса PLC |
| `ErrorCoordinator` | `CurrentInterrupt != null` | Есть активное прерывание |

### SettingsAccessState логика
```csharp
State = hasNoTests || isOnScanStep
    ? SettingsAccessState.Allowed
    : SettingsAccessState.Blocked;
```

**Разрешено:** нет тестов ИЛИ на scan step
**Заблокировано:** тест выполняется И не на scan step

## Подписки на события

Все компоненты подписываются на события изменения состояния:
- `PreExecution.OnStateChanged`
- `SettingsAccessState.OnStateChanged`
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
