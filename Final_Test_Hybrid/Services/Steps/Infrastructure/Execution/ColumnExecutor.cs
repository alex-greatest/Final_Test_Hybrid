using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class ColumnExecutor(
    int columnIndex,
    TestStepContext context,
    ITestStepLogger testLogger,
    ILogger logger)
{
    public int ColumnIndex { get; } = columnIndex;
    public string? CurrentStepName { get; private set; }
    public string? CurrentStepDescription { get; private set; }
    public string? Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ResultValue { get; private set; }
    public bool HasFailed { get; private set; }
    public bool IsVisible => Status != null;

    public event Action? OnStateChanged;

    public async Task ExecuteMapAsync(TestMap map, CancellationToken cancellationToken)
    {
        foreach (var row in map.Rows.TakeWhile(row => !ShouldStopExecution(cancellationToken)))
        {
            await ExecuteRowStep(row, cancellationToken);
        }

        ClearStatusIfNotFailed();
    }

    private bool ShouldStopExecution(CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested || HasFailed;
    }

    private async Task ExecuteRowStep(TestMapRow row, CancellationToken cancellationToken)
    {
        var step = row.Steps[ColumnIndex];

        if (step == null)
        {
            SetStatus("Пропуск");
            return;
        }

        await ExecuteStep(step, cancellationToken);
    }

    private async Task ExecuteStep(ITestStep step, CancellationToken cancellationToken)
    {
        SetRunningState(step);
        try
        {
            var result = await step.ExecuteAsync(context, cancellationToken);
            ProcessStepResult(step, result);
        }
        catch (OperationCanceledException)
        {
            ClearStatusIfNotFailed();
        }
        catch (Exception exception)
        {
            HandleStepException(step, exception);
        }
    }

    private void SetRunningState(ITestStep step)
    {
        CurrentStepName = step.Name;
        CurrentStepDescription = step.Description;
        ResultValue = null;
        testLogger.LogStepStart(step.Name);
        SetStatus("Выполняется");
    }

    private void ProcessStepResult(ITestStep step, TestStepResult result)
    {
        if (!result.Success)
        {
            SetErrorState(step, result.Message);
            return;
        }
        SetSuccessState(step, result);
    }

    private void SetSuccessState(ITestStep step, TestStepResult result)
    {
        ResultValue = result.Message;
        var statusText = DetermineSuccessStatusText(result);
        testLogger.LogStepEnd(step.Name);
        LogResultMessageIfPresent(result.Message);
        SetStatus(statusText);
    }

    private static string DetermineSuccessStatusText(TestStepResult result)
    {
        return result.Skipped ? "Пропуск" : "Готово";
    }

    private void LogResultMessageIfPresent(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            testLogger.LogInformation("  Результат: {Message}", message);
        }
    }

    private void HandleStepException(ITestStep step, Exception exception)
    {
        SetErrorState(step, exception.Message);
        LogException(step, exception);
    }

    private void LogException(ITestStep step, Exception exception)
    {
        logger.LogError(exception, "Исключение в шаге {Step} колонки {Col}", step.Name, ColumnIndex);
        testLogger.LogError(exception, "Исключение в шаге '{Step}': {Message}", step.Name, exception.Message);
    }

    private void SetErrorState(ITestStep step, string message)
    {
        LogStepError(step, message);
        ErrorMessage = message;
        HasFailed = true;
        SetStatus("Ошибка");
    }

    private void LogStepError(ITestStep step, string message)
    {
        logger.LogError("Шаг {Step} в колонке {Col}: {Error}", step.Name, ColumnIndex, message);
        testLogger.LogError(null, "ОШИБКА в шаге '{Step}': {Message}", step.Name, message);
    }

    private void SetStatus(string status)
    {
        Status = status;
        OnStateChanged?.Invoke();
    }

    private void ClearStatusIfNotFailed()
    {
        if (HasFailed)
        {
            return;
        }
        Status = null;
        OnStateChanged?.Invoke();
    }

    public void Reset()
    {
        CurrentStepName = null;
        CurrentStepDescription = null;
        Status = null;
        ErrorMessage = null;
        ResultValue = null;
        HasFailed = false;
        context.Variables.Clear();
    }
}
