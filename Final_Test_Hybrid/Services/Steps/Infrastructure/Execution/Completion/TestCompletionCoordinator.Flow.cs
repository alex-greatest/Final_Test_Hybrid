using System.Globalization;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.Storage;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

public partial class TestCompletionCoordinator
{
    private const string FinalResultName = "Final_result";
    private const string TestingDateName = "Testing_date";
    private const string TestingDateFormat = "dd.MM.yyyy HH:mm:ss";

    public async Task<CompletionResult> HandleTestCompletedAsync(int testResult, CancellationToken ct)
    {
        IsWaitingForCompletion = true;
        try
        {
            // 1. Записать End = true
            var written = await TryWriteTagAsync(BaseTags.ErrorSkip, true, "End = true", ct);
            if (!written)
            {
                return CompletionResult.Cancelled;
            }
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
            // 3. Ждать End = false (PLC сбросит) — может быть прервано Reset
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
        catch (OperationCanceledException)
        {
            logger.LogInformation("Ожидание End прервано");
            return CompletionResult.Cancelled;
        }
        finally
        {
            IsWaitingForCompletion = false;
        }
    }

    private async Task<CompletionResult> HandleRepeatAsync(int testResult, CancellationToken ct)
    {
        logger.LogInformation("Запрошен повтор теста (result={Result})", testResult);

        // NOK (testResult == 2) - сохранение + ReworkDialog + полная подготовка
        if (testResult == 2)
        {
            return await HandleNokRepeatAsync(ct);
        }
        // OK - просто сигнал повтора (без сохранения)
        var written = await TryWriteTagAsync(BaseTags.AskRepeat, true, "AskRepeat = true", ct);
        return written ? CompletionResult.RepeatRequested : CompletionResult.Cancelled;
    }

    private async Task<CompletionResult> HandleFinishAsync(int testResult, CancellationToken ct)
    {
        logger.LogInformation("Завершение теста (result={Result})", testResult);

        // Сохранить с retry loop
        var saved = await TrySaveWithRetryAsync(testResult, ct);
        return !saved ? CompletionResult.Cancelled : CompletionResult.Finished;
    }

    private async Task<bool> TrySaveWithRetryAsync(int testResult, CancellationToken ct)
    {
        AddCompletionResults(testResult);
        while (!ct.IsCancellationRequested)
        {
            InvokeSaveProgressSafely(true);
            SaveResult result;
            try
            {
                result = await deps.Storage.SaveAsync(testResult, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Исключение SaveAsync при сохранении результата теста");
                result = SaveResult.Fail(ex.Message);
            }
            finally
            {
                InvokeSaveProgressSafely(false);
            }

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

    private void AddCompletionResults(int testResult)
    {
        var status = testResult == 1 ? 1 : 2;
        var finalResultValue = testResult == 1 ? "OK" : "NOK";
        var testingDateValue = DateTime.Now.ToString(TestingDateFormat, CultureInfo.InvariantCulture);

        deps.TestResultsService.Remove(FinalResultName);
        deps.TestResultsService.Remove(TestingDateName);

        deps.TestResultsService.Add(
            parameterName: FinalResultName,
            value: finalResultValue,
            min: "",
            max: "",
            status: status,
            isRanged: false,
            unit: "",
            test: "Test Completion");
        deps.TestResultsService.Add(
            parameterName: TestingDateName,
            value: testingDateValue,
            min: "",
            max: "",
            status: 1,
            isRanged: false,
            unit: "",
            test: "Test Completion");
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

    private async Task<bool> TryWriteTagAsync(string nodeId, bool value, string operation, CancellationToken ct)
    {
        var result = await deps.PlcService.WriteAsync(nodeId, value, ct);
        if (result.Success)
        {
            return true;
        }

        logger.LogError("Не удалось выполнить {Operation}: {Error}", operation, result.Error);
        return false;
    }
}
