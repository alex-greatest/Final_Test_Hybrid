# Final_Test_Hybrid

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
- `var` везде, `{}` обязательны, **max 300 строк** сервисы  → partial classes
- **PascalCase:** типы, методы | **camelCase:** локальные, параметры
- Предпочитай `switch` и тернарный оператор где разумно

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

**Детальная документация:** [Final_Test_Hybrid/CLAUDE.md](Final_Test_Hybrid/CLAUDE.md)
