using Final_Test_Hybrid.Services.Common;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

/// <summary>
/// State machine для управления состоянием сканирования.
/// Заменяет разбросанные флаги (IsProcessing, IsScanModeEnabled, _activeScanStepId)
/// на единый источник правды с атомарными переходами.
/// </summary>
public sealed class ScanStateManager : INotifyStateChanged, IDisposable
{
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _processLock = new(1, 1);

    private ScanState _state = ScanState.Disabled;
    private string? _currentBarcode;
    private Guid? _activeScanStepId;
    private string? _errorMessage;
    private bool _disposed;

    /// <summary>
    /// Текущее состояние системы сканирования.
    /// </summary>
    public ScanState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Текущий штрихкод (заполняется в Processing).
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
    /// ID активного шага сканирования в гриде.
    /// </summary>
    public Guid? ActiveScanStepId
    {
        get
        {
            lock (_lock)
            {
                return _activeScanStepId;
            }
        }
    }

    /// <summary>
    /// Сообщение об ошибке (заполняется в Error).
    /// </summary>
    public string? ErrorMessage
    {
        get
        {
            lock (_lock)
            {
                return _errorMessage;
            }
        }
    }

    /// <summary>
    /// Можно ли принимать ввод штрихкода.
    /// </summary>
    public bool CanAcceptInput => State == ScanState.Ready;

    /// <summary>
    /// Активна ли система (Processing или TestRunning).
    /// </summary>
    public bool IsActive => State is ScanState.Processing or ScanState.TestRunning;

    /// <summary>
    /// Событие изменения состояния. Реализует INotifyStateChanged.
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Событие с детальной информацией о переходе.
    /// </summary>
    public event Action<ScanState, ScanState>? OnStateTransition;

    /// <summary>
    /// Попытка перехода в новое состояние.
    /// Выполняет callback в пределах lock'а для атомарности.
    /// </summary>
    /// <param name="newState">Целевое состояние.</param>
    /// <param name="onTransition">Callback, выполняемый при успешном переходе (внутри lock'а).</param>
    /// <returns>true если переход выполнен, false если отклонён.</returns>
    public bool TryTransitionTo(ScanState newState, Action? onTransition = null)
    {
        ScanState oldState;

        lock (_lock)
        {
            if (_disposed)
            {
                return false;
            }

            if (!IsTransitionValid(_state, newState))
            {
                return false;
            }

            oldState = _state;
            _state = newState;

            if (newState == ScanState.Disabled || newState == ScanState.Ready)
            {
                _currentBarcode = null;
                _errorMessage = null;
            }

            if (newState != ScanState.Error)
            {
                _errorMessage = null;
            }

            onTransition?.Invoke();
        }

        NotifyStateChanged(oldState, newState);
        return true;
    }

    /// <summary>
    /// Принудительный переход в состояние (без валидации).
    /// Использовать только для сброса/инициализации.
    /// </summary>
    public void ForceTransitionTo(ScanState newState, Action? onTransition = null)
    {
        ScanState oldState;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            oldState = _state;
            _state = newState;
            onTransition?.Invoke();
        }

        NotifyStateChanged(oldState, newState);
    }

    /// <summary>
    /// Установить штрихкод (только в Processing).
    /// </summary>
    public void SetBarcode(string barcode)
    {
        lock (_lock)
        {
            _currentBarcode = barcode;
        }
    }

    /// <summary>
    /// Очистить штрихкод.
    /// </summary>
    public void ClearBarcode()
    {
        lock (_lock)
        {
            _currentBarcode = null;
        }
    }

    /// <summary>
    /// Установить ID активного шага.
    /// </summary>
    public void SetActiveScanStepId(Guid? id)
    {
        lock (_lock)
        {
            _activeScanStepId = id;
        }
    }

    /// <summary>
    /// Установить ошибку и перейти в Error state.
    /// </summary>
    public bool SetError(string message)
    {
        ScanState oldState;

        lock (_lock)
        {
            if (_disposed)
            {
                return false;
            }

            oldState = _state;
            _state = ScanState.Error;
            _errorMessage = message;
        }

        NotifyStateChanged(oldState, ScanState.Error);
        return true;
    }

    /// <summary>
    /// Очистить ошибку (не меняет состояние).
    /// </summary>
    public void ClearError()
    {
        lock (_lock)
        {
            _errorMessage = null;
        }
    }

    /// <summary>
    /// Попытка захватить lock для обработки.
    /// Используется для предотвращения параллельной обработки.
    /// </summary>
    public bool TryAcquireProcessLock()
    {
        return _processLock.Wait(0);
    }

    /// <summary>
    /// Освободить lock обработки.
    /// </summary>
    public void ReleaseProcessLock()
    {
        if (!_disposed)
        {
            _processLock.Release();
        }
    }

    private static bool IsTransitionValid(ScanState from, ScanState to)
    {
        return (from, to) switch
        {
            // Из Disabled
            (ScanState.Disabled, ScanState.Ready) => true,

            // Из Ready
            (ScanState.Ready, ScanState.Processing) => true,
            (ScanState.Ready, ScanState.Disabled) => true,

            // Из Processing
            (ScanState.Processing, ScanState.TestRunning) => true,
            (ScanState.Processing, ScanState.Ready) => true, // При ошибке PreExecution
            (ScanState.Processing, ScanState.Error) => true,
            (ScanState.Processing, ScanState.Disabled) => true, // Если автомат отключился

            // Из TestRunning
            (ScanState.TestRunning, ScanState.Ready) => true, // Тест завершён
            (ScanState.TestRunning, ScanState.Error) => true,
            (ScanState.TestRunning, ScanState.Disabled) => true, // Если автомат отключился

            // Из Error
            (ScanState.Error, ScanState.Ready) => true, // После обработки ошибки
            (ScanState.Error, ScanState.Processing) => true, // Retry
            (ScanState.Error, ScanState.Disabled) => true,

            // Одинаковые состояния
            _ when from == to => false,

            // Всё остальное запрещено
            _ => false
        };
    }

    private void NotifyStateChanged(ScanState oldState, ScanState newState)
    {
        if (oldState == newState)
        {
            return;
        }

        OnStateTransition?.Invoke(oldState, newState);
        OnStateChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _processLock.Dispose();
    }
}
