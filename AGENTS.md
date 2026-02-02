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

# Final_Test_Hybrid (Codex Guide)

SCADA test system for industrial testing. Treat changes as safety-critical.

## Stack

| Component | Technology |
| --- | --- |
| Framework | .NET 10, WinForms + Blazor Hybrid |
| UI | Radzen Blazor 8.3 |
| OPC-UA | OPCFoundation 1.5 |
| Modbus | NModbus 3.0 |
| Database | PostgreSQL + EF Core 10 |
| Logging | Serilog + DualLogger |
| Excel | EPPlus 8.3 |

Build: `dotnet build && dotnet run`

## Architecture (high-level)

```
Program.cs -> Form1.cs (DI) -> BlazorWebView -> Radzen UI

[Barcode] -> PreExecutionCoordinator -> TestExecutionCoordinator -> [OK/NOK]
                 |                           |
         ScanSteps (pre-exec)         4 x ColumnExecutor
                 v                           v
         StartTestExecution()        OnSequenceCompleted
```

## Key Patterns

### DualLogger (required)
```csharp
public class MyService(DualLogger<MyService> logger)
{
    logger.LogInformation("msg"); // file + UI test log
}
```

### Pausable Decorators
| Context | Service |
| --- | --- |
| Test steps (OPC-UA) | `PausableOpcUaTagService`, `PausableTagWaiter` |
| Test steps (Modbus) | `PausableRegisterReader/Writer` |
| System ops | `OpcUaTagService`, `RegisterReader/Writer` (not pausable) |

In steps: use `context.DelayAsync()`, `context.DiagReader/Writer` (pause-aware).

### Primary Constructors
```csharp
public class MyStep(DualLogger<MyStep> logger, IOpcUaTagService tags) : ITestStep
```

## Coding Rules

- One `if`/`for`/`while`/`switch`/`try` per method (guard clauses OK).
- Use `var` everywhere; `{}` required; max 300 lines -> split via partial classes.
- PascalCase: types/methods. camelCase: locals/parameters.
- Prefer `switch`/ternary when reasonable.

### What is NOT required

| Pattern | When not needed |
| --- | --- |
| `IDisposable`, locks | singleton without concurrency |
| `CancellationToken`, retry | short ops (<2s) |
| null-check DI | internal code |

Checks ARE required at system boundaries, for external input, and during deserialization.

## XML Documentation

- Private: `<summary>` only.
- Public: `<summary>`, `<param>`, `<returns>`, `<exception>`.

## Docs to Read

| Topic | File |
| --- | --- |
| State Management | `Final_Test_Hybrid/Docs/StateManagementGuide.md` |
| Error Handling | `Final_Test_Hybrid/Docs/ErrorCoordinatorGuide.md` |
| PLC Reset | `Final_Test_Hybrid/Docs/PlcResetGuide.md` |
| Steps | `Final_Test_Hybrid/Docs/StepsGuide.md` |
| Cancellation | `Final_Test_Hybrid/Docs/CancellationGuide.md` |
| Modbus | `Final_Test_Hybrid/Docs/DiagnosticGuide.md` |
| TagWaiter | `Final_Test_Hybrid/Docs/TagWaiterGuide.md` |
