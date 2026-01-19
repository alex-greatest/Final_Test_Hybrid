# Tasks: Разделение ErrorCoordinator на partial classes

## Implementation Tasks

- [x] **1. Создать ErrorCoordinator.Interrupts.cs**
  - Вынести регион `Interrupt Handling`
  - Методы: `HandleInterruptAsync`, `TryAcquireLockAsync`, `ReleaseLockSafe`, `ProcessInterruptAsync`, `SetCurrentInterrupt`
  - Добавить `partial class` declaration

- [x] **2. Создать ErrorCoordinator.Resolution.cs**
  - Вынести регион `Error Resolution`
  - Вынести регион `Reset and Recovery`
  - Методы: `WaitForResolutionAsync`, `WaitForResolutionCoreAsync`, `WaitForOperatorSignalAsync`, `SendAskRepeatAsync`, `WaitForRetrySignalResetAsync`, `Reset`, `ForceStop`, `TryResumeFromPauseAsync`, `ClearConnectionErrors`, `ClearCurrentInterrupt`
  - Добавить `partial class` declaration

- [x] **3. Обновить ErrorCoordinator.cs**
  - Удалить вынесенные методы
  - Удалить пустые регионы
  - Добавить `partial` keyword к class declaration
  - Оставить: поля, конструктор, events, Event Subscriptions, IInterruptContext, Disposal, Helpers

- [x] **4. Проверка билда**
  - `dotnet build` должен пройти без ошибок
  - Все partial classes должны корректно компилироваться

## Validation

- [x] Каждый файл ≤300 строк
  - ErrorCoordinator.cs: 165 строк
  - ErrorCoordinator.Interrupts.cs: 84 строки
  - ErrorCoordinator.Resolution.cs: 162 строки
- [x] `dotnet build` успешен
- [x] Нет изменений в поведении (API идентичен)
