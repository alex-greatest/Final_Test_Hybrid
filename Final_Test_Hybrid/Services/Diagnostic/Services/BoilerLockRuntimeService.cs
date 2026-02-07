using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Runtime-обработка блокировок котла по данным ping (статус 1005 + LastErrorId).
/// </summary>
public sealed class BoilerLockRuntimeService : IDisposable
{
    private const ushort ModeKeyRegisterDoc = 1000;
    private const short StatusPauseBranch = 1;
    private const short StatusPlcSignalBranch = 2;
    private const ushort BoilerStatusRegisterDoc = 1005;
    private const ushort ResetBlockageRegisterDoc = 1153;
    private const ushort ResetBlockageValue = 0;
    private const uint StandModeKey = 0xD7F8_DB56;

    // Коды из 111.txt
    private static readonly HashSet<ushort> TargetErrorIds =
    [
        1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 18, 23, 26
    ];

    private readonly IModbusDispatcher _dispatcher;
    private readonly AccessLevelManager _accessLevelManager;
    private readonly RegisterReader _reader;
    private readonly RegisterWriter _writer;
    private readonly DiagnosticSettings _settings;
    private readonly ExecutionActivityTracker _activityTracker;
    private readonly IErrorCoordinator _errorCoordinator;
    private readonly DualLogger<BoilerLockRuntimeService> _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly ushort _modeKeyAddress;
    private readonly ushort _boilerStatusAddress;
    private readonly ushort _resetBlockageAddress;

    private DateTime _lastModeSwitchAttemptUtc = DateTime.MinValue;
    private DateTime _lastResetAttemptUtc = DateTime.MinValue;
    private DateTime _suppressUntilUtc = DateTime.MinValue;
    private DateTime _lastSuppressLogUtc = DateTime.MinValue;
    private int _modeSwitchFailureStreak;
    private int _resetFailureStreak;
    private bool _status2SignalSent;
    private volatile bool _disposed;

    public BoilerLockRuntimeService(
        IModbusDispatcher dispatcher,
        AccessLevelManager accessLevelManager,
        RegisterReader reader,
        RegisterWriter writer,
        IOptions<DiagnosticSettings> settings,
        ExecutionActivityTracker activityTracker,
        IErrorCoordinator errorCoordinator,
        DualLogger<BoilerLockRuntimeService> logger)
    {
        _dispatcher = dispatcher;
        _accessLevelManager = accessLevelManager;
        _reader = reader;
        _writer = writer;
        _settings = settings.Value;
        _activityTracker = activityTracker;
        _errorCoordinator = errorCoordinator;
        _logger = logger;
        _modeKeyAddress = (ushort)(ModeKeyRegisterDoc - _settings.BaseAddressOffset);
        _boilerStatusAddress = (ushort)(BoilerStatusRegisterDoc - _settings.BaseAddressOffset);
        _resetBlockageAddress = (ushort)(ResetBlockageRegisterDoc - _settings.BaseAddressOffset);

        _dispatcher.PingDataUpdated += OnPingDataUpdated;
        _dispatcher.Disconnecting += OnDisconnecting;
        _dispatcher.Stopped += OnDispatcherStopped;
        _errorCoordinator.OnReset += OnErrorCoordinatorReset;
    }

    private void OnPingDataUpdated(DiagnosticPingData data)
    {
        _ = HandlePingAsync(data, _disposeCts.Token).ContinueWith(
            t => _logger.LogError(t.Exception, "Ошибка обработки BoilerLock runtime"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private Task OnDisconnecting()
    {
        ResetStateAndRelease("diagnostic disconnect");
        return Task.CompletedTask;
    }

    private void OnDispatcherStopped()
    {
        ResetStateAndRelease("dispatcher stopped");
    }

    private void OnErrorCoordinatorReset()
    {
        ResetStateAndRelease("error reset");
    }

    private async Task HandlePingAsync(DiagnosticPingData data, CancellationToken ct)
    {
        if (_disposed || !await TryEnterAsync(ct))
        {
            return;
        }

        try
        {
            await ProcessPingAsync(data, ct);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task<bool> TryEnterAsync(CancellationToken ct)
    {
        try
        {
            return await _sync.WaitAsync(0, ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task ProcessPingAsync(DiagnosticPingData data, CancellationToken ct)
    {
        var pauseCondition = ShouldPauseOnStatus1(data);
        ReleaseBoilerLockWhenNoLongerNeeded(pauseCondition);

        if (!ShouldProcessBranches())
        {
            ResetRuntimeState();
            return;
        }

        if (!pauseCondition)
        {
            ResetStatus1AttemptState();
        }

        HandleStatus2Branch(data);
        await HandleStatus1BranchAsync(data, ct);
    }

    private bool ShouldProcessBranches()
    {
        var config = _settings.BoilerLock;
        return config.Enabled && _activityTracker.IsTestExecutionActive;
    }

    private bool ShouldPauseOnStatus1(DiagnosticPingData data)
    {
        var config = _settings.BoilerLock;
        return config is { Enabled: true, PauseOnStatus1Enabled: true }
               && _activityTracker.IsTestExecutionActive
               && data.BoilerStatus == StatusPauseBranch
               && IsTargetError(data.LastErrorId);
    }

    private bool ShouldSignalOnStatus2(DiagnosticPingData data)
    {
        var config = _settings.BoilerLock;
        return config is { Enabled: true, PlcSignalOnStatus2Enabled: true }
               && _activityTracker.IsTestExecutionActive
               && data.BoilerStatus == StatusPlcSignalBranch
               && IsTargetError(data.LastErrorId);
    }

    private static bool IsTargetError(ushort? errorId)
    {
        return errorId.HasValue && TargetErrorIds.Contains(errorId.Value);
    }

    private void HandleStatus2Branch(DiagnosticPingData data)
    {
        if (!ShouldSignalOnStatus2(data))
        {
            _status2SignalSent = false;
            return;
        }
        if (_status2SignalSent)
        {
            return;
        }

        SendStatus2SignalStub(data.LastErrorId!.Value);
        _status2SignalSent = true;
    }

    private async Task HandleStatus1BranchAsync(DiagnosticPingData data, CancellationToken ct)
    {
        if (!ShouldPauseOnStatus1(data))
        {
            return;
        }
        if (!await EnsureBoilerLockPauseAsync(ct))
        {
            return;
        }
        if (!CanRunStatus1Flow())
        {
            LogSuppressedAttempt();
            return;
        }

        var standResult = await EnsureStandModeAsync(data.ModeKey, ct);
        if (!standResult.Success)
        {
            return;
        }

        await ExecuteResetAttemptAsync(standResult.ModeKey, data.BoilerStatus, ct);
    }

    private bool CanRunStatus1Flow()
    {
        return DateTime.UtcNow >= _suppressUntilUtc;
    }

    private async Task<bool> EnsureBoilerLockPauseAsync(CancellationToken ct)
    {
        var currentInterrupt = _errorCoordinator.CurrentInterrupt;
        if (currentInterrupt == InterruptReason.BoilerLock)
        {
            return true;
        }
        if (currentInterrupt != null)
        {
            return false;
        }

        await _errorCoordinator.HandleInterruptAsync(InterruptReason.BoilerLock, ct);
        return _errorCoordinator.CurrentInterrupt == InterruptReason.BoilerLock;
    }

    private async Task<StandModeCheckResult> EnsureStandModeAsync(uint currentModeKey, CancellationToken ct)
    {
        if (!ShouldRequireStandBeforeReset())
        {
            return StandModeCheckResult.SuccessResult(currentModeKey);
        }
        if (currentModeKey == StandModeKey)
        {
            return StandModeCheckResult.SuccessResult(currentModeKey);
        }
        if (!CanStartModeSwitchCycle())
        {
            return StandModeCheckResult.FailResult();
        }

        var maxAttempts = GetBoundedPositive(_settings.BoilerLock.ResetFlow.ModeSwitchRetryMax, 2);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInformation(
                "BoilerLock: status=1, ModeKey=0x{ModeKey:X8}. Переход в Stand (попытка {Attempt}/{MaxAttempts})",
                currentModeKey,
                attempt,
                maxAttempts);

            var switchSuccess = await _accessLevelManager.SetStandModeAsync(ct);
            if (!switchSuccess)
            {
                _logger.LogWarning(
                    "BoilerLock: не удалось записать ключ Stand в {DocAddress}-{DocAddressNext}",
                    ModeKeyRegisterDoc,
                    ModeKeyRegisterDoc + 1);

                await DelayBeforeRetryAsync(attempt, maxAttempts, ct);
                continue;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);
            var modeRead = await ReadModeKeyAsync(ct);
            if (!modeRead.Success)
            {
                _logger.LogWarning("BoilerLock: не удалось перечитать ModeKey: {Error}", modeRead.Error);
                await DelayBeforeRetryAsync(attempt, maxAttempts, ct);
                continue;
            }
            if (modeRead.ModeKey == StandModeKey)
            {
                _modeSwitchFailureStreak = 0;
                return StandModeCheckResult.SuccessResult(modeRead.ModeKey);
            }

            _logger.LogWarning(
                "BoilerLock: после записи Stand прочитан ModeKey=0x{ModeKey:X8} (ожидался 0x{StandModeKey:X8})",
                modeRead.ModeKey,
                StandModeKey);
            await DelayBeforeRetryAsync(attempt, maxAttempts, ct);
        }

        ApplySuppressWindow("переход в Stand", ref _modeSwitchFailureStreak);
        return StandModeCheckResult.FailResult();
    }

    private async Task ExecuteResetAttemptAsync(uint modeKey, short boilerStatus, CancellationToken ct)
    {
        if (!CanStartResetCycle())
        {
            return;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(_settings.WriteVerifyDelayMs), ct);

        var maxAttempts = GetBoundedPositive(_settings.BoilerLock.ResetFlow.ResetRetryMax, 3);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (_errorCoordinator.CurrentInterrupt != InterruptReason.BoilerLock)
            {
                return;
            }

            var writeResult = await _writer.WriteUInt16Async(_resetBlockageAddress, ResetBlockageValue, ct);
            if (writeResult.Success)
            {
                _resetFailureStreak = 0;
                await TryResumeByStatusAsync(ct);
                return;
            }

            _logger.LogWarning(
                "BoilerLock: запись reset не удалась (Doc={DocAddress}, Modbus={ModbusAddress}, Attempt={Attempt}/{MaxAttempts}, ModeKey=0x{ModeKey:X8}, Status={Status}, Error={Error})",
                ResetBlockageRegisterDoc,
                _resetBlockageAddress,
                attempt,
                maxAttempts,
                modeKey,
                boilerStatus,
                writeResult.Error);

            await DelayBeforeRetryAsync(attempt, maxAttempts, ct);
        }

        ApplySuppressWindow("запись 1153=0", ref _resetFailureStreak);
    }

    private bool ShouldRequireStandBeforeReset()
    {
        return _settings.BoilerLock.ResetFlow.RequireStandForReset;
    }

    private bool CanStartModeSwitchCycle()
    {
        return CanStartAttemptCycle(ref _lastModeSwitchAttemptUtc, "перехода в Stand");
    }

    private bool CanStartResetCycle()
    {
        return CanStartAttemptCycle(ref _lastResetAttemptUtc, "сброса 1153");
    }

    private bool CanStartAttemptCycle(ref DateTime lastAttemptUtc, string operation)
    {
        var nowUtc = DateTime.UtcNow;
        var cooldownMs = GetBoundedPositive(_settings.BoilerLock.ResetFlow.AttemptCooldownMs, 1000);
        if (nowUtc - lastAttemptUtc < TimeSpan.FromMilliseconds(cooldownMs))
        {
            _logger.LogDebug("BoilerLock: пропуск цикла {Operation}, действует cooldown {CooldownMs} мс", operation, cooldownMs);
            return false;
        }

        lastAttemptUtc = nowUtc;
        return true;
    }

    private void ApplySuppressWindow(string operation, ref int failureStreak)
    {
        failureStreak++;
        var suppressMs = GetBoundedPositive(_settings.BoilerLock.ResetFlow.ErrorSuppressMs, 5000);
        _suppressUntilUtc = DateTime.UtcNow.AddMilliseconds(suppressMs);
        _logger.LogWarning("BoilerLock: исчерпаны попытки {Operation}. Подавление новых попыток на {SuppressMs} мс", operation, suppressMs);
    }

    private void LogSuppressedAttempt()
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc - _lastSuppressLogUtc < TimeSpan.FromMilliseconds(1000))
        {
            return;
        }

        _lastSuppressLogUtc = nowUtc;
        _logger.LogWarning("BoilerLock: попытки временно подавлены до {SuppressUntil:O}", _suppressUntilUtc);
    }

    private async Task TryResumeByStatusAsync(CancellationToken ct)
    {
        var statusResult = await _reader.ReadInt16Async(_boilerStatusAddress, ct);
        if (!statusResult.Success)
        {
            _logger.LogWarning("BoilerLock: не удалось перечитать 1005: {Error}", statusResult.Error);
            return;
        }
        if (statusResult.Value == StatusPauseBranch)
        {
            return;
        }

        ReleaseBoilerLock("status changed");
    }

    private async Task<ModeKeyReadResult> ReadModeKeyAsync(CancellationToken ct)
    {
        var readResult = await _reader.ReadUInt32Async(_modeKeyAddress, ct);
        if (!readResult.Success)
        {
            return ModeKeyReadResult.Fail(readResult.Error);
        }

        return ModeKeyReadResult.SuccessResult(readResult.Value);
    }

    private async Task DelayBeforeRetryAsync(int attempt, int maxAttempts, CancellationToken ct)
    {
        if (attempt >= maxAttempts)
        {
            return;
        }

        var retryDelayMs = GetBoundedPositive(_settings.BoilerLock.ResetFlow.RetryDelayMs, 250);
        await Task.Delay(TimeSpan.FromMilliseconds(retryDelayMs), ct);
    }

    private static int GetBoundedPositive(int value, int fallback)
    {
        return value > 0 ? value : fallback;
    }

    private void ReleaseBoilerLockWhenNoLongerNeeded(bool pauseCondition)
    {
        if (_errorCoordinator.CurrentInterrupt != InterruptReason.BoilerLock || pauseCondition)
        {
            return;
        }

        ReleaseBoilerLock("condition gone");
    }

    private void ReleaseBoilerLock(string reason)
    {
        if (_errorCoordinator.CurrentInterrupt != InterruptReason.BoilerLock)
        {
            return;
        }

        _logger.LogInformation("BoilerLock: снятие паузы ({Reason})", reason);
        _errorCoordinator.ForceStop();
    }

    private void SendStatus2SignalStub(ushort errorId)
    {
        _logger.LogInformation(
            "BoilerLock(status=2): PLC signal stub. ErrorId={ErrorId}. Реальный PLC-триггер будет добавлен отдельно.",
            errorId);
    }

    private void ResetStateAndRelease(string reason)
    {
        ResetRuntimeState();
        ReleaseBoilerLock(reason);
    }

    private void ResetRuntimeState()
    {
        _status2SignalSent = false;
        ResetStatus1AttemptState();
    }

    private void ResetStatus1AttemptState()
    {
        _lastModeSwitchAttemptUtc = DateTime.MinValue;
        _lastResetAttemptUtc = DateTime.MinValue;
        _suppressUntilUtc = DateTime.MinValue;
        _lastSuppressLogUtc = DateTime.MinValue;
        _modeSwitchFailureStreak = 0;
        _resetFailureStreak = 0;
    }

    private sealed record StandModeCheckResult(bool Success, uint ModeKey)
    {
        public static StandModeCheckResult SuccessResult(uint modeKey) => new(true, modeKey);
        public static StandModeCheckResult FailResult() => new(false, 0);
    }

    private sealed record ModeKeyReadResult(bool Success, uint ModeKey, string? Error)
    {
        public static ModeKeyReadResult SuccessResult(uint modeKey) => new(true, modeKey, null);
        public static ModeKeyReadResult Fail(string? error) => new(false, 0, error);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeCts.Cancel();
        _dispatcher.PingDataUpdated -= OnPingDataUpdated;
        _dispatcher.Disconnecting -= OnDisconnecting;
        _dispatcher.Stopped -= OnDispatcherStopped;
        _errorCoordinator.OnReset -= OnErrorCoordinatorReset;
        _sync.Dispose();
        _disposeCts.Dispose();
    }
}
