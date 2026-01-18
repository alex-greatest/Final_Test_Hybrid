using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class ColumnExecutor(
    int columnIndex,
    TestStepContext context,
    ITestStepLogger testLogger,
    ILogger logger,
    StepStatusReporter statusReporter,
    PauseTokenSource pauseToken,
    IErrorService errorService,
    IStepTimingService stepTimingService)
{
    private ErrorScope? _errorScope;

    private record StepState(
        string? Name,
        string? Description,
        string? Status,
        string? ErrorMessage,
        string? ResultValue,
        bool HasFailed,
        Guid UiStepId,
        ITestStep? FailedStep,
        DateTime StartTime);

    private static readonly StepState EmptyState = new(null, null, null, null, null, false, Guid.Empty, null, default);
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
        await ExecuteStepCoreAsync(step, ct);
    }

    private async Task ExecuteStepCoreAsync(ITestStep step, CancellationToken ct)
    {
        try
        {
            stepTimingService.StartColumnStepTiming(ColumnIndex, step.Name, step.Description);
            var result = await step.ExecuteAsync(context, ct);
            stepTimingService.StopColumnStepTiming(ColumnIndex);
            ProcessStepResult(step, result);
        }
        catch (OperationCanceledException)
        {
            stepTimingService.StopColumnStepTiming(ColumnIndex);
            ClearStatusIfNotFailed();
        }
        catch (Exception ex)
        {
            stepTimingService.StopColumnStepTiming(ColumnIndex);
            SetErrorState(step, ex.Message, null);
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
        _state = new StepState(step.Name, step.Description, "Выполняется", null, null, false, uiId, null, DateTime.Now);
        testLogger.LogStepStart(step.Name);
        OnStateChanged?.Invoke();
    }

    private void ProcessStepResult(ITestStep step, TestStepResult result)
    {
        var limits = result.OutputData?.TryGetValue("Limits", out var val) == true
            ? val.ToString()
            : null;
        if (!result.Success)
        {
            SetErrorState(step, result.Message, limits, result.Errors);
            LogError(step, result.Message, null);
            return;
        }

        ClearStepErrors();
        SetSuccessState(step, result, limits);
    }

    private void SetSuccessState(ITestStep step, TestStepResult result, string? limits)
    {
        var statusText = result.Skipped ? "Пропуск" : "Готово";
        statusReporter.ReportSuccess(_state.UiStepId, result.Message, limits);
        _state = _state with { Status = statusText, ResultValue = result.Message, FailedStep = null };
        testLogger.LogStepEnd(step.Name);
        if (!string.IsNullOrEmpty(result.Message))
        {
            testLogger.LogInformation("  Результат: {Message}", result.Message);
        }
        OnStateChanged?.Invoke();
    }

    private void SetErrorState(ITestStep step, string message, string? limits, List<ErrorDefinition>? errors = null)
    {
        statusReporter.ReportError(_state.UiStepId, message, limits);
        ClearStepErrors();
        _errorScope = new ErrorScope(errorService);
        _errorScope.Raise(errors, step.Id, step.Name);
        _state = _state with { Status = "Ошибка", ErrorMessage = message, HasFailed = true, FailedStep = step };
        OnStateChanged?.Invoke();
    }

    private void ClearStepErrors()
    {
        _errorScope?.Clear();
        _errorScope = null;
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
        ClearStepErrors();
        _state = EmptyState;
        context.Variables.Clear();
    }

    public void ClearFailedState()
    {
        if (!_state.HasFailed)
        {
            return;
        }

        ClearStepErrors();
        _state = _state with { HasFailed = false, FailedStep = null, Status = null };
        OnStateChanged?.Invoke();
    }

    public async Task RetryLastFailedStepAsync(CancellationToken ct)
    {
        var step = _state.FailedStep;
        if (step == null)
        {
            return;
        }
        RestartFailedStep();
        await ExecuteStepCoreAsync(step, ct);
    }
}
