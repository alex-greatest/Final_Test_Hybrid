namespace Final_Test_Hybrid.Services.Main;

using Final_Test_Hybrid.Models;
using Messages;
using OpcUa.Connection;
using SpringBoot.Operator;
using Steps.Infrastructure.Execution.ErrorCoordinator;
using Steps.Infrastructure.Execution.Scanning;
using PlcReset;

public class MessageService
{
    private readonly Lock _lock = new();
    private readonly (int priority, Func<bool> condition, Func<string> message)[] _rules;

    private readonly OperatorState _operator;
    private readonly AutoReadySubscription _autoReady;
    private readonly OpcUaConnectionState _connection;
    private readonly ScanModeController _scanMode;
    private readonly ExecutionPhaseState _phaseState;
    private readonly ErrorCoordinator _errorCoord;
    private readonly PlcResetCoordinator _resetCoord;
    private readonly BoilerState _boilerState;

    public event Action? OnChange;

    public MessageService(
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        OpcUaConnectionState connection,
        ScanModeController scanMode,
        ExecutionPhaseState phaseState,
        ErrorCoordinator errorCoord,
        PlcResetCoordinator resetCoord,
        BoilerState boilerState)
    {
        _operator = operatorState;
        _autoReady = autoReady;
        _connection = connection;
        _scanMode = scanMode;
        _phaseState = phaseState;
        _errorCoord = errorCoord;
        _resetCoord = resetCoord;
        _boilerState = boilerState;

        _rules = BuildRules();
        SubscribeToChanges();
    }

    private (int, Func<bool>, Func<string>)[] BuildRules() =>
    [
        // Критичные комбинации (проблема + сброс)
        (200, () => !_connection.IsConnected && _resetCoord.IsActive,
              () => "Потеря связи с PLC. Выполняется сброс..."),

        (190, () => _errorCoord.CurrentInterrupt == InterruptReason.TagTimeout && _resetCoord.IsActive,
              () => "Нет ответа от ПЛК. Выполняется сброс..."),

        (160, () => !_autoReady.IsReady && _resetCoord.IsActive,
              () => "Нет автомата. Выполняется сброс..."),

        // Критичные без сброса
        (180, () => !_connection.IsConnected,
              () => "Нет связи с PLC"),

        (170, () => _errorCoord.CurrentInterrupt == InterruptReason.TagTimeout,
              () => "Нет ответа от ПЛК"),

        // Сброс
        (150, () => _resetCoord.IsActive,
              () => "Сброс теста..."),

        // Системные
        (140, () => !_operator.IsAuthenticated,
              () => "Войдите в систему"),

        (130, () => _operator.IsAuthenticated && !_autoReady.IsReady,
              () => "Ожидание автомата"),

        (125, () => _errorCoord.CurrentInterrupt == InterruptReason.BoilerLock,
              () => "Блокировка котла. Ожидание восстановления"),

        // Сканирование (только если тест не запущен и нет активной фазы)
        (120, () => _scanMode.IsScanModeEnabled && !_boilerState.IsTestRunning && _phaseState.Phase == null,
              () => "Отсканируйте серийный номер котла"),

        // Фазы выполнения
        (110, () => _phaseState.Phase != null,
              GetPhaseMessage),
    ];

    private string GetPhaseMessage() => _phaseState.Phase switch
    {
        ExecutionPhase.BarcodeReceived => "Штрихкод получен",
        ExecutionPhase.ValidatingSteps => "Проверка шагов...",
        ExecutionPhase.ValidatingRecipes => "Проверка рецептов...",
        ExecutionPhase.LoadingRecipes => "Загрузка рецептов...",
        ExecutionPhase.CreatingDbRecords => "Создание записей в БД...",
        ExecutionPhase.WaitingForAdapter => "Подсоедините адаптер к котлу и нажмите \"Блок\"",
        ExecutionPhase.WaitingForDiagnosticConnection => "Подключите кабель связи с котлом",
        _ => ""
    };

    private void SubscribeToChanges()
    {
        _operator.OnStateChanged += NotifyChanged;
        _autoReady.OnStateChanged += NotifyChanged;
        _connection.ConnectionStateChanged += _ => NotifyChanged();
        _scanMode.OnStateChanged += NotifyChanged;
        _phaseState.OnChanged += NotifyChanged;
        _errorCoord.OnInterruptChanged += NotifyChanged;
        _resetCoord.OnActiveChanged += NotifyChanged;
        _boilerState.OnChanged += NotifyChanged;
    }

    public string CurrentMessage
    {
        get
        {
            lock (_lock)
            {
                return _rules
                    .OrderByDescending(r => r.priority)
                    .Where(r => r.condition())
                    .Select(r => r.message())
                    .FirstOrDefault() ?? "";
            }
        }
    }

    public void NotifyChanged() => OnChange?.Invoke();
}
