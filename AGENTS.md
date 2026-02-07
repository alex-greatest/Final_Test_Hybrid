# Final_Test_Hybrid

## Важность самообучения и совершенстования
Надо записывать инфу после внесениях правок в LEARNING LOG, что сделал,
чтобы отслеживать процесс и поддерживать контекст постоянно, важные замечания и компромиссы в проект,
где ошибки допустил и т.п. чтобы обучаться и не допускать ошибки - совершенстоваваться
- Кратко (2026-02-07): оптимизировали LEARNING LOG — активный короткий лог + архив `Final_Test_Hybrid/LEARNING_LOG_ARCHIVE.md`.
- Обязательно: следить за размером `Final_Test_Hybrid/LEARNING_LOG.md`; при росте переносить старые записи в архив.

## Важность критики
Подвергай сомнению все предположения, ставь под вопрос логику, выявляй слабые места и слепые зоны
Указывай на слабую логику, самообман, отговорки, мелкое мышление, недооценку рисков
Никакого смягчения, лести, пустых похвал или расплывчатых советов
Давай жёсткие факты, стратегический анализ и точные планы действий
Ставь рост выше комфорта
Читай между строк
Всегда возражай.
Ничего не скрывай.

> **SCADA-система промышленных тестов. От кода зависят жизни — думай дважды, проверяй трижды.**

## Обязательное правило проверки

- Прежде чем спорить с существующим планом/документацией или менять логику — **сначала перечитать** релевантные документы проекта (например, `plan-refactor-executor.md`, `Docs/*Guide.md`) и найти уже зафиксированные инциденты/решения.
- Прежде чем выдвигать гипотезы и тем более настаивать на них — **проверить в коде** (поиск определений, семантика полей, реальные условия) и держать “здоровое сомнение” как default.
- Если документ/код противоречит гипотезе — первично перепроверить и скорректировать гипотезу, а не “продавить” мнение.
- **Запрет:** не использовать `ColumnExecutor.IsVisible` как критерий idle/готовности к следующему Map (см. `plan-refactor-executor.md` — инцидент зависания между Map).

## Stack

| Компонент | Технология |
|-----------|------------|
| Framework | .NET 10, WinForms + Blazor Hybrid |
| UI | Radzen Blazor 8.3 |
| OPC-UA | OPCFoundation 1.5 |
| Modbus | NModbus 3.0 |
| Database | PostgreSQL + EF Core 10 |
| Logging | Serilog + DualLogger |
| Excel | EPPlus 8.3 |

**Build:** `dotnet build && dotnet run`
**Обязательная проверка:** запускать `dotnet build`.

## Архитектура

```
Program.cs → Form1.cs (DI) → BlazorWebView → Radzen UI

[Barcode] → PreExecutionCoordinator → TestExecutionCoordinator → [OK/NOK]
                    │                           │
            ScanSteps (pre-exec)         4 × ColumnExecutor
                    ↓                           ↓
            StartTestExecution()        OnSequenceCompleted
```

## Ключевые паттерны

### DualLogger (обязательно)
```csharp
public class MyService(DualLogger<MyService> logger)
{
    logger.LogInformation("msg"); // → файл + UI теста
}
```

### Pausable Decorators
| Контекст | Сервис |
|----------|--------|
| Тестовые шаги (OPC-UA) | `PausableOpcUaTagService`, `PausableTagWaiter` |
| Тестовые шаги (Modbus) | `PausableRegisterReader/Writer` |
| Системные операции | `OpcUaTagService`, `RegisterReader/Writer` (НЕ паузятся) |

**В шагах:** `context.DelayAsync()`, `context.DiagReader/Writer` — паузятся автоматически.

### Primary Constructors
```csharp
public class MyStep(DualLogger<MyStep> _logger, IOpcUaTagService _tags) : ITestStep
```

## Правила кодирования

- ***простота, без не нужных усложнений.***
- ***чистый простой и понятный код. минимум defense programm***
- ***никакого оверинжиниринг***
- **Один** `if`/`for`/`while`/`switch`/`try` на метод (guard clauses OK) ***метод ~50 строк не больше***
- Если метод начинает разрастаться или требует больше одной основной управляющей конструкции — **сразу** упрощать: выносить ветки в `private` helper-методы (в том же partial), не откладывая на потом.
- `var` везде, `{}` обязательны, **max 300 строк** сервисы  → partial classes
- **PascalCase:** типы, методы | **camelCase:** локальные, параметры
- Предпочитай `switch`, тернарный оператор и `LINQ`, когда это уместно

## Язык и кодировка

- Документация и комментарии в новых/переименованных файлах — на русском языке.
- Файлы сохранять в `UTF-8` (если файл уже с BOM — сохранять с BOM). Не использовать ANSI/1251.
- После сохранения проверять отсутствие «кракозябр» (типичный признак неверной перекодировки UTF-8/ANSI).
- Если в тексте появились строки вида `РџР.../РѕР...` — файл почти наверняка открыли/сохранили не в той кодировке: пересохранить в `UTF-8` (для Windows-инструментов надёжнее `UTF-8 with BOM`).

### Что НЕ нужно
| Паттерн | Когда не нужен |
|---------|----------------|
| `IDisposable`, блокировки | Singleton без конкуренции |
| `CancellationToken`, retry | Короткие операции (<2 сек) |
| null-проверки DI | Внутренний код |

**Проверки НУЖНЫ:** границы системы, внешний ввод, десериализация.

## XML-документация

- Приватные: только `<summary>`
- Публичные: `<summary>`, `<param>`, `<returns>`, `<exception>`

## Документация

| Тема | Файл |
|------|------|
| State Management | [Docs/StateManagementGuide.md](Final_Test_Hybrid/Docs/StateManagementGuide.md) |
| Error Handling | [Docs/ErrorCoordinatorGuide.md](Final_Test_Hybrid/Docs/ErrorCoordinatorGuide.md) |
| PLC Reset | [Docs/PlcResetGuide.md](Final_Test_Hybrid/Docs/PlcResetGuide.md) |
| Steps | [Docs/StepsGuide.md](Final_Test_Hybrid/Docs/StepsGuide.md) |
| Cancellation | [Docs/CancellationGuide.md](Final_Test_Hybrid/Docs/CancellationGuide.md) |
| Modbus | [Docs/DiagnosticGuide.md](Final_Test_Hybrid/Docs/DiagnosticGuide.md) |
| TagWaiter | [Docs/TagWaiterGuide.md](Final_Test_Hybrid/Docs/TagWaiterGuide.md) |
| Scanner | [Docs/ScannerGuide.md](Final_Test_Hybrid/Docs/ScannerGuide.md) |

**Детальная документация:** [Final_Test_Hybrid/CLAUDE.md](Final_Test_Hybrid/CLAUDE.md)

## Практики устойчивого кода (обязательно)

- Для lifecycle/cleanup критические операции освобождения (Release*, Dispose*, сброс флагов) выполнять в finally.
- Публичные события (On...) не вызывать напрямую в критических путях: использовать safe-обёртки Notify...Safely()/Invoke...Safe() с логированием исключений.
- Для reconnect использовать только ограниченный retry (2-3 попытки, 200-500 ms задержка) и только для transient OPC ошибок.
- Перед повторной записью/ожиданием PLC-тегов после reconnect сначала дождаться connectionState.WaitForConnectionAsync(ct).
- В reset-flow ожидание reconnect через `WaitForConnectionAsync` не должно быть бесконечным: ограничивать окно reconnect через `OpcUa:ResetFlowTimeouts:ReconnectWaitTimeoutSec` и остаток `ResetHardTimeoutSec`.
- Ошибки записи критичных PLC-тегов (Reset, AskRepeat, End) не проглатывать: либо throw, либо явный fallback с логом.
- При rebind/reset подписок обязательно инвалидировать кэш значений и не сохранять Bad quality значения в runtime-кэш.
- При ошибке добавления runtime monitored item (`AddTagAsync`/`ApplyChangesAsync`) выполнять rollback (`_monitoredItems`, `_values`, callbacks), чтобы не оставлять stale-состояние.
- Для `TagWaiter.WaitForFalseAsync` первичную проверку cache выполнять через `subscription.GetValue(nodeId)` + `is bool`, а не через `GetValue<bool>()` (чтобы исключить фейковый `default(false)` при пустом cache после reconnect).
- При не-`OperationCanceledException` в фоновом retry (`ExecuteRetryInBackgroundAsync`) использовать fail-fast путь в `HardReset` и дополнительно делать fail-safe `OpenGate()`, чтобы не оставлять колонку в вечной блокировке.
- В completion-flow исключения `SaveAsync` не должны останавливать main loop: переводить их в failed-save (`SaveResult.Fail`) и продолжать retry/dialog loop.
- Для `PlcResetCoordinator.PlcHardResetPending` сброс в `0` обязателен в `finally` вокруг `_errorCoordinator.Reset()`.
- Если метод разрастается по условиям/веткам, сразу выносить ветви в private helper-методы в том же partial-файле.

## Периодичность проверок

- После каждого значимого изменения в логике (reconnect/reset/error flow) запускать минимум dotnet build.
- Перед сдачей изменений обязательно выполнять полный чек-лист верификации.

## Обязательный чек-лист верификации

1. dotnet build Final_Test_Hybrid.slnx — успешно.
2. dotnet format analyzers --verify-no-changes — чисто.
3. dotnet format style --verify-no-changes — чисто.

## Зафиксированные проектные решения (не переобсуждаем без нового инцидента)

- `PLC reset` и `HardReset` — разные потоки:
  `PLC reset` (через `Req_Reset`/`PlcResetCoordinator`) работает по цепочке с `AskEnd`,
  `HardReset` (через `ErrorCoordinator.OnReset` / `ExecutionStopReason.PlcHardReset`) обрабатывается немедленно и не ждёт `AskEnd`.
- В `PreExecutionCoordinator.ExecuteCycleAsync` при гонке `testCompletion` vs `reset` приоритет у reset (если reset уже активен/сигнал сброса уже выставлен).
- Для soft-reset очистка `UI/Boiler` не выполняется заранее: очистка должна идти по текущему `AskEnd`-пути.
- Логика таймера переналадки не меняется при фиксе reset/reconnect; любые правки reset не должны ломать существующий changeover-flow.
- Для runtime OPC-подписок при reconnect использовать только полный rebuild (`новая Session + RecreateForSessionAsync`), без гибридного ручного rebind.
- Спиннер `Выполняется подписка` показывать только при фактическом старте реальных подписок (после готовности соединения), а не на фазе retry/reconnect попыток.
- Для `Coms/Check_Comms` (`CheckCommsStep`) при `AutoReady = false` шаг должен завершаться `NoDiagnosticConnection` (fail-fast по результату шага), а при неуспешном завершении шага `IModbusDispatcher` должен останавливаться (`StopAsync`), чтобы не оставлять reconnect в фоне. Показ диалога резолюции при `AutoReady OFF` может быть отложен до восстановления автомата (`AutoReady = true`) — это допустимое поведение. Пропуск этого шага недопустим (`INonSkippable`); `Retry` имеет смысл только после восстановления `AutoReady`.
- Fail-результат шага в execution-flow должен фиксироваться в `ColumnExecutor` **до** `pauseToken.WaitWhilePausedAsync`; иначе ошибка может «застрять» до Resume и не попасть вовремя в error-queue.
- Для non-PLC шагов запись `BaseTags.Fault` (`true/false`) обязательна с bounded retry (до 3 попыток, 250 мс). Если запись Fault не удалась после retry — fail-fast в `HardReset` (`_errorCoordinator.Reset()` + остановка текущего прогона).
- Для стартовой подписки execution-шагов использовать `IRequiresPlcSubscriptions` (интерфейс наследует `IRequiresPlcTags`): шаги только с `IRequiresPlcTags` в runtime-подписку не попадают.
- `IRequiresPlcTags` оставлять как базовый/валидационный контракт для pre-execution шагов (например `BlockBoilerAdapterStep`), без обязательной подписки monitored items при старте.
- `BaseTags.AskEnd` считать системным preload-тегом: добавлять в стартовые системные подписки (`ErrorPlcMonitor.ValidateTagsAsync`), а не оставлять только как on-demand подписку первого reset.
- Для `PlcResetCoordinator` таймауты reset-flow берутся из `OpcUa:ResetFlowTimeouts`:
  `AskEndTimeoutSec` (ожидание AskEnd), `ReconnectWaitTimeoutSec` (одно ожидание reconnect), `ResetHardTimeoutSec` (общий дедлайн).
  `ResetHardTimeoutSec` должен быть `>=` двух остальных; по таймауту — `TagTimeout` + `OnResetCompleted`.
- В pre-execution `ErrorResolution.ConnectionLost` маппится в `PreExecutionResolution.HardReset` (не в `Timeout`).
- В execution-flow не-`OperationCanceledException` внутри фонового retry трактуется как критическая ошибка и переводит систему в `HardReset`.
- В completion-flow ошибка `SaveAsync` трактуется как recoverable save-failure (через retry/dialog), а не как причина падения main loop.
- В pre-execution при `OpcUaConnectionState.IsConnected = false` блокировать **оба канала** barcode:
  `BoilerInfo` (ручной ввод read-only) и `BarcodeDebounceHandler` (игнор скана). Фикс только в UI без гейтинга в pipeline недопустим.
- В pre-execution при `PreExecutionCoordinator.IsAcceptingInput = true` и `OpcUaConnectionState.IsConnected = false`
  Scan-таймер должен быть на паузе; возобновление допустимо только после восстановления связи и только при активном scan-mode (без reset-фазы).
- При чтении/проверке текстовых файлов через CLI явно использовать UTF-8 (`Get-Content -Encoding UTF8`), чтобы не принимать артефакты декодирования за порчу файла.
- Если в изменяемом файле найден текст вида `РџР.../РѕР...`, это дефект: исправлять в том же изменении и проверять файл повторным чтением в UTF-8.

## Компромиссы (временные)

- **Пункт 2 (кодировка):** временно допускаем наличие локальных «кракозябр» в части исторически изменённых комментариев/логов. Отдельная задача на централизованную нормализацию кодировки в UTF-8 (без смешения ANSI/UTF-8) запланирована отдельно.
- **Пункт 5 (UI-сообщение по PlcConnectionLost):** отдельное правило `CurrentInterrupt == InterruptReason.PlcConnectionLost` в `MessageService` пока не добавляем. Пользовательский текст остаётся через текущие правила (`OpcUaConnectionState.IsConnected`, `TagTimeout`, `ResetActive`) как согласованный временный компромисс.
- **Пункт 6 (OPC reconnect):** для runtime-подписок используем только полный rebuild (`новая Session + RecreateForSessionAsync`), без гибридного `SessionReconnectHandler + ручной rebind`. Это временно зафиксированный компромисс для исключения дублей monitored items.
- **PLC reset vs HardReset (док-фиксация):** различать два независимых потока.  
  `PlcResetCoordinator` (по `Req_Reset`) работает через `OnForceStop` и сценарий с `AskEnd`.  
  `HardReset` (через `ErrorCoordinator.OnReset` / `ExecutionStopReason.PlcHardReset`) обрабатывается немедленно и не должен зависеть от ожидания `AskEnd`.
