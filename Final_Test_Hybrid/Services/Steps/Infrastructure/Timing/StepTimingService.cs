using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;

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

    // Методы для шагов колонок (4 параллельных таймера)
    void StartColumnStepTiming(int columnIndex, string name, string description);
    void StopColumnStepTiming(int columnIndex);
    void PauseAllColumnsTiming();
    void ResumeAllColumnsTiming();

    // Методы для pre-execution шагов (используют колонку 0, т.к. выполняются до параллельных колонок)
    void StartCurrentStepTiming(string name, string description);
    void StopCurrentStepTiming();
}

public class StepTimingService : IStepTimingService, IDisposable
{
    private const int ColumnCount = 4;

    private readonly List<StepTimingRecord> _records = [];
    private readonly Lock _lock = new();
    private readonly System.Threading.Timer _timer;
    private readonly DualLogger<StepTimingService> 
        _logger;

    // Состояние шага сканирования
    private readonly Guid _scanId = Guid.NewGuid();
    private string? _scanName;
    private string? _scanDescription;
    private DateTime _scanStartTime;
    private bool _scanIsRunning;
    private TimeSpan _scanFrozenDuration;

    // Состояние шагов колонок (4 параллельных)
    private readonly ColumnTimingState[] _columnStates = CreateColumnStates();

    private static ColumnTimingState[] CreateColumnStates()
    {
        var states = new ColumnTimingState[ColumnCount];
        for (var i = 0; i < ColumnCount; i++)
        {
            states[i] = new ColumnTimingState();
        }
        return states;
    }

    private class ColumnTimingState
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsRunning { get; set; }
        public TimeSpan AccumulatedDuration { get; set; }
    }

    public event Action? OnChanged;

    public StepTimingService(DualLogger<StepTimingService> logger)
    {
        _logger = logger;
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
            if (_scanName == null)
            {
                return;
            }
            _scanStartTime = DateTime.Now;
            _scanIsRunning = true;
            _scanFrozenDuration = TimeSpan.Zero;
        }

        _timer.Change(0, 1000);
        OnChanged?.Invoke();
    }

    public void StartColumnStepTiming(int columnIndex, string name, string description)
    {
        bool shouldStartTimer;
        lock (_lock)
        {
            var state = _columnStates[columnIndex];
            state.Name = name;
            state.Description = description;
            state.StartTime = DateTime.Now;
            state.IsRunning = true;
            state.AccumulatedDuration = TimeSpan.Zero;
            shouldStartTimer = true; // Всегда запускаем, т.к. только что установили IsRunning = true
        }
        if (shouldStartTimer)
        {
            _timer.Change(0, 1000);
        }
        OnChanged?.Invoke();
    }

    public void StopColumnStepTiming(int columnIndex)
    {
        bool shouldStopTimer;
        lock (_lock)
        {
            var state = _columnStates[columnIndex];
            if (state.Name == null)
            {
                return;
            }

            var duration = state.IsRunning
                ? state.AccumulatedDuration + (DateTime.Now - state.StartTime)
                : state.AccumulatedDuration;
            _records.Add(new StepTimingRecord(Guid.NewGuid(), state.Name, state.Description!, FormatDuration(duration)));

            state.Name = null;
            state.Description = null;
            state.IsRunning = false;
            state.AccumulatedDuration = TimeSpan.Zero;

            shouldStopTimer = !_scanIsRunning && !AnyColumnRunning;
        }
        if (shouldStopTimer)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        OnChanged?.Invoke();
    }

    public void PauseAllColumnsTiming()
    {
        bool shouldStopTimer;
        lock (_lock)
        {
            foreach (var state in _columnStates)
            {
                if (!state.IsRunning)
                {
                    continue;
                }
                state.AccumulatedDuration += DateTime.Now - state.StartTime;
                state.IsRunning = false;
            }
            shouldStopTimer = !_scanIsRunning && !AnyColumnRunning;
        }
        if (shouldStopTimer)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        OnChanged?.Invoke();
    }

    public void ResumeAllColumnsTiming()
    {
        bool shouldStartTimer;
        lock (_lock)
        {
            foreach (var state in _columnStates)
            {
                if (state.IsRunning || state.Name == null)
                {
                    continue;
                }
                state.StartTime = DateTime.Now;
                state.IsRunning = true;
            }
            shouldStartTimer = _scanIsRunning || AnyColumnRunning;
        }
        if (shouldStartTimer)
        {
            _timer.Change(0, 1000);
        }
        OnChanged?.Invoke();
    }

    // Методы для pre-execution шагов (делегируют в колонку 0)
    public void StartCurrentStepTiming(string name, string description) =>
        StartColumnStepTiming(0, name, description);

    public void StopCurrentStepTiming() =>
        StopColumnStepTiming(0);

    private bool AnyColumnRunning => _columnStates.Any(s => s.IsRunning);

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
            result.AddRange(_records);
            // Все активные колонки (и на паузе тоже)
            foreach (var state in _columnStates)
            {
                if (state.Name == null)
                {
                    continue;
                }
                var duration = state.IsRunning
                    ? state.AccumulatedDuration + (DateTime.Now - state.StartTime)
                    : state.AccumulatedDuration;
                result.Add(new StepTimingRecord(state.Id, state.Name, state.Description!, FormatDuration(duration)));
            }
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
