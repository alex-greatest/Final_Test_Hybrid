using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Errors;

public sealed class GasValveTubeDeferredErrorService
{
    private static readonly TimeSpan DefaultRaiseDelay = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly TimeSpan _raiseDelay;
    private CancellationTokenSource? _delayCts;
    private ErrorDefinition? _activeTagError;
    private ErrorDefinition? _currentError;
    private bool _isErrorRaised;
    private bool _isMessageActive;
    private long _activationVersion;
    private int _isStarted;

    public event Action? OnStateChanged;

    public bool IsMessageActive
    {
        get
        {
            lock (_lock)
            {
                return _isMessageActive;
            }
        }
    }

    public GasValveTubeDeferredErrorService(
        OpcUaSubscription subscription,
        OpcUaConnectionState connectionState,
        IStepTimingService stepTimingService,
        IErrorService errorService,
        ILogger<GasValveTubeDeferredErrorService> logger)
        : this(
            subscription,
            connectionState,
            stepTimingService,
            errorService,
            logger,
            DefaultRaiseDelay,
            static (delay, ct) => Task.Delay(delay, ct))
    {
    }

    internal GasValveTubeDeferredErrorService(
        OpcUaSubscription subscription,
        OpcUaConnectionState connectionState,
        IStepTimingService stepTimingService,
        IErrorService errorService,
        ILogger<GasValveTubeDeferredErrorService> logger,
        TimeSpan raiseDelay,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        _raiseDelay = raiseDelay;
        _delayAsync = delayAsync;
        Subscription = subscription;
        StepTimingService = stepTimingService;
        ErrorService = errorService;
        Logger = logger;
        connectionState.ConnectionStateChanged += HandleConnectionStateChanged;
        stepTimingService.OnChanged += HandleStepTimingChanged;
    }

    private OpcUaSubscription Subscription { get; }
    private IStepTimingService StepTimingService { get; }
    private IErrorService ErrorService { get; }
    private ILogger<GasValveTubeDeferredErrorService> Logger { get; }

    public async Task StartMonitoringAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _isStarted, 1) == 1)
        {
            return;
        }

        foreach (var errorDef in ErrorDefinitions.DeferredPlcErrors)
        {
            await Subscription.SubscribeAsync(
                errorDef.PlcTag!,
                value => ProcessTagChangedAsync(errorDef, value),
                ct);

            Logger.LogDebug(
                "Подписка на deferred PLC ошибку {Code}: {Tag}",
                errorDef.Code,
                errorDef.PlcTag);
        }
    }

    internal Task ProcessTagChangedAsync(ErrorDefinition error, object? value)
    {
        if (!PlcErrorValueNormalizer.TryNormalizeBooleanValue(
                value,
                out var isActive,
                out var normalizedType,
                out var normalizationNote))
        {
            Logger.LogWarning(
                "Deferred PLC error callback skipped (не bool): Code={Code}, Tag={Tag}, Value={Value}, Type={Type}, NormalizeNote={NormalizeNote}",
                error.Code,
                error.PlcTag,
                value,
                normalizedType,
                normalizationNote);

            return Task.CompletedTask;
        }

        if (isActive)
        {
            lock (_lock)
            {
                _activeTagError = error;
            }

            ReconcileState();
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            if (_activeTagError?.Code == error.Code)
            {
                _activeTagError = null;
            }
        }

        ReconcileState();
        return Task.CompletedTask;
    }

    private void ReconcileState()
    {
        ErrorDefinition? nextError;
        string? previousErrorCode = null;
        CancellationToken delayToken;
        long? activationVersion = null;
        bool notify;

        lock (_lock)
        {
            nextError = GetStepScopedErrorLocked();

            if (_currentError?.Code == nextError?.Code)
            {
                if (nextError is null || _isMessageActive)
                {
                    return;
                }

                _isMessageActive = true;
                notify = true;
                delayToken = CancellationToken.None;
            }
            else
            {
                if (_currentError is not null
                    && _currentError.Code != nextError?.Code)
                {
                    previousErrorCode = _currentError.Code;
                }

                CancelDelayLocked();
                _currentError = nextError;
                _isErrorRaised = false;
                _activationVersion++;

                if (nextError is null)
                {
                    notify = _isMessageActive;
                    _isMessageActive = false;
                    delayToken = CancellationToken.None;
                }
                else
                {
                    _isMessageActive = true;
                    notify = true;
                    activationVersion = _activationVersion;
                    _delayCts = new CancellationTokenSource();
                    delayToken = _delayCts.Token;
                }
            }
        }

        if (previousErrorCode is not null)
        {
            ErrorService.ClearPlc(previousErrorCode);
        }

        if (notify)
        {
            NotifyChanged();
        }

        if (delayToken != CancellationToken.None)
        {
            _ = RunDelayedRaiseAsync(nextError!, activationVersion!.Value, delayToken);
        }
    }

    private ErrorDefinition? GetStepScopedErrorLocked()
    {
        var stepName = _activeTagError?.RelatedStepName;
        if (stepName is null)
        {
            return null;
        }

        if (!StepTimingService.HasActiveStep(stepName))
        {
            return null;
        }

        return _activeTagError;
    }

    private async Task RunDelayedRaiseAsync(
        ErrorDefinition error,
        long activationVersion,
        CancellationToken ct)
    {
        try
        {
            await _delayAsync(_raiseDelay, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_lock)
        {
            if (_currentError?.Code != error.Code
                || _activationVersion != activationVersion
                || _isErrorRaised)
            {
                return;
            }

            _isErrorRaised = true;
            CancelDelayLocked();
        }

        ErrorService.RaisePlc(error, error.RelatedStepId, error.RelatedStepName);
    }

    private void HandleConnectionStateChanged(bool isConnected)
    {
        if (isConnected)
        {
            return;
        }

        lock (_lock)
        {
            _activeTagError = null;
        }

        ReconcileState();
    }

    private void HandleStepTimingChanged()
    {
        ReconcileState();
    }

    private void CancelDelayLocked()
    {
        if (_delayCts is null)
        {
            return;
        }

        _delayCts.Cancel();
        _delayCts.Dispose();
        _delayCts = null;
    }

    private void NotifyChanged()
    {
        var handler = OnStateChanged;
        if (handler is null)
        {
            return;
        }

        try
        {
            handler();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Ошибка в обработчике OnStateChanged deferred PGB monitor");
        }
    }
}
