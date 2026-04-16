using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Удерживает последний установленный шагом режим котла по регистру 1036
/// и периодически подтверждает его повторной записью.
/// </summary>
public sealed class BoilerOperationModeRefreshService : IDisposable
{
    private const ushort OperationModeRegisterDoc = 1036;
    private const ushort RetainedMinHeatingMode = 3;
    private const ushort RetainedMaxHeatingMode = 4;
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DefaultDispatcherPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DefaultFailedRefreshRetryDelay = TimeSpan.FromSeconds(5);

    private readonly IModbusDispatcher _dispatcher;
    private readonly RegisterWriter _writer;
    private readonly RegisterReader _reader;
    private readonly DualLogger<BoilerOperationModeRefreshService> _logger;
    private readonly PlcResetCoordinator? _plcResetCoordinator;
    private readonly IErrorCoordinator? _errorCoordinator;
    private readonly BoilerState? _boilerState;
    private readonly Lock _stateLock = new();
    private readonly SemaphoreSlim _signal = new(initialCount: 0, maxCount: 1);
    private readonly SemaphoreSlim _modeChangeGate = new(initialCount: 1, maxCount: 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly TimeSpan _refreshInterval;
    private readonly TimeSpan _dispatcherPollInterval;
    private readonly TimeSpan _failedRefreshRetryDelay;
    private readonly ushort _operationModeAddress;
    private readonly Task _workerTask;
    private Task _backgroundDrainTask = Task.CompletedTask;
    private CancellationTokenSource _stateCts;
    private ArmedModeState? _armedState;
    private int _stateVersion;
    private bool _disposed;

    public BoilerOperationModeRefreshService(
        IModbusDispatcher dispatcher,
        RegisterWriter writer,
        RegisterReader reader,
        IOptions<DiagnosticSettings> settings,
        PlcResetCoordinator plcResetCoordinator,
        IErrorCoordinator errorCoordinator,
        BoilerState boilerState,
        DualLogger<BoilerOperationModeRefreshService> logger)
        : this(
            dispatcher,
            writer,
            reader,
            settings.Value,
            logger,
            refreshInterval: null,
            dispatcherPollInterval: null,
            failedRefreshRetryDelay: null,
            plcResetCoordinator,
            errorCoordinator,
            boilerState)
    {
    }

    internal BoilerOperationModeRefreshService(
        IModbusDispatcher dispatcher,
        RegisterWriter writer,
        RegisterReader reader,
        DiagnosticSettings settings,
        DualLogger<BoilerOperationModeRefreshService> logger,
        TimeSpan? refreshInterval,
        TimeSpan? dispatcherPollInterval,
        TimeSpan? failedRefreshRetryDelay,
        PlcResetCoordinator? plcResetCoordinator = null,
        IErrorCoordinator? errorCoordinator = null,
        BoilerState? boilerState = null)
    {
        _dispatcher = dispatcher;
        _writer = writer;
        _reader = reader;
        _logger = logger;
        _plcResetCoordinator = plcResetCoordinator;
        _errorCoordinator = errorCoordinator;
        _boilerState = boilerState;
        _refreshInterval = ResolveRefreshInterval(refreshInterval, settings.OperationModeRefreshInterval);
        _dispatcherPollInterval = dispatcherPollInterval ?? DefaultDispatcherPollInterval;
        _failedRefreshRetryDelay = failedRefreshRetryDelay ?? DefaultFailedRefreshRetryDelay;
        _operationModeAddress = (ushort)(OperationModeRegisterDoc - settings.BaseAddressOffset);
        _stateCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        _workerTask = Task.Run(() => RunAsync(_disposeCts.Token));

        AttachRuntimeSubscriptions();
    }

    private TimeSpan ResolveRefreshInterval(TimeSpan? refreshInterval, TimeSpan configuredRefreshInterval)
    {
        var interval = refreshInterval ?? configuredRefreshInterval;
        if (interval > TimeSpan.Zero)
        {
            return interval;
        }

        _logger.LogWarning(
            "Некорректный интервал удержания режима 1036 ({RefreshInterval}); используется значение по умолчанию {DefaultRefreshInterval}.",
            interval,
            DefaultRefreshInterval);
        return DefaultRefreshInterval;
    }

    public async Task<IDisposable> AcquireModeChangeLeaseAsync(CancellationToken ct)
    {
        await _modeChangeGate.WaitAsync(ct);
        return new ModeChangeLease(_modeChangeGate);
    }

    public void ArmMode(ushort modeValue, string sourceStep)
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            ReplaceStateTokenLocked();
            _stateVersion++;
            _armedState = new ArmedModeState(
                modeValue,
                sourceStep,
                DateTime.UtcNow + _refreshInterval);
        }

        _logger.LogInformation(
            "Удержание режима 1036 взведено: режим={Mode}, источник={SourceStep}, refresh={RefreshMinutes} мин",
            modeValue,
            sourceStep,
            _refreshInterval.TotalMinutes);
        SignalWorker();
    }

    public void Clear(string reason)
    {
        if (!InvalidateState(reason))
        {
            return;
        }

        StartBackgroundDrain();
    }

    public async Task ClearAndDrainAsync(string reason, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!InvalidateState(reason))
        {
            return;
        }

        using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, ct);
        try
        {
            await WaitForModeChangeQuiescenceAsync(drainCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RunAsync(CancellationToken disposeCt)
    {
        while (!disposeCt.IsCancellationRequested)
        {
            try
            {
                await RunSingleIterationAsync(disposeCt);
            }
            catch (OperationCanceledException) when (!disposeCt.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка фонового удержания режима 1036");
                await Task.Delay(_failedRefreshRetryDelay, disposeCt);
            }
        }
    }

    private async Task RunSingleIterationAsync(CancellationToken disposeCt)
    {
        var snapshot = CaptureSnapshot();

        if (snapshot is null)
        {
            await WaitForSignalAsync(timeout: null, disposeCt);
            return;
        }

        if (TryGetDelayUntilRefresh(snapshot, out var delay))
        {
            await WaitForSignalAsync(delay, snapshot.Token);
            return;
        }

        if (!IsDispatcherReady())
        {
            await WaitForSignalAsync(_dispatcherPollInterval, snapshot.Token);
            return;
        }

        var success = await TryRefreshAsync(snapshot, snapshot.Token);
        ScheduleNextAttempt(snapshot, success);
    }

    private RefreshSnapshot? CaptureSnapshot()
    {
        lock (_stateLock)
        {
            if (_armedState is null)
            {
                return null;
            }

            return new RefreshSnapshot(
                _stateVersion,
                _armedState.ModeValue,
                _armedState.SourceStep,
                _armedState.NextRefreshUtc,
                _stateCts.Token);
        }
    }

    private static bool TryGetDelayUntilRefresh(RefreshSnapshot snapshot, out TimeSpan delay)
    {
        delay = snapshot.NextRefreshUtc - DateTime.UtcNow;
        return delay > TimeSpan.Zero;
    }

    private async Task WaitForSignalAsync(TimeSpan? timeout, CancellationToken ct)
    {
        if (timeout is null)
        {
            await _signal.WaitAsync(ct);
            return;
        }

        _ = await _signal.WaitAsync(timeout.Value, ct);
    }

    private bool IsDispatcherReady()
    {
        return _dispatcher.IsStarted
               && _dispatcher is { IsConnected: true, IsReconnecting: false, LastPingData: not null };
    }

    private async Task<bool> TryRefreshAsync(RefreshSnapshot snapshot, CancellationToken ct)
    {
        using var modeChangeLease = await AcquireModeChangeLeaseAsync(ct);
        if (!CanContinueRefresh(snapshot, ct))
        {
            return false;
        }

        _logger.LogInformation(
            "Повторное подтверждение режима 1036: режим={Mode}, источник={SourceStep}",
            snapshot.ModeValue,
            snapshot.SourceStep);

        var sequence = CreateRefreshSequence(snapshot.ModeValue);
        if (!await TryWriteRefreshSequenceAsync(snapshot, sequence, ct))
        {
            return false;
        }

        var readResult = await _reader.ReadUInt16Async(_operationModeAddress, ct);
        if (!readResult.Success)
        {
            _logger.LogWarning(
                "Не удалось перечитать 1036 после refresh режима {Mode} ({SourceStep}): {Error}",
                snapshot.ModeValue,
                snapshot.SourceStep,
                readResult.Error);
            return false;
        }

        if (readResult.Value != snapshot.ModeValue)
        {
            _logger.LogWarning(
                "Refresh режима 1036 не подтверждён: источник={SourceStep}, ожидалось={Expected}, прочитано={Actual}",
                snapshot.SourceStep,
                snapshot.ModeValue,
                readResult.Value);
            return false;
        }

        _logger.LogInformation(
            "Refresh режима 1036 подтверждён: режим={Mode}, источник={SourceStep}",
            snapshot.ModeValue,
            snapshot.SourceStep);
        return true;
    }

    private static RefreshSequence CreateRefreshSequence(ushort targetMode)
    {
        var oppositeMode = TryGetOppositeRefreshMode(targetMode);
        return new RefreshSequence(oppositeMode, targetMode);
    }

    private static ushort? TryGetOppositeRefreshMode(ushort targetMode)
    {
        return targetMode switch
        {
            RetainedMinHeatingMode => RetainedMaxHeatingMode,
            RetainedMaxHeatingMode => RetainedMinHeatingMode,
            _ => null
        };
    }

    private async Task<bool> TryWriteRefreshSequenceAsync(
        RefreshSnapshot snapshot,
        RefreshSequence sequence,
        CancellationToken ct)
    {
        var oppositeMode = sequence.OppositeMode;
        if (!oppositeMode.HasValue)
        {
            return await TryWriteRefreshModeAsync(
                snapshot,
                sequence.TargetMode,
                sequence.TargetMode,
                isIntermediate: false,
                checkCurrentBeforeWrite: true,
                checkCurrentAfterWrite: true,
                ioCt: _disposeCts.Token,
                currentCt: ct);
        }

        if (!await TryWriteRefreshModeAsync(
                snapshot,
                oppositeMode.Value,
                sequence.TargetMode,
                isIntermediate: true,
                checkCurrentBeforeWrite: true,
                checkCurrentAfterWrite: false,
                ioCt: _disposeCts.Token,
                currentCt: ct))
        {
            return false;
        }

        return await TryWriteRefreshModeAsync(
            snapshot,
            sequence.TargetMode,
            sequence.TargetMode,
            isIntermediate: false,
            checkCurrentBeforeWrite: false,
            checkCurrentAfterWrite: true,
            ioCt: _disposeCts.Token,
            currentCt: ct);
    }

    private async Task<bool> TryWriteRefreshModeAsync(
        RefreshSnapshot snapshot,
        ushort modeToWrite,
        ushort targetMode,
        bool isIntermediate,
        bool checkCurrentBeforeWrite,
        bool checkCurrentAfterWrite,
        CancellationToken ioCt,
        CancellationToken currentCt)
    {
        if (checkCurrentBeforeWrite && !CanContinueRefresh(snapshot, currentCt))
        {
            return false;
        }

        var writeResult = await _writer.WriteUInt16Async(_operationModeAddress, modeToWrite, ioCt);
        if (writeResult.Success)
        {
            return !checkCurrentAfterWrite || CanContinueRefresh(snapshot, currentCt);
        }

        LogRefreshWriteFailure(snapshot, modeToWrite, targetMode, isIntermediate, writeResult.Error);
        return false;
    }

    private void LogRefreshWriteFailure(
        RefreshSnapshot snapshot,
        ushort modeToWrite,
        ushort targetMode,
        bool isIntermediate,
        string? error)
    {
        if (isIntermediate)
        {
            _logger.LogWarning(
                "Не удалось записать промежуточный режим 1036={IntermediateMode} перед целевым режимом {TargetMode} ({SourceStep}): {Error}",
                modeToWrite,
                targetMode,
                snapshot.SourceStep,
                error);
            return;
        }

        _logger.LogWarning(
            "Не удалось повторно записать режим 1036={Mode} ({SourceStep}): {Error}",
            modeToWrite,
            snapshot.SourceStep,
            error);
    }

    private bool CanContinueRefresh(RefreshSnapshot snapshot, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return IsSnapshotCurrent(snapshot);
    }

    private bool IsSnapshotCurrent(RefreshSnapshot snapshot)
    {
        lock (_stateLock)
        {
            return !_disposed
                   && _armedState is not null
                   && _stateVersion == snapshot.Version;
        }
    }

    private void ScheduleNextAttempt(RefreshSnapshot snapshot, bool success)
    {
        lock (_stateLock)
        {
            if (_disposed || _armedState is null || _stateVersion != snapshot.Version)
            {
                return;
            }

            var nextRefreshUtc = DateTime.UtcNow + (success ? _refreshInterval : _failedRefreshRetryDelay);
            _armedState = _armedState with { NextRefreshUtc = nextRefreshUtc };
        }

        SignalWorker();
    }

    private void ReplaceStateTokenLocked()
    {
        _stateCts.Cancel();
        _stateCts.Dispose();
        _stateCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
    }

    private bool InvalidateState(string reason)
    {
        bool hadState;

        lock (_stateLock)
        {
            if (_disposed)
            {
                return false;
            }

            hadState = _armedState != null;
            ReplaceStateTokenLocked();
            _stateVersion++;
            _armedState = null;
        }

        if (hadState)
        {
            _logger.LogInformation("Удержание режима 1036 очищено: {Reason}", reason);
        }

        SignalWorker();
        return true;
    }

    private void StartBackgroundDrain()
    {
        lock (_stateLock)
        {
            if (_disposed || !_backgroundDrainTask.IsCompleted)
            {
                return;
            }

            _backgroundDrainTask = DrainModeChangeInBackgroundAsync();
        }
    }

    private async Task DrainModeChangeInBackgroundAsync()
    {
        try
        {
            await WaitForModeChangeQuiescenceAsync(_disposeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Ошибка фонового drain удержания режима 1036: {Error}", ex.Message);
        }
    }

    private async Task WaitForModeChangeQuiescenceAsync(CancellationToken ct)
    {
        await _modeChangeGate.WaitAsync(ct).ConfigureAwait(false);
        _modeChangeGate.Release();
    }

    private void AttachRuntimeSubscriptions()
    {
        _dispatcher.Connected += HandleDispatcherReadySignal;
        _dispatcher.PingDataUpdated += HandlePingDataUpdated;

        if (_plcResetCoordinator != null)
        {
            _plcResetCoordinator.OnForceStop += HandleForceStop;
        }

        if (_errorCoordinator != null)
        {
            _errorCoordinator.OnReset += HandleErrorReset;
        }

        if (_boilerState != null)
        {
            _boilerState.OnCleared += HandleBoilerStateCleared;
        }
    }

    private void HandleDispatcherReadySignal()
    {
        SignalWorker();
    }

    private void HandlePingDataUpdated(DiagnosticPingData _)
    {
        SignalWorker();
    }

    private void HandleForceStop()
    {
        Clear("soft reset / PLC OnForceStop");
    }

    private void HandleErrorReset()
    {
        Clear("hard reset / ErrorCoordinator.OnReset");
    }

    private void HandleBoilerStateCleared()
    {
        Clear("BoilerState.Clear / нормальное завершение или полный cleanup");
    }

    private void SignalWorker()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        Task backgroundDrainTask;

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stateCts.Cancel();
            _stateCts.Dispose();
            backgroundDrainTask = _backgroundDrainTask;
        }

        _disposeCts.Cancel();
        _dispatcher.Connected -= HandleDispatcherReadySignal;
        _dispatcher.PingDataUpdated -= HandlePingDataUpdated;

        if (_plcResetCoordinator != null)
        {
            _plcResetCoordinator.OnForceStop -= HandleForceStop;
        }

        if (_errorCoordinator != null)
        {
            _errorCoordinator.OnReset -= HandleErrorReset;
        }

        if (_boilerState != null)
        {
            _boilerState.OnCleared -= HandleBoilerStateCleared;
        }

        try
        {
            WaitForShutdownTask(_workerTask);
            WaitForShutdownTask(backgroundDrainTask);
        }
        finally
        {
            _disposeCts.Dispose();
            _signal.Dispose();
            _modeChangeGate.Dispose();
        }
    }

    private static void WaitForShutdownTask(Task task)
    {
        try
        {
            task.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e =>
                     e is OperationCanceledException or ObjectDisposedException))
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private sealed record ArmedModeState(ushort ModeValue, string SourceStep, DateTime NextRefreshUtc);

    private sealed record RefreshSnapshot(
        int Version,
        ushort ModeValue,
        string SourceStep,
        DateTime NextRefreshUtc,
        CancellationToken Token);

    private sealed record RefreshSequence(ushort? OppositeMode, ushort TargetMode);

    private sealed class ModeChangeLease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;

        public void Dispose()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
        }
    }
}
