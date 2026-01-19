# Tasks: Fix CurrentBarcode Not Cleared

## Investigation (completed)

- [x] Найти все места установки CurrentBarcode
- [x] Найти все места сброса CurrentBarcode
- [x] Проанализировать все пути выхода из цикла
- [x] Выявить race conditions

## Implementation

### Phase 1: Critical Fixes

- [ ] **Fix NullReferenceException при повторе** (HIGH)
  - Файл: `PreExecutionCoordinator.MainLoop.cs:34-38`
  - Добавить null-check для CurrentBarcode при `_skipNextScan`
  - Fallback к обычному сканированию если CurrentBarcode = null

- [ ] **Сбрасывать _skipNextScan при HardReset** (HIGH)
  - Файл: `PreExecutionCoordinator.cs:50-61`
  - Добавить `_skipNextScan = false` в `ClearStateOnReset()`
  - Добавить `_executeFullPreparation = false`

### Phase 2: Edge Cases

- [ ] **Обработать CompletionResult.Cancelled** (MEDIUM)
  - Файл: `PreExecutionCoordinator.MainLoop.cs:173-179`
  - Добавить явный case для `Cancelled` → `TestCompleted`

- [ ] **Решить поведение SoftReset** (требует обсуждения)
  - Вопрос: сбрасывать CurrentBarcode сразу или только по AskEnd?
  - Файл: `PreExecutionCoordinator.MainLoop.cs:127-129`

### Phase 3: Logging & Testing

- [ ] Добавить логирование для отладки сброса CurrentBarcode
- [ ] Проверить все сценарии вручную:
  - Нормальное завершение (OK/NOK)
  - OK повтор
  - NOK повтор
  - HardReset во время теста
  - SoftReset во время теста
  - HardReset между повторами
  - Отмена сохранения
