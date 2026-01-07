using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

namespace Final_Test_Hybrid.Services.Main;

/// <summary>
/// Состояние доступа к настройкам (переключение MES, etc).
/// </summary>
public enum SettingsAccessState
{
    /// <summary>
    /// Взаимодействие разрешено.
    /// Нет запущенных тестов ИЛИ находимся на шаге сканирования.
    /// </summary>
    Allowed,

    /// <summary>
    /// Взаимодействие заблокировано.
    /// Тесты выполняются и не находимся на шаге сканирования.
    /// </summary>
    Blocked
}

/// <summary>
/// State manager для доступа к настройкам.
/// Заменяет SettingsInteractionState с его комбинированной проверкой.
/// </summary>
public sealed class SettingsAccessStateManager : INotifyStateChanged, IDisposable
{
    private readonly TestSequenseService _testSequenseService;
    private SettingsAccessState _state = SettingsAccessState.Allowed;

    public SettingsAccessState State
    {
        get => _state;
        private set
        {
            if (_state == value)
            {
                return;
            }
            _state = value;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Можно ли взаимодействовать с настройками.
    /// </summary>
    public bool CanInteract => State == SettingsAccessState.Allowed;

    public event Action? OnStateChanged;

    public SettingsAccessStateManager(TestSequenseService testSequenseService)
    {
        _testSequenseService = testSequenseService;
        _testSequenseService.OnDataChanged += UpdateState;
        UpdateState();
    }

    private void UpdateState()
    {
        var hasNoTests = !_testSequenseService.Data.Any();
        var isOnScanStep = _testSequenseService.IsOnActiveScanStep;

        State = hasNoTests || isOnScanStep
            ? SettingsAccessState.Allowed
            : SettingsAccessState.Blocked;
    }

    public void Dispose()
    {
        _testSequenseService.OnDataChanged -= UpdateState;
    }
}
