# NOK Repeat Flow Specification

## CHANGED Requirements

### Requirement: NOK Repeat Should Not Show Rework Dialog Immediately

Система SHALL показывать ReworkDialog только когда MES сервер явно требует rework, а НЕ сразу при NOK повторе.

#### Current Behavior (WRONG)

```
HandleNokRepeatAsync()
├─ TrySaveWithRetryAsync()       // Сохранить NOK
├─ OnReworkDialogRequested()     // ❌ СРАЗУ показать ReworkDialog
└─ WriteAsync(AskRepeat, true)   // Сигнал повтора
```

**Проблема:** ReworkDialog показывается сразу, без запроса к MES серверу.

#### Expected Behavior (CORRECT)

```
HandleNokRepeatAsync()
├─ TrySaveWithRetryAsync()       // Сохранить NOK
└─ WriteAsync(AskRepeat, true)   // Сигнал повтора
    │
    ▼
ExecuteNokRepeatPipelineAsync()
└─ ScanBarcodeMesStep.ExecuteAsync()
   └─ StartOperationAsync()      // Запрос к MES
      ├─ RequiresRework = false → Продолжить (данные получены)
      └─ RequiresRework = true → OnReworkRequired() → ReworkDialog
```

**Правильно:** ReworkDialog показывается в `ScanBarcodeMesStep` ПОСЛЕ ответа MES с `RequiresRework = true`.

#### Scenario: NOK repeat without rework required
- **WHEN** тест завершён с NOK результатом
- **AND** оператор запросил повтор (Req_Repeat = true)
- **THEN** результат сохраняется в MES
- **AND** записывается AskRepeat = true
- **AND** возвращается `NokRepeatRequested`
- **AND** ReworkDialog НЕ показывается
- **WHEN** запускается `ExecuteNokRepeatPipelineAsync`
- **AND** MES возвращает `RequiresRework = false`
- **THEN** тест продолжается с полученными данными

#### Scenario: NOK repeat with rework required
- **WHEN** тест завершён с NOK результатом
- **AND** оператор запросил повтор
- **THEN** результат сохраняется в MES
- **AND** записывается AskRepeat = true
- **WHEN** запускается `ExecuteNokRepeatPipelineAsync`
- **AND** MES возвращает `RequiresRework = true`
- **THEN** `ScanBarcodeMesStep` показывает ReworkDialog
- **AND** оператор вводит причину и получает одобрение
- **THEN** тест продолжается

## Design Decision

Убрать вызов `OnReworkDialogRequested` из `HandleNokRepeatAsync`.

ReworkDialog уже корректно реализован в `ScanBarcodeMesStep.HandleReworkFlowAsync()`:
- Принимает callback `executeRework` для MES запроса
- Показывается только когда `OperationStartResult.RequiresRework = true`
- Обрабатывает повторный запрос после одобрения rework

## Implementation

### Changes to TestCompletionCoordinator.Repeat.cs

**Before:**
```csharp
private async Task<CompletionResult> HandleNokRepeatAsync(CancellationToken ct)
{
    // 1. Сохранить NOK результат
    var saved = await TrySaveWithRetryAsync(2, ct);
    if (!saved) return CompletionResult.Cancelled;

    // 2. ReworkDialog (только MES) ← УБРАТЬ
    if (deps.AppSettings.UseMes)
    {
        var handler = OnReworkDialogRequested;
        if (handler == null) return CompletionResult.Cancelled;
        var reworkResult = await handler("NOK результат");
        if (!reworkResult.IsSuccess) return CompletionResult.Cancelled;
    }

    // 3. AskRepeat = true
    await deps.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);
    return CompletionResult.NokRepeatRequested;
}
```

**After:**
```csharp
private async Task<CompletionResult> HandleNokRepeatAsync(CancellationToken ct)
{
    logger.LogInformation("NOK повтор: начало процесса сохранения");

    // 1. Сохранить NOK результат
    var saved = await TrySaveWithRetryAsync(2, ct);
    if (!saved)
    {
        logger.LogWarning("NOK повтор: сохранение отменено");
        return CompletionResult.Cancelled;
    }

    // 2. AskRepeat = true (PLC сбросит Req_Repeat)
    // ReworkDialog будет показан в ScanBarcodeMesStep если MES потребует
    await deps.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);
    logger.LogInformation("NOK повтор: AskRepeat = true, переход к подготовке");

    return CompletionResult.NokRepeatRequested;
}
```

### Optional Cleanup

После исправления можно удалить:
1. `TestCompletionCoordinator.OnReworkDialogRequested` event — больше не используется
2. `HandleReworkForNokRepeatAsync` в `BoilerInfo.razor` — больше не используется
3. Подписку/отписку `CompletionCoordinator.OnReworkDialogRequested` в `BoilerInfo.razor`
