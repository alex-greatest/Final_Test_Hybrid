using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public interface IStepTimingService
{
    event Action? OnChanged;

    IReadOnlyList<StepTimingRecord> GetAll();
    void Clear(bool preserveScanState = false);

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

    private (string Name, string Description)? _scanStepInfo;
    private readonly TimingState _scanState = new();
    private readonly TimingState[] _columnStates = CreateColumnStates();
    private Guid? _scanPausedByGlobalPauseId;
    private readonly Guid?[] _columnsPausedByGlobalPauseIds = new Guid?[ColumnCount];

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
            var result = new List<StepTimingRecord>(_records.Count + ColumnCount + 1);
            if (_scanState.IsActive)
            {
                result.Add(CreateRecord(_scanState));
            }
            result.AddRange(_records);
            result.AddRange(_columnStates.Where(state => state.IsActive).Select(CreateRecord));
            return result;
        }
    }

    public void Clear(bool preserveScanState = false)
    {
        bool hadChanges;
        lock (_lock)
        {
            hadChanges = _records.Count > 0 || _scanState.IsActive || _columnStates.Any(state => state.IsActive);
            _records.Clear();
            foreach (var state in _columnStates)
            {
                state.Clear();
            }

            if (_scanState.IsActive)
            {
                if (preserveScanState)
                {
                    _scanState.Pause();
                }
                else
                {
                    _scanState.Clear();
                }
            }

            _scanPausedByGlobalPauseId = null;
            Array.Clear(_columnsPausedByGlobalPauseIds, 0, _columnsPausedByGlobalPauseIds.Length);
        }
        UpdateTimerState();
        if (hadChanges)
        {
            OnChanged?.Invoke();
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private bool AnyRunning => _scanState.IsRunning || _columnStates.Any(state => state.IsRunning);

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

    private bool PauseAllActiveLocked()
    {
        var hadRunning = false;
        if (_scanState.IsRunning)
        {
            _scanPausedByGlobalPauseId = _scanState.Id;
            _scanState.Pause();
            hadRunning = true;
        }

        for (var i = 0; i < _columnStates.Length; i++)
        {
            var state = _columnStates[i];
            if (!state.IsRunning)
            {
                continue;
            }
            _columnsPausedByGlobalPauseIds[i] = state.Id;
            state.Pause();
            hadRunning = true;
        }

        return hadRunning;
    }

    private bool ResumeAllActiveLocked()
    {
        var hadResumed = false;
        if (_scanPausedByGlobalPauseId.HasValue)
        {
            hadResumed |= TryResumeIfMatch(_scanState, _scanPausedByGlobalPauseId.Value);
            _scanPausedByGlobalPauseId = null;
        }

        for (var i = 0; i < _columnStates.Length; i++)
        {
            var resumeId = _columnsPausedByGlobalPauseIds[i];
            if (!resumeId.HasValue)
            {
                continue;
            }

            hadResumed |= TryResumeIfMatch(_columnStates[i], resumeId.Value);
            _columnsPausedByGlobalPauseIds[i] = null;
        }

        return hadResumed;
    }

    private static bool TryResumeIfMatch(TimingState state, Guid expectedId)
    {
        if (!state.IsActive || state.IsRunning || state.Id != expectedId)
        {
            return false;
        }
        state.Resume();
        return true;
    }

    private void OnTimerTick(object? state) => OnChanged?.Invoke();

    private static StepTimingRecord CreateRecord(TimingState state) =>
        new(state.Id, state.Name!, state.Description!, FormatDuration(state.CalculateDuration()), state.IsRunning);

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalMinutes:D2}.{duration.Seconds:D2}";
}
