using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public partial class StepTimingService
{
    public void StartColumnStepTiming(int columnIndex, string name, string description)
    {
        lock (_lock)
        {
            _columnStates[columnIndex].Start(name, description);
        }
        StartTimer();
        OnChanged?.Invoke();
    }

    public void StopColumnStepTiming(int columnIndex)
    {
        lock (_lock)
        {
            var state = _columnStates[columnIndex];
            if (!state.IsActive)
            {
                return;
            }

            var duration = state.CalculateDuration();
            _records.Add(new StepTimingRecord(state.Id, state.Name!, state.Description!, FormatDuration(duration)));

            state.Clear();
        }
        UpdateTimerState();
        OnChanged?.Invoke();
    }

    public void PauseAllColumnsTiming()
    {
        lock (_lock)
        {
            foreach (var state in _columnStates)
            {
                state.Pause();
            }
        }
        UpdateTimerState();
        OnChanged?.Invoke();
    }

    public void ResumeAllColumnsTiming()
    {
        lock (_lock)
        {
            foreach (var state in _columnStates)
            {
                state.Resume();
            }
        }
        UpdateTimerState();
        OnChanged?.Invoke();
    }

    public void StartCurrentStepTiming(string name, string description) =>
        StartColumnStepTiming(0, name, description);

    public void StopCurrentStepTiming() =>
        StopColumnStepTiming(0);
}
