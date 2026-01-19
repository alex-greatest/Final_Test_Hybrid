# Tasks: Fix Scan Timing Loss

## 1. Fix StopScanTiming Method

- [ ] 1.1 Изменить `StopScanTiming()` в `StepTimingService.Scan.cs`:
  - Заменить проверку `IsRunning` на `IsActive`
  - Добавить сохранение времени в `_records` через `Insert(0, ...)`
  - Заменить `Stop()` на `Clear()` для полной очистки state

## 2. Verification

- [ ] 2.1 Запустить тест и проверить:
  - Время scan step отображается во время сканирования
  - Время scan step сохраняется после завершения ScanStep
  - Время остаётся видимым при выполнении следующих шагов
  - Время очищается при завершении теста (ClearForTestCompletion)
