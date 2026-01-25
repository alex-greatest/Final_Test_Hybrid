using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Plc;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    private Channel<bool>? _errorSignalChannel;

    /// <summary>
    /// Обрабатывает изменение состояния executor'а.
    /// </summary>
    private void HandleExecutorStateChanged()
    {
        OnStateChanged?.Invoke();
        EnqueueFailedExecutors();
    }

    /// <summary>
    /// Создаёт и запускает канал для сигналов об ошибках.
    /// </summary>
    private Channel<bool> StartErrorSignalChannel()
    {
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });
        Interlocked.Exchange(ref _errorSignalChannel, channel);
        return channel;
    }

    /// <summary>
    /// Сигнализирует об обнаружении ошибки.
    /// </summary>
    private void SignalErrorDetected()
    {
        var channel = Volatile.Read(ref _errorSignalChannel);
        channel?.Writer.TryWrite(true);
    }

    /// <summary>
    /// Завершает канал сигналов после окончания выполнения.
    /// </summary>
    private void CompleteErrorSignalChannel()
    {
        var channel = Interlocked.Exchange(ref _errorSignalChannel, null);
        if (channel == null)
        {
            return;
        }
        channel.Writer.TryWrite(true);
        channel.Writer.TryComplete();
    }

    /// <summary>
    /// Запускает цикл обработки ошибок.
    /// </summary>
    private Task RunErrorHandlingLoopAsync(ChannelReader<bool> reader, CancellationToken token)
    {
        return ProcessErrorSignalsAsync(reader, token);
    }

    /// <summary>
    /// Обрабатывает сигналы об ошибках из канала.
    /// </summary>
    private async Task ProcessErrorSignalsAsync(ChannelReader<bool> reader, CancellationToken token)
    {
        try
        {
            await foreach (var _ in reader.ReadAllAsync(token))
            {
                await HandleErrorsIfAny();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Добавляет ошибки упавших колонок в очередь.
    /// </summary>
    private void EnqueueFailedExecutors()
    {
        var state = StateManager.State;
        if (state != ExecutionState.Running && state != ExecutionState.PausedOnError)
        {
            return;
        }

        lock (_enqueueLock)
        {
            var hadErrors = StateManager.HasPendingErrors;
            foreach (var executor in _executors.Where(e => e.HasFailed))
            {
                var error = CreateErrorFromExecutor(executor);
                StateManager.EnqueueError(error);
            }
            if (!hadErrors && StateManager.HasPendingErrors)
            {
                SignalErrorDetected();
            }
        }
    }

    /// <summary>
    /// Создаёт объект ошибки из состояния executor'а.
    /// </summary>
    private static StepError CreateErrorFromExecutor(ColumnExecutor executor)
    {
        return new StepError(
            executor.ColumnIndex,
            executor.CurrentStepName ?? "Неизвестный шаг",
            executor.CurrentStepDescription ?? "",
            executor.ErrorMessage ?? "Неизвестная ошибка",
            DateTime.Now,
            Guid.Empty,
            executor.FailedStep);
    }

    /// <summary>
    /// Обрабатывает все ошибки в очереди.
    /// </summary>
    private async Task HandleErrorsIfAny()
    {
        var cts = _cts;
        if (cts == null)
        {
            _logger.LogWarning("HandleErrorsIfAny вызван без активного CancellationTokenSource");
            return;
        }

        while (StateManager.HasPendingErrors && !cts.IsCancellationRequested)
        {
            var error = StateManager.CurrentError;
            if (error == null)
            {
                break;
            }
            StateManager.TransitionTo(ExecutionState.PausedOnError);
            await SetSelectedAsync(error, true);
            await SetFaultIfNoBlockAsync(error.FailedStep, cts.Token);
            OnErrorOccurred?.Invoke(error);
            ErrorResolution resolution;
            try
            {
                var options = new WaitForResolutionOptions(
                    BlockEndTag: GetBlockEndTag(error.FailedStep),
                    BlockErrorTag: GetBlockErrorTag(error.FailedStep));
                resolution = await _errorCoordinator.WaitForResolutionAsync(options, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // await SetSelectedAsync(error, false);  // PLC сам сбросит
                break;
            }
            if (cts.IsCancellationRequested || _flowState.IsStopRequested)
            {
                break;
            }
            if (resolution == ErrorResolution.Timeout)
            {
                await HandleTagTimeoutAsync("ожидание решения оператора", cts.Token);
                break;
            }
            await ProcessErrorResolution(error, resolution, cts.Token);
            // await SetSelectedAsync(error, false);  // PLC сам сбросит
        }
        if (!cts.IsCancellationRequested && !_flowState.IsStopRequested)
        {
            StateManager.TransitionTo(ExecutionState.Running);
        }
    }

    /// <summary>
    /// Устанавливает тег Selected для PLC-блока.
    /// </summary>
    /// <summary>
    /// Обрабатывает таймаут ожидания PLC-тегов как жёсткий стоп теста.
    /// </summary>
    private async Task HandleTagTimeoutAsync(string context, CancellationToken ct)
    {
        var cts = _cts;
        if (cts == null)
        {
            _logger.LogWarning("TagTimeout во время {Context}, но нет активного CancellationTokenSource", context);
            return;
        }

        _logger.LogWarning("TagTimeout во время {Context} — жёсткий стоп теста", context);
        await _errorCoordinator.HandleInterruptAsync(InterruptReason.TagTimeout, ct);
        await cts.CancelAsync();
    }

    /// <summary>
    /// Устанавливает тег Selected для PLC-блока.
    /// </summary>
    private async Task SetSelectedAsync(StepError error, bool value)
    {
        if (error.FailedStep is not IHasPlcBlockPath plcStep)
        {
            return;
        }
        var selectedTag = PlcBlockTagHelper.GetSelectedTag(plcStep);
        if (selectedTag == null)
        {
            return;
        }
        _logger.LogDebug("Установка Selected={Value} для {BlockPath}", value, plcStep.PlcBlockPath);
        var result = await _plcService.WriteAsync(selectedTag, value);
        if (result.Error != null)
        {
            _logger.LogWarning("Ошибка записи Selected: {Error}", result.Error);
        }
    }

    /// <summary>
    /// Устанавливает Fault для шагов без PLC-блока.
    /// </summary>
    private async Task SetFaultIfNoBlockAsync(ITestStep? step, CancellationToken ct)
    {
        if (step is IHasPlcBlockPath)
        {
            return;
        }

        _logger.LogDebug("Установка Fault=true для шага без блока");
        await _plcService.WriteAsync(BaseTags.Fault, true, ct);
    }

    /// <summary>
    /// Сбрасывает Fault для шагов без PLC-блока.
    /// </summary>
    private async Task ResetFaultIfNoBlockAsync(ITestStep? step, CancellationToken ct)
    {
        if (step is IHasPlcBlockPath)
        {
            return;
        }

        _logger.LogDebug("Сброс Fault=false для шага без блока");
        await _plcService.WriteAsync(BaseTags.Fault, false, ct);
    }

    /// <summary>
    /// Обрабатывает решение пользователя по ошибке.
    /// </summary>
    private async Task ProcessErrorResolution(StepError error, ErrorResolution resolution, CancellationToken ct)
    {
        ColumnExecutor executor;
        lock (_enqueueLock)
        {
            executor = _executors[error.ColumnIndex];
        }

        if (resolution == ErrorResolution.Retry)
        {
            await ProcessRetryAsync(error, executor, ct);
        }
        else
        {
            await ProcessSkipAsync(error, executor, ct);
        }
    }

    /// <summary>
    /// Безопасно вызывает событие OnRetryStarted.
    /// </summary>
    private void InvokeRetryStartedSafely()
    {
        try
        {
            OnRetryStarted?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка в обработчике OnRetryStarted");
        }
    }

    /// <summary>
    /// Обрабатывает повтор шага.
    /// Fire-and-forget для retry, чтобы диалог следующей ошибки появился сразу.
    /// </summary>
    private async Task ProcessRetryAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
    {
        try
        {
            var blockErrorTag = GetBlockErrorTag(error.FailedStep);
            await _errorCoordinator.SendAskRepeatAsync(blockErrorTag, ct);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Block.Error не сброшен за 5 сек — жёсткий стоп");
            await HandleTagTimeoutAsync("Block.Error не сброшен", ct);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка SendAskRepeatAsync для колонки {Column}", error.ColumnIndex);
            return;
        }

        InvokeRetryStartedSafely();

        try
        {
            await _errorCoordinator.WaitForRetrySignalResetAsync(ct);
        }
        catch (TimeoutException)
        {
            _logger.LogError("Req_Repeat не сброшен за 5 сек — жёсткий стоп");
            await HandleTagTimeoutAsync("Req_Repeat не сброшен", ct);
            return;
        }

        StateManager.DequeueError();

        _ = ExecuteRetryInBackgroundAsync(error, executor, ct);
    }

    /// <summary>
    /// Выполняет retry шага в фоне.
    /// Открывает gate после успешного завершения.
    /// </summary>
    private async Task ExecuteRetryInBackgroundAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
    {
        try
        {
            await executor.RetryLastFailedStepAsync(ct);

            await ResetFaultIfNoBlockAsync(error.FailedStep, ct);

            if (!executor.HasFailed)
            {
                executor.OpenGate();
            }
        }
        catch (OperationCanceledException)
        {
            // Гарантируем что колонка не зависнет при Cancel
            if (!executor.HasFailed)
            {
                executor.OpenGate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка Retry в фоне для колонки {Column}", error.ColumnIndex);
        }
    }

    /// <summary>
    /// Обрабатывает пропуск шага.
    /// </summary>
    private async Task ProcessSkipAsync(StepError error, ColumnExecutor executor, CancellationToken ct)
    {
        await ResetBlockStartAsync(error.FailedStep, ct);
        await ResetFaultIfNoBlockAsync(error.FailedStep, ct);

        _logger.LogWarning(">>> ProcessSkipAsync: НАЧАЛО ожидания сброса сигналов");
        try
        {
            await WaitForSkipSignalsResetAsync(error.FailedStep, ct);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(">>> ProcessSkipAsync: TIMEOUT РѕР¶РёРґР°РЅРёСЏ СЃР±СЂРѕСЃР° СЃРёРіРЅР°Р»РѕРІ");
            await HandleTagTimeoutAsync("сброс сигналов Skip", ct);
            return;
        }
        _logger.LogWarning(">>> ProcessSkipAsync: КОНЕЦ ожидания сброса сигналов");

        StateManager.MarkErrorSkipped();
        StateManager.DequeueError();     // СНАЧАЛА удаляем из очереди (защита от race condition)
        executor.ClearFailedState();     // ПОТОМ открываем gate
    }

    /// <summary>
    /// Ожидает сброса сигналов после пропуска.
    /// </summary>
    /// <exception cref="TimeoutException">Сигнал не сброшен за 5 секунд.</exception>
    private async Task WaitForSkipSignalsResetAsync(ITestStep? step, CancellationToken ct)
    {
        if (step is IHasPlcBlockPath plcStep)
        {
            // Для шагов С блоком: ждём сброс Block.Error И Block.End
            // Skip детектируется по (End=true AND Error=true), нужно сбросить оба
            // Защита от stale сигналов при следующей ошибке в том же блоке
            var errorTag = PlcBlockTagHelper.GetErrorTag(plcStep);
            var endTag = PlcBlockTagHelper.GetEndTag(plcStep);

            if (errorTag != null)
            {
                _logger.LogDebug("Ожидание сброса Block.Error: {Tag}", errorTag);
                await _tagWaiter.WaitForFalseAsync(errorTag, TimeSpan.FromSeconds(5), ct);
            }
            if (endTag != null)
            {
                _logger.LogDebug("Ожидание сброса Block.End: {Tag}", endTag);
                await _tagWaiter.WaitForFalseAsync(endTag, TimeSpan.FromSeconds(5), ct);
            }
            return;
        }

        // Для шагов БЕЗ блока: ждём Test_End_Step=false с таймаутом
        // (PLC сбросит после того как PC сбросит Fault)
        _logger.LogDebug("Ожидание сброса Test_End_Step");
        await _tagWaiter.WaitForFalseAsync(BaseTags.TestEndStep, timeout: TimeSpan.FromSeconds(5), ct);
    }

    /// <summary>
    /// Сбрасывает сигнал Start для PLC-блока.
    /// </summary>
    private async Task ResetBlockStartAsync(ITestStep? step, CancellationToken ct)
    {
        if (step is not IHasPlcBlockPath plcStep)
        {
            return;
        }
        var startTag = PlcBlockTagHelper.GetStartTag(plcStep);
        if (startTag == null)
        {
            return;
        }
        _logger.LogDebug("Сброс Start для {BlockPath}", plcStep.PlcBlockPath);
        await _plcService.WriteAsync(startTag, false, ct);
    }

    /// <summary>
    /// Возвращает тег End для PLC-блока.
    /// </summary>
    private static string? GetBlockEndTag(ITestStep? step)
    {
        return PlcBlockTagHelper.GetEndTag(step as IHasPlcBlockPath);
    }

    /// <summary>
    /// Возвращает тег Error для PLC-блока.
    /// </summary>
    private static string? GetBlockErrorTag(ITestStep? step)
    {
        return PlcBlockTagHelper.GetErrorTag(step as IHasPlcBlockPath);
    }
}
