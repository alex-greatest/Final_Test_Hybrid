using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public interface IStepTimingService
{
    event Action? OnChanged;

    // Существующие методы для тестовых шагов
    void Record(string name, string description, TimeSpan duration);
    IReadOnlyList<StepTimingRecord> GetAll();
    void Clear();

    // Методы для шага сканирования (таймер в реальном времени)
    void StartScanTiming(string name, string description);
    void StopScanTiming();
    void ResetScanTiming();
}

public class StepTimingService : IStepTimingService, IDisposable
{
    private readonly List<StepTimingRecord> _records = [];
    private readonly Lock _lock = new();
    private readonly System.Threading.Timer _timer;

    // Состояние шага сканирования
    private string? _scanName;
    private string? _scanDescription;
    private DateTime _scanStartTime;
    private bool _scanIsRunning;
    private TimeSpan _scanFrozenDuration;

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

    private void OnTimerTick(object? state)
    {
        OnChanged?.Invoke();
    }

    public void Record(string name, string description, TimeSpan duration)
    {
        var formatted = FormatDuration(duration);
        lock (_lock)
        {
            _records.Add(new StepTimingRecord(name, description, formatted));
        }
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
                result.Add(new StepTimingRecord(_scanName, _scanDescription!, FormatDuration(duration)));
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
