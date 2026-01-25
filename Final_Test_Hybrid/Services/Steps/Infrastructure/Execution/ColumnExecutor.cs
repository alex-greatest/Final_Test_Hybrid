using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Limits;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class ColumnExecutor(
    int columnIndex,
    TestStepContext context,
    IDualLogger logger,
    StepStatusReporter statusReporter,
    PauseTokenSource pauseToken,
    IErrorService errorService,
    IStepTimingService stepTimingService)
{
    private readonly AsyncManualResetEvent _continueGate = new(true);
    private readonly SemaphoreSlim _retrySemaphore = new(1, 1);
    private ErrorScope? _errorScope;

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
        if (!_state.HasFailed)
        {
            _continueGate.Set();
        }

        var steps = map.Rows
            .Select(row => row.Steps[ColumnIndex])
            .Where(step => step != null)
            .ToList();

        foreach (var step in steps)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await _continueGate.WaitAsync(ct);

            if (ct.IsCancellationRequested)
            {
                break;
            }

            await pauseToken.WaitWhilePausedAsync(ct);
            await ExecuteStep(step!, ct);
        }

        ClearStatusIfNotFailed();
    }

    /// <summary>
    /// Выполняет один шаг теста с защитой от исключений на этапе инициализации.
    /// </summary>
    private async Task ExecuteStep(ITestStep step, CancellationToken ct)
    {
        if (!TryStartNewStep(step))
        {
            return;
        }

        await ExecuteStepCoreAsync(step, ct);
    }

    /// <summary>
    /// Пытается запустить новый шаг. При ошибке логирует и устанавливает состояние ошибки.
    /// </summary>
    /// <returns>true если шаг успешно запущен, false при ошибке.</returns>
    private bool TryStartNewStep(ITestStep step)
    {
        Guid uiId;
        try
        {
            var limits = GetPreExecutionLimits(step);
            uiId = statusReporter.ReportStepStarted(step, limits);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка регистрации шага {Step} в UI", step.Name);
            return false;
        }

        try
        {
            ApplyRunningState(step, uiId);
        }
        catch (Exception ex)
        {
            SetErrorState(step, ex.Message, null);
            LogError(step, ex.Message, ex);
            return false;
        }

        return true;
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

    /// <summary>
    /// Получает пределы от шага если он реализует IProvideLimits.
    /// Безопасно - при ошибке возвращает null и логирует.
    /// </summary>
    private string? GetPreExecutionLimits(ITestStep step)
    {
        if (step is not IProvideLimits limitsProvider)
        {
            return null;
        }

        try
        {
            var limitsContext = new LimitsContext
            {
                ColumnIndex = ColumnIndex,
                RecipeProvider = context.RecipeProvider
            };
            return limitsProvider.GetLimits(limitsContext);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Ошибка получения limits для {StepName}: {Exception}", step.Name, ex);
            return null;
        }
    }

    private void RestartFailedStep()
    {
        statusReporter.ReportRetry(_state.UiStepId);
        ApplyRunningState(_state.FailedStep!, _state.UiStepId);
    }

    private void ApplyRunningState(ITestStep step, Guid uiId)
    {
        _state = new StepState(step.Name, step.Description, "Выполняется", null, null, false, uiId, null);
        logger.LogStepStart(step.Name);
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
        logger.LogStepEnd(step.Name);
        if (!string.IsNullOrEmpty(result.Message))
        {
            logger.LogInformation("  Результат: {Message}", result.Message);
        }
        OnStateChanged?.Invoke();
    }

    private void SetErrorState(ITestStep step, string message, string? limits, List<ErrorDefinition>? errors = null)
    {
        statusReporter.ReportError(_state.UiStepId, message, limits);
        ClearStepErrors();
        _errorScope = new ErrorScope(errorService);
        _errorScope.Raise(errors, step.Id, step.Name);
        _continueGate.Reset();
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
        logger.LogError(ex, "ОШИБКА в шаге '{Step}': {Message}", step.Name, message);
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
        _continueGate.Set();
        _state = _state with { HasFailed = false, FailedStep = null, Status = null };
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Открывает gate для продолжения выполнения после успешного Retry.
    /// Вызывается из координатора после cleanup.
    /// </summary>
    public void OpenGate()
    {
        _continueGate.Set();
    }

    public async Task RetryLastFailedStepAsync(CancellationToken ct)
    {
        var acquired = false;
        try
        {
            await _retrySemaphore.WaitAsync(ct);
            acquired = true;

            var step = _state.FailedStep;
            if (step == null)
            {
                return;
            }
            RestartFailedStep();
            await ExecuteStepCoreAsync(step, ct);
        }
        finally
        {
            if (acquired)
            {
                _retrySemaphore.Release();
            }
        }
    }
}
