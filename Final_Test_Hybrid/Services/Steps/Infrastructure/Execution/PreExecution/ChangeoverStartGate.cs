using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main.PlcReset;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public sealed class ChangeoverStartGate : IChangeoverStartGate
{
    private const int AutoStartNotStarted = 0;
    private const int AutoStartStarted = 1;
    private const int DeferredStartNone = 0;
    private const int DeferredStartPending = 1;
    private const int AskEndNotReceived = 0;
    private const int AskEndReceived = 1;
    private const int ReasonDialogNotExpected = 0;
    private const int ReasonDialogExpected = 1;

    private readonly PlcResetCoordinator _plcResetCoordinator;
    private readonly BoilerState _boilerState;
    private readonly AppSettingsService _appSettings;
    private readonly DualLogger<ChangeoverStartGate> _logger;

    private int _autoStartState;
    private int _deferredStartState;
    private int _askEndState;
    private int _reasonDialogExpectedForReset;

    public ChangeoverStartGate(
        PlcResetCoordinator plcResetCoordinator,
        BoilerState boilerState,
        AppSettingsService appSettings,
        DualLogger<ChangeoverStartGate> logger)
    {
        _plcResetCoordinator = plcResetCoordinator;
        _boilerState = boilerState;
        _appSettings = appSettings;
        _logger = logger;

        _plcResetCoordinator.OnForceStop += HandleResetStarted;
        _plcResetCoordinator.OnAskEndReceived += HandleAskEndReceived;
    }

    public void RequestStartFromAutoReady()
    {
        try
        {
            RequestStartFromAutoReadyCore();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка запуска changeover по AutoReady");
        }
    }

    private void RequestStartFromAutoReadyCore()
    {
        if (Volatile.Read(ref _autoStartState) == AutoStartStarted)
        {
            return;
        }
        if (IsPlcResetAskEndPending())
        {
            Interlocked.Exchange(ref _deferredStartState, DeferredStartPending);
            _logger.LogInformation("AutoReady получен во время PLC reset до AskEnd — запуск changeover отложен");
            return;
        }
        if (Volatile.Read(ref _reasonDialogExpectedForReset) == ReasonDialogExpected)
        {
            _logger.LogInformation("AutoReady запуск changeover заблокирован (ожидается диалог причины прерывания)");
            return;
        }
        TryStartChangeoverTimerOnce("AutoReady");
    }

    private bool IsPlcResetAskEndPending()
    {
        return _plcResetCoordinator.IsActive
            && Volatile.Read(ref _askEndState) == AskEndNotReceived;
    }

    private void TryStartChangeoverTimerOnce(string source)
    {
        if (Interlocked.CompareExchange(ref _autoStartState, AutoStartStarted, AutoStartNotStarted) != AutoStartNotStarted)
        {
            return;
        }
        _boilerState.StartChangeoverTimer();
        _logger.LogInformation("Changeover таймер запущен: {Source}", source);
    }

    private void HandleResetStarted()
    {
        try
        {
            HandleResetStartedCore();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки события PLC reset start для changeover gate");
        }
    }

    private void HandleResetStartedCore()
    {
        Volatile.Write(ref _askEndState, AskEndNotReceived);
        Volatile.Write(ref _deferredStartState, DeferredStartNone);
        Volatile.Write(ref _reasonDialogExpectedForReset, GetReasonDialogExpectedSnapshot());
    }

    private int GetReasonDialogExpectedSnapshot()
    {
        var shouldDelay = _boilerState.IsTestRunning
            && _boilerState.SerialNumber != null
            && _appSettings.UseInterruptReason;
        return shouldDelay ? ReasonDialogExpected : ReasonDialogNotExpected;
    }

    private void HandleAskEndReceived()
    {
        try
        {
            HandleAskEndReceivedCore();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки события AskEnd для changeover gate");
        }
    }

    private void HandleAskEndReceivedCore()
    {
        Volatile.Write(ref _askEndState, AskEndReceived);
        if (Interlocked.Exchange(ref _deferredStartState, DeferredStartNone) != DeferredStartPending)
        {
            return;
        }
        if (Volatile.Read(ref _reasonDialogExpectedForReset) == ReasonDialogExpected)
        {
            _logger.LogInformation("Отложенный AutoReady запуск changeover отменён (ожидается диалог причины прерывания)");
            return;
        }
        TryStartChangeoverTimerOnce("AskEnd (deferred AutoReady)");
    }
}
