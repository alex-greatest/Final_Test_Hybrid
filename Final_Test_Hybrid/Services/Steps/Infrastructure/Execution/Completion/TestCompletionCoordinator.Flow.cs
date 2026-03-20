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
        _runtimeTerminalState.SetCompletionActive(true);
        try
        {
            await StopDiagnosticDispatcherSafelyAsync();

            // 1. Записать End = true
            var written = await TryWriteTagAsync(BaseTags.ErrorSkip, true, "End = true", ct);
            if (!written)
            {
                return CompletionResult.Cancelled;
            }
            logger.LogInformation("End = true записан, ожидание решения PLC");

            await deps.TagWaiter.WaitForTrueAsync(BaseTags.ErrorSkip, timeout: TimeSpan.FromSeconds(5), ct);

            var shouldRepeat = await WaitCompletionDecisionAsync(ct);
            if (shouldRepeat)
            {
                return await HandleRepeatAsync(testResult, ct);
            }

            return await HandleFinishAsync(testResult, ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Ожидание completion-handshake прервано");
            return CompletionResult.Cancelled;
        }
        finally
        {
            _runtimeTerminalState.SetCompletionActive(false);
        }
    }

    private async Task StopDiagnosticDispatcherSafelyAsync()
    {
        if (!deps.Dispatcher.IsStarted)
        {
            return;
        }

        try
        {
            await deps.Dispatcher.StopAsync();
            logger.LogInformation("Диагностическая связь остановлена перед completion-handshake");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка остановки диагностической связи перед completion-handshake");
        }
    }

    private async Task<bool> WaitCompletionDecisionAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (deps.Subscription.TryGetValue<bool>(BaseTags.ErrorRetry, out var shouldRepeat) && shouldRepeat)
            {
                logger.LogInformation("Completion: Req_Repeat = true");
                return true;
            }

            if (deps.Subscription.TryGetValue<bool>(BaseTags.ErrorSkip, out var endSignal) && !endSignal)
            {
                logger.LogInformation("Completion: End сброшен без Req_Repeat");
                return false;
            }

            await Task.Delay(200, ct);
        }

        throw new OperationCanceledException(ct);
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
