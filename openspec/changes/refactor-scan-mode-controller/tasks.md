# Tasks: Refactor ScanModeController

## 1. Подготовка

- [ ] 1.1 Создать `ScanModePhase.cs` enum: `Idle`, `Active`, `Resetting`
- [ ] 1.2 Добавить helper методы: `IsActive()`, `IsResetting()`, `IsIdle()`

## 2. Bugfixes (критические)

- [ ] 2.1 **CTS Lifecycle:** Добавить Dispose() при замене _loopCts в StartMainLoop()
- [ ] 2.2 **Dispose Race:** Обернуть проверку _disposed и Cancel/Dispose в lock
- [ ] 2.3 **TOCTOU:** Захватить IsScanModeEnabled в локальную переменную в UpdateScanModeState()

## 3. State Migration

- [ ] 3.1 Заменить `_isActivated` + `_isResetting` на `_phase`
- [ ] 3.2 Обновить `IsInScanningPhase` → `_phase == ScanModePhase.Active`
- [ ] 3.3 Обновить `HandleResetStarting()` → `_phase = Resetting`
- [ ] 3.4 Обновить `HandleResetCompleted()` → `_phase = Active/Idle`
- [ ] 3.5 Обновить `TryActivateScanMode()` → проверка/установка `_phase`
- [ ] 3.6 Обновить `TryDeactivateScanMode()` → проверка/установка `_phase`

## 4. Verification

- [ ] 4.1 Build проходит без ошибок
- [ ] 4.2 Сценарий: login → AutoReady → сканирование → logout
- [ ] 4.3 Сценарий: мягкий сброс (reset во время scan step)
- [ ] 4.4 Сценарий: жёсткий сброс (reset во время теста)
- [ ] 4.5 Сценарий: AutoReady off во время теста → on → продолжение

## 5. Cleanup

- [ ] 5.1 Удалить `IsInScanningPhaseUnsafe` (заменён на `_phase == Active`)
- [ ] 5.2 Обновить XML-комментарии
- [ ] 5.3 Проверить что публичный API не изменился

## Инварианты для проверки

После каждого изменения убедиться:

| Инвариант | Как проверить |
|-----------|---------------|
| Нет невалидных состояний | `_phase` только 3 значения |
| CTS правильно Dispose | Нет утечек в StartMainLoop |
| Dispose потокобезопасен | Lock вокруг _disposed |
| wasInScanPhase корректен | `_phase == Active` перед reset |
| Loop и pipeline синхронизированы | Оба CTS отменяются при reset |