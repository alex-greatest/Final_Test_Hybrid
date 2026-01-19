# Change: Рефакторинг ErrorCoordinator для упрощения и читаемости

## Why
ErrorCoordinator накопил избыточную сложность:
- 3 примитива синхронизации (`_operationLock`, `_isHandlingInterrupt`, `_activeOperations`) где достаточно одного
- 4 partial-файла (~350 строк) для логики, которая поместится в ~200 строк
- 3 dependency-группы с чрезмерной абстракцией
- 3 перегрузки `WaitForResolutionAsync` с путаными параметрами

## What Changes

### 1. Упрощение синхронизации
- **BREAKING**: Убрать `_activeOperations` — использовался только для graceful shutdown
- Объединить `_isHandlingInterrupt` и `_operationLock` в один `SemaphoreSlim(1,1)`
- Удалить `WaitForPendingOperationsAsync` — заменить на таймаут в `DisposeAsync`

### 2. Консолидация файлов
- Объединить `ErrorCoordinator.cs`, `.Interrupts.cs`, `.Recovery.cs` в один файл
- Сохранить `ErrorCoordinatorDependencies.cs` отдельно (группировка зависимостей полезна)
- Behaviors оставить без изменений — хорошо изолированы

### 3. Упрощение зависимостей
- Убрать `ErrorCoordinatorState` — переместить зависимости напрямую
- Оставить `ErrorCoordinatorSubscriptions` и `ErrorResolutionServices` — логическая группировка

### 4. Упрощение WaitForResolution API
- Заменить 3 перегрузки на один метод с optional параметрами:
  ```csharp
  Task<ErrorResolution> WaitForResolutionAsync(
      WaitForResolutionOptions? options = null,
      CancellationToken ct = default);
  ```
- Создать record `WaitForResolutionOptions` для чистого API

### 5. Улучшение читаемости
- Переименовать методы для большей ясности
- Убрать избыточные helper-методы (`TryAcquireLockCoreAsync`, `AcquireAndValidateAsync`)
- Упростить вложенность в `HandleInterruptAsync`

## Impact

### Затронутые файлы
- `ErrorCoordinator/ErrorCoordinator.cs` — основные изменения
- `ErrorCoordinator/ErrorCoordinator.Interrupts.cs` — удаление (объединение)
- `ErrorCoordinator/ErrorCoordinator.Recovery.cs` — удаление (объединение)
- `ErrorCoordinator/ErrorCoordinatorDependencies.cs` — упрощение
- `ErrorCoordinator/IErrorCoordinator.cs` — новый API WaitForResolution

### Затронутые вызовы
- `ColumnExecutor` — использует `WaitForResolutionAsync`
- `TestExecutionCoordinator` — подписка на `OnReset`
- `PreExecutionCoordinator` — подписка на `OnReset`
- `ReworkDialogService` — подписка на `OnReset`

### Риски
- Изменение API `WaitForResolutionAsync` требует обновления вызывающего кода
- Упрощение синхронизации может теоретически повлиять на race conditions (но текущий код over-engineered)
