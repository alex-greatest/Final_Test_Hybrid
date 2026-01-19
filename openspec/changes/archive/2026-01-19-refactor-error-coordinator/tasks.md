# Tasks: Рефакторинг ErrorCoordinator

## 1. Подготовка API
- [x] 1.1 Создать `WaitForResolutionOptions` record в `ErrorCoordinator.cs`
- [x] 1.2 Обновить `IErrorCoordinator` — заменить 3 перегрузки на один метод с options
- [x] 1.3 Добавить backward-compatible extension methods (опционально, если много вызовов)

## 2. Рефакторинг ErrorCoordinator
- [x] 2.1 Объединить содержимое `.Interrupts.cs` и `.Recovery.cs` в `ErrorCoordinator.cs`
- [x] 2.2 Упростить синхронизацию: один `_interruptLock` вместо трёх примитивов
- [x] 2.3 Удалить `WaitForPendingOperationsAsync` — заменить на таймаут в dispose
- [x] 2.4 Упростить helper-методы: inline `TryAcquireLockCoreAsync`, `AcquireAndValidateAsync`
- [x] 2.5 Переименовать `_operationLock` → `_interruptLock` для ясности

## 3. Упрощение зависимостей
- [x] 3.1 Убрать `ErrorCoordinatorState` из `ErrorCoordinatorDependencies.cs`
- [x] 3.2 Передать `PauseTokenSource` напрямую в конструктор `ErrorCoordinator`
- [x] 3.3 Обновить DI регистрацию в `StepsServiceExtensions.cs`

## 4. Обновление вызывающего кода
- [x] 4.1 Найти все вызовы `WaitForResolutionAsync` в кодовой базе
- [x] 4.2 Обновить вызовы на новый API с `WaitForResolutionOptions`
- [x] 4.3 Обновить вызовы `SendAskRepeatAsync` если требуется

## 5. Очистка
- [x] 5.1 Удалить `ErrorCoordinator.Interrupts.cs`
- [x] 5.2 Удалить `ErrorCoordinator.Recovery.cs`
- [x] 5.3 Обновить `ErrorCoordinatorGuide.md` — новая структура и API

## 6. Проверка
- [x] 6.1 Запустить build — убедиться что компилируется
- [x] 6.2 Проверить что прерывания работают (ручной тест если возможно)