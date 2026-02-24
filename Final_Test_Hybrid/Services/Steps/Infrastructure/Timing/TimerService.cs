using Final_Test_Hybrid.Models;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public class TimerService : ITimerService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DateTime> _timers = [];
    private readonly Dictionary<string, TimeSpan> _frozenTimers = [];

    public event Action? OnChanged;

    public TimerService(BoilerState boilerState)
    {
        boilerState.OnCleared += StopAll;
    }

    public void Start(string key)
    {
        lock (_lock)
        {
            _timers[key] = DateTime.UtcNow;
            _frozenTimers.Remove(key);
        }
        OnChanged?.Invoke();
    }

    public TimeSpan? Stop(string key)
    {
        TimeSpan? elapsed;
        var changed = false;
        lock (_lock)
        {
            if (_timers.TryGetValue(key, out var startTime))
            {
                var currentElapsed = DateTime.UtcNow - startTime;
                elapsed = currentElapsed;
                _timers.Remove(key);
                _frozenTimers[key] = currentElapsed;
                changed = true;
            }
            else if (_frozenTimers.TryGetValue(key, out var frozen))
            {
                elapsed = frozen;
            }
            else
            {
                return null;
            }
        }

        if (changed)
        {
            OnChanged?.Invoke();
        }

        return elapsed;
    }

    public TimeSpan? GetElapsed(string key)
    {
        lock (_lock)
        {
            if (_frozenTimers.TryGetValue(key, out var frozen))
                return frozen;

            return _timers.TryGetValue(key, out var startTime)
                ? DateTime.UtcNow - startTime
                : null;
        }
    }

    public bool IsRunning(string key)
    {
        lock (_lock) { return _timers.ContainsKey(key); }
    }

    public IReadOnlyDictionary<string, TimeSpan> GetAllActive()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, TimeSpan>(_frozenTimers);
            var now = DateTime.UtcNow;
            foreach (var (key, startTime) in _timers)
            {
                result[key] = now - startTime;
            }
            return result;
        }
    }

    /// <summary>
    /// Замораживает все активные таймеры, сохраняя их текущие значения.
    /// Вызывается при завершении теста для отображения финальных значений оператору.
    /// </summary>
    public void StopAll()
    {
        lock (_lock)
        {
            if (_timers.Count == 0) return;

            var now = DateTime.UtcNow;
            foreach (var (key, startTime) in _timers)
            {
                _frozenTimers[key] = now - startTime;
            }
            _timers.Clear();
        }
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        bool hadTimers;
        lock (_lock)
        {
            hadTimers = _timers.Count > 0 || _frozenTimers.Count > 0;
            _timers.Clear();
            _frozenTimers.Clear();
        }
        if (hadTimers) OnChanged?.Invoke();
    }
}
