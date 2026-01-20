# StepTimingGuide

## Обзор

`StepTimingService` управляет таймерами для отображения времени выполнения шагов в UI.

## Два типа таймеров

| Таймер | Назначение | Отображение |
|--------|------------|-------------|
| **Scan** | Время ожидания сканирования штрихкода | Колонка "Scan" |
| **Columns** | Время выполнения шагов по колонкам | Колонки 1-4 |

## TimingState

Каждый таймер использует `TimingState` с тремя состояниями:

| Свойство | Описание |
|----------|----------|
| `IsActive` | Таймер инициализирован (Name != null) |
| `IsRunning` | Таймер активно тикает (не на паузе) |
| `Elapsed` | Накопленное время |

## Scan Timing - Жизненный цикл

```
[Активация режима сканирования]
    ↓
StartScanTiming() → IsActive=true, IsRunning=true
    ↓
[Ошибка scan step] → IsRunning=true (StopScanTiming НЕ вызывается)
    ↓
ResetScanTiming() → НИЧЕГО (таймер продолжает тикать)
    ↓
[Успешный scan step]
    ↓
StopScanTiming() → IsRunning=false (пауза)
    ↓
[Следующее сканирование]
    ↓
ResetScanTiming() → Reset (сброс и старт заново)
```

## Ключевое поведение ResetScanTiming

```csharp
public void ResetScanTiming()
{
    // Если таймер уже работает - ничего не делаем
    // Это важно для сценария ошибки scan step
    if (_scanState.IsRunning) return;

    // Иначе сбрасываем и запускаем заново
    _scanState.Reset();
}
```

**Почему так:**
- При ошибке scan step `StopScanTiming()` не вызывается
- Таймер должен продолжать тикать до успешного завершения
- `ResetScanTiming()` вызывается в `SetAcceptingInput(true)` при каждом цикле
- Без проверки `IsRunning` таймер сбрасывался бы при каждой ошибке

## Сценарии

| Сценарий | IsRunning до | Действие ResetScanTiming | Результат |
|----------|--------------|--------------------------|-----------|
| Ошибка scan step | true | Ничего | Таймер продолжает |
| Успех scan step | false | Reset | Таймер сбрасывается |
| После Clear | false | Start | Новый таймер |

## Глобальная пауза

При глобальной паузе (`PauseAllTiming`):
- Все активные таймеры ставятся на паузу
- `_scanPausedByGlobalPauseId` запоминает ID паузы
- При `ResumeAllTiming` таймеры возобновляются

## Методы API

### Scan Timing

| Метод | Когда вызывать |
|-------|----------------|
| `StartScanTiming(name, desc)` | Активация режима сканирования |
| `StopScanTiming()` | Успешное завершение scan step |
| `ResetScanTiming()` | Готовность к новому сканированию |
| `ClearScanTiming()` | Завершение теста / выход из режима |

### Column Timing

| Метод | Когда вызывать |
|-------|----------------|
| `StartColumnTiming(col, name, desc)` | Начало шага в колонке |
| `StopColumnTiming(col)` | Завершение шага в колонке |
| `ClearColumnTiming(col)` | Очистка колонки |

### Global

| Метод | Когда вызывать |
|-------|----------------|
| `PauseAllTiming(pauseId)` | Глобальная пауза (ошибка, прерывание) |
| `ResumeAllTiming(pauseId)` | Возобновление после паузы |
| `Clear()` | Полная очистка всех таймеров |
