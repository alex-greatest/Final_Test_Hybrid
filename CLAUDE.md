<!-- OPENSPEC:START -->
# OpenSpec Instructions

These instructions are for AI assistants working in this project.

Always open `@/openspec/AGENTS.md` when the request:
- Mentions planning or proposals (words like proposal, spec, change, plan)
- Introduces new capabilities, breaking changes, architecture shifts, or big performance/security work
- Sounds ambiguous and you need the authoritative spec before coding

Use `@/openspec/AGENTS.md` to learn:
- How to create and apply change proposals
- Spec format and conventions
- Project structure and guidelines

Keep this managed block so 'openspec update' can refresh the instructions.

<!-- OPENSPEC:END -->

# Final_Test_Hybrid

> **SCADA-система промышленных тестов. От кода зависят жизни — думай дважды, проверяй трижды.**

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

- **Один** `if`/`for`/`while`/`switch`/`try` на метод (guard clauses OK)
- `var` везде, `{}` обязательны, **max 300 строк** → partial classes
- **PascalCase:** типы, методы | **camelCase:** локальные, параметры
- Предпочитай `switch` и тернарный оператор где разумно

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
