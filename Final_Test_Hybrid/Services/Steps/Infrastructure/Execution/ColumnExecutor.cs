using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class ColumnExecutor(
    int columnIndex,
    TestStepContext context,
    ITestStepLogger testLogger,
    ILogger logger,
    StepStatusReporter statusReporter,
    PauseTokenSource pauseToken)
{
    private record StepState(
        string? Name,
        string? Description,
        string? Status,
        string? ErrorMessage,
        string? ResultValue,
        bool HasFailed,
        Guid UiStepId,
        ITestStep? FailedStep);

    private static readonly StepState EmptyState = new(null, null, null, null, null, false, Guid.Empty, null);
    private StepState _state = EmptyState;
    public int ColumnIndex { get; } = columnIndex;
    public string? CurrentStepName => _state.Name;
    public string? CurrentStepDescription => _state.Description;
    public string? Status => _state.Status;
    public string? ErrorMessage => _state.ErrorMessage;
    public string? ResultValue => _state.ResultValue;
    public bool HasFailed => _state.HasFailed;
    public bool IsVisible => _state.Status != null;
    public ITestStep? FailedStep => _state.FailedStep;
    public event Action? OnStateChanged;

    public async Task ExecuteMapAsync(TestMap map, CancellationToken ct)
    {
        var stepsToExecute = map.Rows
            .TakeWhile(_ => !ct.IsCancellationRequested && !_state.HasFailed)
            .Select(row => row.Steps[ColumnIndex])
            .Where(step => step != null);
        foreach (var step in stepsToExecute)
        {
            await pauseToken.WaitWhilePausedAsync(ct);
            await ExecuteStep(step!, ct);
        }
        ClearStatusIfNotFailed();
    }

    private async Task ExecuteStep(ITestStep step, CancellationToken ct)
    {
        StartNewStep(step);
        try
        {
            var result = await step.ExecuteAsync(context, ct);
            ProcessStepResult(step, result);
        }
        catch (OperationCanceledException)
        {
            ClearStatusIfNotFailed();
        }
        catch (Exception ex)
        {
            SetErrorState(step, ex.Message);
            LogError(step, ex.Message, ex);
        }
    }

    private void StartNewStep(ITestStep step)
    {
        var uiId = statusReporter.ReportStepStarted(step);
        ApplyRunningState(step, uiId);
    }

    private void RestartFailedStep()
    {
        statusReporter.ReportRetry(_state.UiStepId);
        ApplyRunningState(_state.FailedStep!, _state.UiStepId);
    }

    private void ApplyRunningState(ITestStep step, Guid uiId)
    {
        _state = new StepState(step.Name, step.Description, "Выполняется", null, null, false, uiId, null);
        testLogger.LogStepStart(step.Name);
        OnStateChanged?.Invoke();
    }

    private void ProcessStepResult(ITestStep step, TestStepResult result)
    {
        if (!result.Success)
        {
            SetErrorState(step, result.Message);
            LogError(step, result.Message, null);
            return;
        }
        SetSuccessState(step, result);
    }

    private void SetSuccessState(ITestStep step, TestStepResult result)
    {
        var statusText = result.Skipped ? "Пропуск" : "Готово";
        statusReporter.ReportSuccess(_state.UiStepId, result.Message);
        _state = _state with { Status = statusText, ResultValue = result.Message, FailedStep = null };
        testLogger.LogStepEnd(step.Name);
        if (!string.IsNullOrEmpty(result.Message))
        {
            testLogger.LogInformation("  Результат: {Message}", result.Message);
        }
        OnStateChanged?.Invoke();
    }

    private void SetErrorState(ITestStep step, string message)
    {
        statusReporter.ReportError(_state.UiStepId, message);
        _state = _state with { Status = "Ошибка", ErrorMessage = message, HasFailed = true, FailedStep = step };
        OnStateChanged?.Invoke();
    }

    private void LogError(ITestStep step, string message, Exception? ex)
    {
        logger.LogError(ex, "Шаг {Step} в колонке {Col}: {Error}", step.Name, ColumnIndex, message);
        testLogger.LogError(ex, "ОШИБКА в шаге '{Step}': {Message}", step.Name, message);
    }

    private void ClearStatusIfNotFailed()
    {
        if (_state.HasFailed)
        {
            return;
        }
        _state = _state with { Status = null };
        OnStateChanged?.Invoke();
    }

    public void Reset()
    {
        _state = EmptyState;
        context.Variables.Clear();
    }

    public void ClearFailedState()
    {
        if (!_state.HasFailed)
        {
            return;
        }
        _state = _state with { HasFailed = false, FailedStep = null, Status = null };
        OnStateChanged?.Invoke();
    }

    public async Task RetryLastFailedStepAsync(CancellationToken ct)
    {
        if (_state.FailedStep == null)
        {
            return;
        }
        var step = _state.FailedStep;
        RestartFailedStep();
        try
        {
            var result = await step.ExecuteAsync(context, ct);
            ProcessStepResult(step, result);
        }
        catch (OperationCanceledException)
        {
            ClearStatusIfNotFailed();
        }
        catch (Exception ex)
        {
            SetErrorState(step, ex.Message);
            LogError(step, ex.Message, ex);
        }
    }
}
