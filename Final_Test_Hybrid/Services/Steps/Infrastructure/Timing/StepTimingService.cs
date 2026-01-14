using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public interface IStepTimingService
{
    event Action? OnChanged;

    IReadOnlyList<StepTimingRecord> GetAll();
    void Clear();

    // Методы для шага сканирования (таймер в реальном времени)
    void StartScanTiming(string name, string description);
    void StopScanTiming();
    void ResetScanTiming();

    // Методы для текущего шага (таймер в реальном времени)
    void StartCurrentStepTiming(string name, string description);
    void StopCurrentStepTiming();
}

public class StepTimingService : IStepTimingService, IDisposable
{
    private readonly List<StepTimingRecord> _records = [];
    private readonly Lock _lock = new();
    private readonly System.Threading.Timer _timer;

    // Состояние шага сканирования
    private readonly Guid _scanId = Guid.NewGuid();
    private string? _scanName;
    private string? _scanDescription;
    private DateTime _scanStartTime;
    private bool _scanIsRunning;
    private TimeSpan _scanFrozenDuration;

    // Состояние текущего шага
    private readonly Guid _currentId = Guid.NewGuid();
    private string? _currentName;
    private string? _currentDescription;
    private DateTime _currentStartTime;
    private bool _currentIsRunning;

    public event Action? OnChanged;

    public StepTimingService()
    {
        _timer = new System.Threading.Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void StartScanTiming(string name, string description)
    {
        lock (_lock)
        {
            _scanName = name;
            _scanDescription = description;
            _scanStartTime = DateTime.Now;
            _scanIsRunning = true;
            _scanFrozenDuration = TimeSpan.Zero;
        }

        _timer.Change(0, 1000);
        OnChanged?.Invoke();
    }

    public void StopScanTiming()
    {
        lock (_lock)
        {
            if (!_scanIsRunning) return;

            _scanFrozenDuration = DateTime.Now - _scanStartTime;
            _scanIsRunning = false;
        }

        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        OnChanged?.Invoke();
    }

    public void ResetScanTiming()
    {
        lock (_lock)
        {
            if (_scanName == null) return;

            _scanStartTime = DateTime.Now;
            _scanIsRunning = true;
            _scanFrozenDuration = TimeSpan.Zero;
        }

        _timer.Change(0, 1000);
        OnChanged?.Invoke();
    }

    public void StartCurrentStepTiming(string name, string description)
    {
        lock (_lock)
        {
            _currentName = name;
            _currentDescription = description;
            _currentStartTime = DateTime.Now;
            _currentIsRunning = true;
        }

        StartTimerIfNeeded();
        OnChanged?.Invoke();
    }

    public void StopCurrentStepTiming()
    {
        lock (_lock)
        {
            if (!_currentIsRunning) return;

            var duration = DateTime.Now - _currentStartTime;
            _records.Add(new StepTimingRecord(Guid.NewGuid(), _currentName!, _currentDescription!, FormatDuration(duration)));
            _currentIsRunning = false;
            _currentName = null;
            _currentDescription = null;
        }

        StopTimerIfNotNeeded();
        OnChanged?.Invoke();
    }

    private void StartTimerIfNeeded()
    {
        if (_scanIsRunning || _currentIsRunning)
        {
            _timer.Change(0, 1000);
        }
    }

    private void StopTimerIfNotNeeded()
    {
        if (!_scanIsRunning && !_currentIsRunning)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void OnTimerTick(object? state)
    {
        OnChanged?.Invoke();
    }

    public IReadOnlyList<StepTimingRecord> GetAll()
    {
        lock (_lock)
        {
            var result = new List<StepTimingRecord>();

            // Шаг сканирования всегда первый
            if (_scanName != null)
            {
                var duration = _scanIsRunning
                    ? DateTime.Now - _scanStartTime
                    : _scanFrozenDuration;
                result.Add(new StepTimingRecord(_scanId, _scanName, _scanDescription!, FormatDuration(duration)));
            }

            // Текущий выполняемый шаг
            if (_currentIsRunning && _currentName != null)
            {
                var duration = DateTime.Now - _currentStartTime;
                result.Add(new StepTimingRecord(_currentId, _currentName, _currentDescription!, FormatDuration(duration)));
            }

            result.AddRange(_records);
            return result;
        }
    }

    public void Clear()
    {
        bool hadItems;
        lock (_lock)
        {
            hadItems = _records.Count > 0;
            _records.Clear();
        }
        if (hadItems)
        {
            OnChanged?.Invoke();
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalMinutes:D2}.{duration.Seconds:D2}";
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
