# Proposal: Разделение ErrorCoordinator на partial classes

## Summary
Разбить `ErrorCoordinator.cs` (400 строк) на partial classes по логическим блокам, соблюдая лимит 300 строк на файл.

## Motivation
- Файл превышает установленный лимит 300 строк
- Регионы чётко отделяют логические блоки — это готовые кандидаты для partial classes
- Улучшение навигации и maintainability

## Scope

### In Scope
- Разделение `ErrorCoordinator.cs` на partial classes
- Сохранение всей логики и API без изменений

### Out of Scope
- Изменение поведения
- Рефакторинг логики
- Изменение dependencies

## Approach

Разделить на 3 файла по логическим группам:

| Файл | Содержимое | ~Строк |
|------|-----------|--------|
| `ErrorCoordinator.cs` | Поля, конструктор, events, Event Subscriptions, Disposal, Helpers | ~130 |
| `ErrorCoordinator.Interrupts.cs` | Interrupt Handling (HandleInterruptAsync, ProcessInterruptAsync, locks) | ~80 |
| `ErrorCoordinator.Resolution.cs` | Error Resolution (WaitForResolution, SendAskRepeat), Reset and Recovery | ~150 |

### Rationale

1. **Event Subscriptions** остаются в основном файле — тесно связаны с конструктором и disposal
2. **Interrupt Handling** — самостоятельный блок с lock-логикой
3. **Error Resolution + Reset/Recovery** — объединены, так как Reset тесно связан с resolution flow

## Impact

- **API:** Без изменений
- **Behavior:** Без изменений
- **Files:** 1 файл → 3 файла (partial classes)

## Alternatives Considered

1. **Оставить как есть** — нарушает лимит 300 строк
2. **4+ файла** — избыточное дробление для класса с 400 строками