using Final_Test_Hybrid.Services.Common.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Lifecycle;

/// <summary>
/// Единый источник истины для фаз жизненного цикла системы.
/// Thread-safe реализация с атомарными переходами.
/// </summary>
/// <remarks>
/// <para>
/// Двухуровневая архитектура:
/// - SystemLifecycleManager: управляет фазами СИСТЕМЫ (Idle → Testing)
/// - ExecutionStateManager (EXISTS): управляет состояниями ВНУТРИ теста (Running ⇄ PausedOnError)
/// </para>
/// <para>
/// Гарантия синхронизации Scanner ↔ Input Field:
/// <code>
/// IsScannerInputEnabled == true ↔ Phase == WaitingForBarcode ↔ Scanner session активна ↔ Input field enabled
/// </code>
/// </para>
/// </remarks>
public class SystemLifecycleManager(DualLogger<SystemLifecycleManager> logger)
{
    private readonly Lock _lock = new();
    private SystemPhase _phase = SystemPhase.Idle;
    private string? _currentBarcode;

    /// <summary>
    /// Вызывается при изменении фазы. Параметры: (oldPhase, newPhase).
    /// </summary>
    public event Action<SystemPhase, SystemPhase>? OnPhaseChanged;

    /// <summary>
    /// Вызывается при неудачной попытке перехода. Параметры: (currentPhase, trigger).
    /// </summary>
    public event Action<SystemPhase, SystemTrigger>? OnTransitionFailed;

    /// <summary>
    /// Текущая фаза системы.
    /// </summary>
    public SystemPhase Phase
    {
        get
        {
            lock (_lock)
            {
                return _phase;
            }
        }
    }

    /// <summary>
    /// Текущий штрихкод. Управляется lifecycle rules.
    /// </summary>
    public string? CurrentBarcode
    {
        get
        {
            lock (_lock)
            {
                return _currentBarcode;
            }
        }
    }

    /// <summary>
    /// Сканер должен быть активен (Session acquired).
    /// </summary>
    public bool IsScannerActive
    {
        get
        {
            lock (_lock)
            {
                return _phase is SystemPhase.WaitingForBarcode or SystemPhase.Preparing;
            }
        }
    }

    /// <summary>
    /// Поле ввода штрихкода должно быть активно.
    /// ГАРАНТИРОВАННО синхронизировано со сканером для WaitingForBarcode.
    /// </summary>
    public bool IsScannerInputEnabled
    {
        get
        {
            lock (_lock)
            {
                return _phase == SystemPhase.WaitingForBarcode;
            }
        }
    }

    /// <summary>
    /// Настройки (SwitchMes, QR Auth) могут быть изменены.
    /// </summary>
    public bool CanInteractWithSettings
    {
        get
        {
            lock (_lock)
            {
                return _phase is SystemPhase.Idle or SystemPhase.WaitingForBarcode;
            }
        }
    }

    /// <summary>
    /// Система заблокирована (тест выполняется или сброс).
    /// </summary>
    public bool IsBlocked
    {
        get
        {
            lock (_lock)
            {
                return _phase is SystemPhase.Preparing
                    or SystemPhase.Testing
                    or SystemPhase.Completed
                    or SystemPhase.Resetting;
            }
        }
    }

    /// <summary>
    /// Активна ли любая операция (PreExecution или Testing).
    /// </summary>
    public bool IsAnyActive
    {
        get
        {
            lock (_lock)
            {
                return _phase is SystemPhase.Preparing or SystemPhase.Testing;
            }
        }
    }

    /// <summary>
    /// Выполняет переход по указанному триггеру.
    /// </summary>
    /// <param name="trigger">Триггер перехода.</param>
    /// <returns>true если переход успешен, false если недопустим.</returns>
    public bool Transition(SystemTrigger trigger)
    {
        return TransitionCore(trigger, barcode: null);
    }

    /// <summary>
    /// Выполняет переход с установкой штрихкода.
    /// </summary>
    /// <param name="trigger">Триггер перехода.</param>
    /// <param name="barcode">Штрихкод для BarcodeReceived.</param>
    /// <returns>true если переход успешен, false если недопустим.</returns>
    public bool Transition(SystemTrigger trigger, string barcode)
    {
        return TransitionCore(trigger, barcode);
    }

    /// <summary>
    /// Проверяет возможность перехода без его выполнения.
    /// </summary>
    /// <param name="trigger">Триггер для проверки.</param>
    /// <returns>true если переход допустим.</returns>
    public bool CanTransition(SystemTrigger trigger)
    {
        lock (_lock)
        {
            return GetNextPhase(_phase, trigger) != null;
        }
    }

    private bool TransitionCore(SystemTrigger trigger, string? barcode)
    {
        SystemPhase oldPhase = default;
        SystemPhase newPhase = default;
        SystemPhase? failedPhase = null;

        lock (_lock)
        {
            var nextPhase = GetNextPhase(_phase, trigger);
            if (nextPhase == null)
            {
                failedPhase = _phase;
            }
            else
            {
                oldPhase = _phase;
                newPhase = nextPhase.Value;
                _phase = newPhase;
                ApplyBarcodeRules(trigger, barcode);
            }
        }

        if (failedPhase != null)
        {
            logger.LogWarning(
                "Transition failed: {CurrentPhase} + {Trigger} → invalid",
                failedPhase.Value, trigger);
            RaiseTransitionFailed(failedPhase.Value, trigger);
            return false;
        }

        logger.LogInformation(
            "Transition: {OldPhase} → {NewPhase} (trigger: {Trigger})",
            oldPhase, newPhase, trigger);

        RaisePhaseChanged(oldPhase, newPhase);
        return true;
    }

    private void ApplyBarcodeRules(SystemTrigger trigger, string? barcode)
    {
        switch (trigger)
        {
            case SystemTrigger.BarcodeReceived:
                _currentBarcode = barcode;
                break;
            case SystemTrigger.ScanModeDisabled:
            case SystemTrigger.ResetCompletedHard:
                _currentBarcode = null;
                break;
        }
    }

    private static SystemPhase? GetNextPhase(SystemPhase current, SystemTrigger trigger)
    {
        return (current, trigger) switch
        {
            // Activation
            (SystemPhase.Idle, SystemTrigger.ScanModeEnabled) => SystemPhase.WaitingForBarcode,
            (SystemPhase.WaitingForBarcode, SystemTrigger.ScanModeDisabled) => SystemPhase.Idle,

            // Barcode flow
            (SystemPhase.WaitingForBarcode, SystemTrigger.BarcodeReceived) => SystemPhase.Preparing,
            (SystemPhase.Preparing, SystemTrigger.PreparationCompleted) => SystemPhase.Testing,
            (SystemPhase.Preparing, SystemTrigger.PreparationFailed) => SystemPhase.WaitingForBarcode,

            // Test flow
            (SystemPhase.Testing, SystemTrigger.TestFinished) => SystemPhase.Completed,
            (SystemPhase.Completed, SystemTrigger.RepeatRequested) => SystemPhase.WaitingForBarcode,
            (SystemPhase.Completed, SystemTrigger.TestAcknowledged) => SystemPhase.WaitingForBarcode,

            // Reset from any active phase
            (not SystemPhase.Idle and not SystemPhase.Resetting, SystemTrigger.ResetRequestedHard) => SystemPhase.Resetting,
            (not SystemPhase.Idle and not SystemPhase.Resetting, SystemTrigger.ResetRequestedSoft) => SystemPhase.Resetting,

            // Reset completion
            (SystemPhase.Resetting, SystemTrigger.ResetCompletedSoft) => SystemPhase.WaitingForBarcode,
            (SystemPhase.Resetting, SystemTrigger.ResetCompletedHard) => SystemPhase.Idle,

            _ => null
        };
    }

    private void RaisePhaseChanged(SystemPhase oldPhase, SystemPhase newPhase)
    {
        OnPhaseChanged?.Invoke(oldPhase, newPhase);
    }

    private void RaiseTransitionFailed(SystemPhase phase, SystemTrigger trigger)
    {
        OnTransitionFailed?.Invoke(phase, trigger);
    }
}
