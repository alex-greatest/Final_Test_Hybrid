using Final_Test_Hybrid.Models;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public class TimerService : ITimerService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DateTime> _timers = [];

    public event Action? OnChanged;

    public TimerService(BoilerState boilerState)
    {
        boilerState.OnCleared += Clear;
    }

    public void Start(string key)
    {
        lock (_lock) { _timers[key] = DateTime.UtcNow; }
        OnChanged?.Invoke();
    }

    public TimeSpan? Stop(string key)
    {
        TimeSpan? elapsed;
        lock (_lock)
        {
            if (!_timers.TryGetValue(key, out var startTime))
                return null;
            elapsed = DateTime.UtcNow - startTime;
            _timers.Remove(key);
        }
        OnChanged?.Invoke();
        return elapsed;
    }

    public TimeSpan? GetElapsed(string key)
    {
        lock (_lock)
        {
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
            var now = DateTime.UtcNow;
            return _timers.ToDictionary(kvp => kvp.Key, kvp => now - kvp.Value);
        }
    }

    public void Clear()
    {
        bool hadTimers;
        lock (_lock)
        {
            hadTimers = _timers.Count > 0;
            _timers.Clear();
        }
        if (hadTimers) OnChanged?.Invoke();
    }
}
