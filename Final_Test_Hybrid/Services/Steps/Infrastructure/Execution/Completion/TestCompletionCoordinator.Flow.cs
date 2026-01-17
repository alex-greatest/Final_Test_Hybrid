using Final_Test_Hybrid.Models.Plc.Tags;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

public partial class TestCompletionCoordinator
{
    public async Task<CompletionResult> HandleTestCompletedAsync(int testResult, CancellationToken ct)
    {
        IsWaitingForCompletion = true;
        try
        {
            // 1. Записать End = true
            await deps.PlcService.WriteAsync(BaseTags.ErrorSkip, true, ct);
            logger.LogInformation("End = true записан, ожидание сброса от PLC");

            // 2. Ждать пока подписка увидит true (запись дошла до PLC)
            var currentValue = deps.Subscription.GetValue<bool>(BaseTags.ErrorSkip);
            logger.LogInformation("Текущее значение End после записи: {Value}", currentValue);

            if (!currentValue)
            {
                logger.LogWarning("End ещё false, ждём подтверждения записи...");
                await deps.TagWaiter.WaitForTrueAsync(BaseTags.ErrorSkip, timeout: TimeSpan.FromSeconds(5), ct);
                logger.LogInformation("End = true подтверждено");
            }

            // 3. Ждать End = false (PLC сбросит)
            await deps.TagWaiter.WaitForFalseAsync(BaseTags.ErrorSkip, timeout: null, ct);
            logger.LogDebug("PLC сбросил End");

            // 3. Delay 1 секунда (даём PLC время выставить Req_Repeat)
            await Task.Delay(1000, ct);

            // 4. Читаем Req_Repeat → решение
            var reqRepeat = deps.Subscription.GetValue<bool>(BaseTags.ErrorRetry);
            logger.LogInformation("Req_Repeat = {ReqRepeat}", reqRepeat);

            if (reqRepeat)
            {
                return await HandleRepeatAsync(testResult, ct);
            }

            return await HandleFinishAsync(testResult, ct);
        }
        finally
        {
            IsWaitingForCompletion = false;
        }
    }

    private async Task<CompletionResult> HandleRepeatAsync(int testResult, CancellationToken ct)
    {
        logger.LogInformation("Запрошен повтор теста (result={Result})", testResult);

        // Записать Ask_Repeat = true (PLC сбросит Req_Repeat)
        await deps.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);

        // Для NOK: сохранение + подготовка будут в этапе 5
        return CompletionResult.RepeatRequested;
    }

    private async Task<CompletionResult> HandleFinishAsync(int testResult, CancellationToken ct)
    {
        logger.LogInformation("Завершение теста (result={Result})", testResult);

        // Сохранить с retry loop
        var saved = await TrySaveWithRetryAsync(testResult, ct);
        if (!saved)
        {
            return CompletionResult.Cancelled;
        }

        return CompletionResult.Finished;
    }

    private async Task<bool> TrySaveWithRetryAsync(int testResult, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await deps.Storage.SaveAsync(testResult, ct);
            if (result.IsSuccess)
            {
                logger.LogInformation("Результат {Status} сохранён", testResult == 1 ? "OK" : "NOK");
                return true;
            }

            logger.LogWarning("Ошибка сохранения: {Error}", result.ErrorMessage);

            // Показать диалог ошибки
            var shouldRetry = await ShowSaveErrorDialogAsync(result.ErrorMessage);
            if (!shouldRetry)
            {
                return false; // Сброс PLC отменил операцию
            }
        }
        return false;
    }

    private async Task<bool> ShowSaveErrorDialogAsync(string? errorMessage)
    {
        var handler = OnSaveErrorDialogRequested;
        if (handler == null)
        {
            logger.LogWarning("Нет подписчика на OnSaveErrorDialogRequested");
            return false;
        }

        try
        {
            return await handler(errorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка показа диалога сохранения");
            return false;
        }
    }
}
