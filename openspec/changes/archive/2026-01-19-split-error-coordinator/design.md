# Design: Разделение ErrorCoordinator на partial classes

## Context

После рефакторинга `2026-01-19-refactor-error-coordinator` класс `ErrorCoordinator.cs` консолидировал 4 файла в один. Результат — 400 строк, что превышает project convention (max 300 строк).

## Current Structure

```
ErrorCoordinator.cs (400 строк)
├─ Поля и конструктор (строки 1-57)
├─ #region Event Subscriptions (строки 59-107) ~50 строк
├─ #region Interrupt Handling (строки 109-187) ~80 строк
├─ #region Error Resolution (строки 189-289) ~100 строк
├─ #region Reset and Recovery (строки 291-341) ~50 строк
├─ #region IInterruptContext Implementation (строки 343-350) ~8 строк
├─ #region Disposal (строки 352-373) ~22 строк
└─ #region Helpers (строки 375-389) ~15 строк
```

## Target Structure

```
ErrorCoordinator.cs (~130 строк)
├─ Поля, конструктор, events
├─ Event Subscriptions
├─ IInterruptContext Implementation
├─ Disposal
└─ Helpers

ErrorCoordinator.Interrupts.cs (~80 строк)
└─ Interrupt Handling
   ├─ HandleInterruptAsync
   ├─ TryAcquireLockAsync
   ├─ ReleaseLockSafe
   ├─ ProcessInterruptAsync
   └─ SetCurrentInterrupt

ErrorCoordinator.Resolution.cs (~150 строк)
├─ Error Resolution
│  ├─ WaitForResolutionAsync
│  ├─ WaitForResolutionCoreAsync
│  ├─ WaitForOperatorSignalAsync
│  ├─ SendAskRepeatAsync
│  └─ WaitForRetrySignalResetAsync
└─ Reset and Recovery
   ├─ Reset
   ├─ ForceStop
   ├─ TryResumeFromPauseAsync
   ├─ ClearConnectionErrors
   └─ ClearCurrentInterrupt
```

## Decisions

### 1. Группировка Event Subscriptions с основным файлом

**Причина:** Event subscriptions напрямую связаны с:
- Конструктором (SubscribeToEvents вызывается там)
- Disposal (UnsubscribeFromEvents)
- Fire-and-forget методы используют `_disposeCts`

Выносить отдельно создаст искусственное разделение связанного кода.

### 2. Объединение Error Resolution и Reset/Recovery

**Причина:** Reset и Recovery семантически связаны с resolution flow:
- `TryResumeFromPauseAsync` — часть recovery после прерывания
- `ClearConnectionErrors` и `ClearCurrentInterrupt` — cleanup resolution state
- Reset вызывается из resolution flow

### 3. Interrupt Handling отдельно

**Причина:** Самостоятельный блок с:
- Lock-логикой (`TryAcquireLockAsync`, `ReleaseLockSafe`)
- Единой точкой входа (`HandleInterruptAsync`)
- Делегацией в behavior registry

Не имеет прямых зависимостей от resolution.

## Trade-offs

| Решение | Pro | Con |
|---------|-----|-----|
| 3 файла | Баланс между атомарностью и связностью | Некоторые методы на границе (SetCurrentInterrupt) |
| Event Subscriptions в основном | Связность с lifecycle | Основной файл чуть больше |
| Reset + Resolution | Семантическая связность | Resolution файл самый большой (~150) |

## No Changes To

- `IErrorCoordinator` interface
- `ErrorCoordinatorDependencies.cs`
- Public API
- Behavior
