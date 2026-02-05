using Final_Test_Hybrid.Models.Steps;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    /// <summary>
    /// Запускает выполнение тестов с ожиданием завершения.
    /// </summary>
    public async Task StartAsync()
    {
        if (!TryStart())
        {
            return;
        }
        await RunEventLoopAsync();
    }

    /// <summary>
    /// Запускает выполнение тестов в фоновом режиме.
    /// </summary>
    public bool TryStartInBackground()
    {
        if (!TryStart())
        {
            return false;
        }
        _ = RunEventLoopAsync();
        return true;
    }

    /// <summary>
    /// Пытается начать выполнение тестов.
    /// </summary>
    private bool TryStart()
    {
        lock (_stateLock)
        {
            if (!CanStart())
            {
                return false;
            }
            BeginExecution();
            return true;
        }
    }

    /// <summary>
    /// Выполняет тесты с обработкой ошибок.
    /// </summary>
    private async Task RunWithErrorHandlingAsync()
    {
        await RunEventLoopAsync();
    }

    /// <summary>
    /// Проверяет возможность запуска.
    /// </summary>
    private bool CanStart()
    {
        if (!StateManager.IsActive)
        {
            return ValidateMapsLoaded();
        }
        _logger.LogWarning("Координатор уже выполняется");
        return false;
    }

    /// <summary>
    /// Проверяет наличие загруженных карт.
    /// </summary>
    private bool ValidateMapsLoaded()
    {
        if (HasMapsLoaded())
        {
            return true;
        }
        LogNoSequenceLoaded();
        return false;
    }

    /// <summary>
    /// Возвращает true, если карты загружены.
    /// </summary>
    private bool HasMapsLoaded()
    {
        return _maps.Count > 0;
    }

    /// <summary>
    /// Логирует отсутствие загруженной последовательности.
    /// </summary>
    private void LogNoSequenceLoaded()
    {
        _logger.LogError("Последовательность не загружена");
        _testLogger.LogError(null, "Ошибка: последовательность тестов не загружена");
    }

    /// <summary>
    /// Инициализирует состояние для начала выполнения.
    /// </summary>
    private void BeginExecution()
    {
        _latchedStopReason = ExecutionStopReason.None;
        _latchedStopAsFailure = false;
        _activityTracker.SetTestExecutionActive(true);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _logger.LogInformation("Запуск {Count} Maps", _maps.Count);
        _testLogger.LogInformation("═══ ЗАПУСК ТЕСТИРОВАНИЯ ({Count} блоков) ═══", _maps.Count);
    }

    /// <summary>
    /// Завершает выполнение и освобождает ресурсы.
    /// </summary>
    private void Complete()
    {
        var flowSnapshot = _flowState.GetSnapshot();
        var latchedSnapshot = GetLatchedStopSnapshot();
        var finalStopAsFailure = flowSnapshot.StopAsFailure || latchedSnapshot.StopAsFailure;
        var finalReason = flowSnapshot.Reason != ExecutionStopReason.None
            ? flowSnapshot.Reason
            : latchedSnapshot.Reason;
        var finalSnapshot = (Reason: finalReason, StopAsFailure: finalStopAsFailure);
        var isSuccessful = !(StateManager.State == ExecutionState.Failed || HasErrors || finalSnapshot.StopAsFailure);
        var finalState = isSuccessful ? ExecutionState.Completed : ExecutionState.Failed;
        StateManager.TransitionTo(finalState);
        _activityTracker.SetTestExecutionActive(false);
        _errorService.ClearActiveApplicationErrors();
        LogExecutionCompleted(isSuccessful, finalSnapshot);
        DispatchEvent(new ExecutionEvent(ExecutionEventKind.SequenceCompleted));
        lock (_stateLock)
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Безопасно вызывает OnSequenceCompleted с перехватом исключений из обработчиков.
    /// </summary>
    private void InvokeSequenceCompletedSafely()
    {
        try
        {
            OnSequenceCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка в обработчике OnSequenceCompleted");
        }
    }

    /// <summary>
    /// Логирует завершение выполнения.
    /// </summary>
    private void LogExecutionCompleted(bool isSuccessful, (ExecutionStopReason Reason, bool StopAsFailure) flowSnapshot)
    {
        _logger.LogInformation(
            "Завершено. Ошибки: {HasErrors}, StopAsFailure: {StopAsFailure}, StopReason: {StopReason}",
            HasErrors,
            flowSnapshot.StopAsFailure,
            flowSnapshot.Reason);
        var result = isSuccessful ? "УСПЕШНО" : "С ОШИБКАМИ";
        _testLogger.LogInformation("═══ ТЕСТИРОВАНИЕ ЗАВЕРШЕНО: {Result} ═══", result);
    }

    /// <summary>
    /// Останавливает выполнение тестов.
    /// </summary>
    public void Stop(string reason = "оператором", bool markFailed = false)
    {
        Stop(ExecutionStopReason.Operator, reason, markFailed);
    }

    /// <summary>
    /// Останавливает выполнение тестов с указанной причиной.
    /// </summary>
    public void Stop(ExecutionStopReason stopReason, string reason, bool markFailed = false)
    {
        LatchStop(stopReason, markFailed);
        _flowState.RequestStop(stopReason, markFailed);
        CancelExecution(reason);
    }

    /// <summary>
    /// Запрашивает остановку как ошибку без немедленной отмены.
    /// </summary>
    private void RequestStopAsFailure(ExecutionStopReason reason)
    {
        LatchStop(reason, stopAsFailure: true);
        _flowState.RequestStop(reason, stopAsFailure: true);
    }

    private void LatchStop(ExecutionStopReason stopReason, bool stopAsFailure)
    {
        lock (_stateLock)
        {
            if (_latchedStopReason == ExecutionStopReason.None)
            {
                _latchedStopReason = stopReason;
            }
            _latchedStopAsFailure |= stopAsFailure;
        }
    }

    private (ExecutionStopReason Reason, bool StopAsFailure) GetLatchedStopSnapshot()
    {
        lock (_stateLock)
        {
            return (_latchedStopReason, _latchedStopAsFailure);
        }
    }

    /// <summary>
    /// Отменяет выполнение.
    /// </summary>
    private void CancelExecution(string reason)
    {
        lock (_stateLock)
        {
            if (_cts == null || _cts.IsCancellationRequested)
            {
                return;
            }
            LogStopRequested(reason);
            _cts.Cancel();
        }
    }

    /// <summary>
    /// Логирует запрос на остановку.
    /// </summary>
    private void LogStopRequested(string reason)
    {
        _logger.LogInformation("Остановка: {Reason}", reason);
        _testLogger.LogWarning("Тестирование остановлено {Reason}", reason);
    }
}

