using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
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

    public async Task ExecuteMapAsync(TestMap map, CancellationToken ct)
    {
        foreach (var row in map.Rows.TakeWhile(_ => !ct.IsCancellationRequested && !HasFailed))
        {
            await ExecuteRowStep(row, ct);
        }
        ClearStatus();
    }

    private async Task ExecuteRowStep(TestMapRow row, CancellationToken ct)
    {
        var step = row.Steps[ColumnIndex];
        if (step == null)
        {
            SetStatus("Пропуск");
            return;
        }
        await ExecuteStep(step, ct);
    }

    private async Task ExecuteStep(Interaces.ITestStep step, CancellationToken ct)
    {
        SetRunning(step);
        try
        {
            var result = await step.ExecuteAsync(context, ct);
            HandleResult(step, result);
        }
        catch (OperationCanceledException)
        {
            ClearStatus();
        }
        catch (Exception ex)
        {
            SetError(step, ex.Message);
            logger.LogError(ex, "Исключение в шаге {Step} колонки {Col}", step.Name, ColumnIndex);
            testLogger.LogError(ex, "Исключение в шаге '{Step}': {Message}", step.Name, ex.Message);
        }
    }

    private void SetRunning(Interaces.ITestStep step)
    {
        CurrentStepName = step.Name;
        CurrentStepDescription = step.Description;
        ResultValue = null;
        testLogger.LogStepStart(step.Name);
        SetStatus("Выполняется");
    }

    private void HandleResult(Interaces.ITestStep step, TestStepResult result)
    {
        if (!result.Success)
        {
            SetError(step, result.Message);
            return;
        }
        ResultValue = result.Message;
        var statusText = result.Skipped ? "Пропуск" : "Готово";
        testLogger.LogStepEnd(step.Name);
        if (!string.IsNullOrEmpty(result.Message))
        {
            testLogger.LogInformation("  Результат: {Message}", result.Message);
        }
        SetStatus(statusText);
    }

    private void SetError(Interaces.ITestStep step, string message)
    {
        logger.LogError("Шаг {Step} в колонке {Col}: {Error}", step.Name, ColumnIndex, message);
        testLogger.LogError(null, "ОШИБКА в шаге '{Step}': {Message}", step.Name, message);
        ErrorMessage = message;
        HasFailed = true;
        SetStatus("Ошибка");
    }

    private void SetStatus(string status)
    {
        Status = status;
        OnStateChanged?.Invoke();
    }

    private void ClearStatus()
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
