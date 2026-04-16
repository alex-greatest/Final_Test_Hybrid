# StepTimingGuide

## Обзор

`StepTimingService` управляет таймерами для отображения времени выполнения шагов в UI.

## Граница ответственности

`StepTimingService` не является владельцем всех runtime-таймеров системы.

Жёсткий контракт:

| Контур | Source of truth | Что запрещено смешивать |
|--------|-----------------|-------------------------|
| Scan/step timing UI | `StepTimingService` | Нельзя трактовать как `TestTime` или `ChangeoverTime` |
| `TestTime` | `BoilerState` | Нельзя перезапускать через scanner ownership / `BoilerInfo` rearm |
| `ChangeoverTime` | `BoilerState` + `PreExecutionCoordinator` changeover flow | Нельзя менять через scan-mode, reset rearm или `StepTimingService` |

Следствия:
- правки scanner ownership, `BoilerInfo` gating и ordinary owner rearm не должны вызывать `BoilerState.Start/Stop/Reset` для `TestTime` и `ChangeoverTime`;
- возврат ordinary scanner-ready после `reset -> repeat -> success` не должен менять semantics test/changeover timers;
- `StepTimingService` допустимо использовать только для scan/step lifecycle и их pause/resume в runtime-gating.

## Два типа таймеров

| Таймер | Назначение | Отображение |
|--------|------------|-------------|
| **Scan** | Время ожидания сканирования штрихкода | Колонка "Scan" |
| **Columns** | Время выполнения шагов по колонкам | Колонки 1-4 |

## Форматы времени (важно)

| Контур | Формат | Примечание |
|--------|--------|------------|
| `ActiveTimersGrid` (`TimerService`) | `HH:mm:ss` | UI-отображение активных таймеров |
| Runtime-результаты `Timer_1` / `Timer_2` | `HH:mm:ss` | Уходит в `TB_RESULT` и MES `Items` |
| `StepTimingService` (`StepTimingRecord.Duration`) | `mm.ss` | Используется для MES `time[]` и `TB_STEP_TIME.DURATION` |

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

## Пауза Scan при неготовности входа в ожидании ввода

Если в pre-execution одновременно:
- `PreExecutionCoordinator.IsAcceptingInput = true`
- `OpcUaConnectionState.IsConnected = false` **или** `ScanModeController.IsScanModeEnabled = false` (например, `AskAuto = false`)

то Scan-таймер должен быть на паузе до восстановления входной готовности.

Возобновление допускается только когда одновременно восстановлены:
- PLC-связь (`IsConnected = true`)
- scan-mode (`IsScanModeEnabled = true`)
- контроллер не в reset и уже активирован

Это касается только Scan-таймера `StepTimingService`.
`TestTime` и `ChangeoverTime` этим pause/resume-контуром не управляются.

## Пауза таймеров на диалоге причины прерывания

Во время активного `InterruptReasonDialog` (после `AskEnd` в reset-сценарии):
- Scan и column timers принудительно остаются на паузе.
- Любая попытка restart scan timing из `OnResetCompleted` блокируется.
- В логах фиксируется `InterruptDialogTimingFreezeApplied` и `ScanTimingRestartBlockedByInterruptDialog`.

После закрытия актуального окна:
- pre-execution возвращается в ожидание barcode;
- при `SetAcceptingInput(true)` выполняется `ResetScanTiming()` (сброс и старт scan-таймера с нуля);
- stale reset-seq не должен запускать таймер.
- аварийный `RepeatBypass` из repeat-save flow не вводит отдельного timing-контурa:
  он использует existing repeat outcome через `StartRepeatAfterReset(...)` и не должен добавлять новые вызовы в `StepTimingService`.

## Глобальная пауза

При глобальной паузе (`PauseAllColumnsTiming`):
- Все активные таймеры ставятся на паузу
- При `ResumeAllColumnsTiming` таймеры возобновляются

Контракт regression-guard:
- `PauseAllColumnsTiming` / `ResumeAllColumnsTiming` / `ResetScanTiming` нельзя использовать как скрытый способ менять `BoilerState` timers;
- если правка касается scanner unlock, `BoilerInfo` unlock или reset/repeat rearm, нужно отдельно доказать, что `StepTimingService` по-прежнему не влияет на `TestTime` и `ChangeoverTime`.

## Методы API

### Completed Timing

- Для мгновенно завершающихся шагов допускается completed-запись без active timer lifecycle.
- Такой шаг сразу попадает в `_records` как завершённый `StepTimingRecord` с `IsRunning = false`.
- Это поведение используется для `Misc/StartTimer1`: шаг фиксируется в `StepTimingService` с длительностью `00.00`.
- Completed-запись не должна:
  - активировать `HasActiveStep(...)`;
  - участвовать в `PauseAllColumnsTiming` / `ResumeAllColumnsTiming`;
  - менять ownership runtime-логики, которая опирается на active-step.

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
| `PauseAllColumnsTiming()` | Глобальная пауза (ошибка, прерывание) |
| `ResumeAllColumnsTiming()` | Возобновление после паузы |
| `Clear()` | Полная очистка всех таймеров |
