using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;

namespace Final_Test_Hybrid.Models;

public class BoilerState
{
    private readonly Lock _lock = new();
    private readonly IRecipeProvider _recipeProvider;

    public BoilerState(AppSettingsService appSettings, IRecipeProvider recipeProvider)
    {
        _recipeProvider = recipeProvider;
        appSettings.UseMesChanged += _ => Clear();
    }
    private string? _serialNumber;
    private string? _article;
    private bool _isValid;
    private BoilerTypeCycle? _boilerTypeCycle;
    private IReadOnlyList<RecipeResponseDto>? _recipes;
    private string? _lastSerialNumber;
    private DateTime? _lastTestCompletedAt;
    private bool _isTestRunning;
    private int _testResult;
    private DateTime? _testStartTime;
    private System.Threading.Timer? _testTimer;

    public event Action? OnChanged;
    public event Action? OnCleared;
    public event Action? OnTestTimerTick;

    public string? SerialNumber
    {
        get
        {
            lock (_lock)
            {
                return _serialNumber;
            }
        }
    }

    public string? Article
    {
        get
        {
            lock (_lock)
            {
                return _article;
            }
        }
    }

    public bool IsValid
    {
        get
        {
            lock (_lock)
            {
                return _isValid;
            }
        }
    }

    public BoilerTypeCycle? BoilerTypeCycle
    {
        get
        {
            lock (_lock)
            {
                return _boilerTypeCycle;
            }
        }
    }

    public IReadOnlyList<RecipeResponseDto>? Recipes
    {
        get
        {
            lock (_lock)
            {
                return _recipes;
            }
        }
    }

    /// <summary>
    /// Серийный номер котла из предыдущего теста.
    /// Сохраняется при вызове Clear() для возможности отслеживания.
    /// </summary>
    public string? LastSerialNumber
    {
        get
        {
            lock (_lock)
            {
                return _lastSerialNumber;
            }
        }
    }

    /// <summary>
    /// Время завершения предыдущего теста.
    /// Сохраняется при вызове Clear() вместе с LastSerialNumber.
    /// </summary>
    public DateTime? LastTestCompletedAt
    {
        get
        {
            lock (_lock)
            {
                return _lastTestCompletedAt;
            }
        }
    }

    public bool IsTestRunning
    {
        get
        {
            lock (_lock)
            {
                return _isTestRunning;
            }
        }
    }

    public int TestResult
    {
        get
        {
            lock (_lock)
            {
                return _testResult;
            }
        }
    }

    public void SetTestResult(int value)
    {
        lock (_lock)
        {
            _testResult = value;
        }
        NotifyChanged();
    }

    public void SetTestRunning(bool value)
    {
        lock (_lock)
        {
            _isTestRunning = value;
        }
        NotifyChanged();
    }

    public void StartTestTimer()
    {
        lock (_lock)
        {
            _testStartTime = DateTime.Now;
            _testTimer?.Dispose();
            _testTimer = new System.Threading.Timer(OnTimerTick, null, 0, 1000);
        }
    }

    public void StopTestTimer()
    {
        lock (_lock)
        {
            _testTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public TimeSpan GetTestDuration()
    {
        lock (_lock)
        {
            return _testStartTime.HasValue
                ? DateTime.Now - _testStartTime.Value
                : TimeSpan.Zero;
        }
    }

    public string GetTestDurationFormatted()
    {
        var duration = GetTestDuration();
        return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
    }

    private void OnTimerTick(object? state)
    {
        OnTestTimerTick?.Invoke();
    }

    public void SetData(
        string serialNumber,
        string article,
        bool isValid,
        BoilerTypeCycle? boilerTypeCycle = null,
        IReadOnlyList<RecipeResponseDto>? recipes = null)
    {
        UpdateState(serialNumber, article, isValid, boilerTypeCycle, recipes);
        NotifyChanged();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _lastSerialNumber = _serialNumber;
            _lastTestCompletedAt = DateTime.Now;
            _isTestRunning = false;
            _testTimer?.Dispose();
            _testTimer = null;
            _testStartTime = null;
        }
        UpdateState(serialNumber: null, article: null, isValid: false, boilerTypeCycle: null, recipes: null);
        _recipeProvider.Clear();
        NotifyChanged();
        OnCleared?.Invoke();
    }

    /// <summary>
    /// Сохраняет текущий серийный номер и время как "последний тест".
    /// Вызывается перед очисткой шагов для сохранения истории.
    /// </summary>
    public void SaveLastTestInfo()
    {
        lock (_lock)
        {
            _lastSerialNumber = _serialNumber;
            _lastTestCompletedAt = DateTime.Now;
        }
        NotifyChanged();
    }

    /// <summary>
    /// Очищает информацию о предыдущем тесте.
    /// Вызывается перед стартом нового теста.
    /// </summary>
    public void ClearLastTestInfo()
    {
        lock (_lock)
        {
            _lastSerialNumber = null;
            _lastTestCompletedAt = null;
        }
        NotifyChanged();
    }

    private void UpdateState(
        string? serialNumber,
        string? article,
        bool isValid,
        BoilerTypeCycle? boilerTypeCycle,
        IReadOnlyList<RecipeResponseDto>? recipes)
    {
        lock (_lock)
        {
            _serialNumber = serialNumber;
            _article = article;
            _isValid = isValid;
            _boilerTypeCycle = boilerTypeCycle;
            _recipes = recipes;
        }
    }

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }
}
