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

    // Методы для шагов колонок (4 параллельных таймера)
    void StartColumnStepTiming(int columnIndex, string name, string description);
    void StopColumnStepTiming(int columnIndex);
    void PauseAllColumnsTiming();
    void ResumeAllColumnsTiming();

    // Методы для pre-execution шагов (используют колонку 0, т.к. выполняются до параллельных колонок)
    void StartCurrentStepTiming(string name, string description);
    void StopCurrentStepTiming();
}

public partial class StepTimingService : IStepTimingService, IDisposable
{
    private const int ColumnCount = 4;

    private readonly List<StepTimingRecord> _records = [];
    private readonly Lock _lock = new();
    private readonly System.Threading.Timer _timer;

    private readonly TimingState _scanState = new();
    private readonly TimingState[] _columnStates = CreateColumnStates();

    private static TimingState[] CreateColumnStates()
    {
        var states = new TimingState[ColumnCount];
        for (var i = 0; i < ColumnCount; i++)
        {
            states[i] = new TimingState();
        }
        return states;
    }

    public event Action? OnChanged;

    public StepTimingService()
    {
        _timer = new System.Threading.Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public IReadOnlyList<StepTimingRecord> GetAll()
    {
        lock (_lock)
        {
            var result = new List<StepTimingRecord>();
            if (_scanState.IsActive)
            {
                result.Add(CreateRecord(_scanState));
            }
            result.AddRange(_records);
            result.AddRange(from state in _columnStates where state.IsActive select CreateRecord(state));
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

    public void Dispose()
    {
        _timer.Dispose();
    }

    private bool AnyRunning => _scanState.IsRunning || _columnStates.Any(s => s.IsRunning);

    private void StartTimer() => _timer.Change(0, 1000);

    private void StopTimer() => _timer.Change(Timeout.Infinite, Timeout.Infinite);

    private void UpdateTimerState()
    {
        if (AnyRunning)
        {
            StartTimer();
        }
        else
        {
            StopTimer();
        }
    }

    private void OnTimerTick(object? state) => OnChanged?.Invoke();

    private static StepTimingRecord CreateRecord(TimingState state) =>
        new(state.Id, state.Name!, state.Description!, FormatDuration(state.CalculateDuration()));

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalMinutes:D2}.{duration.Seconds:D2}";
}
