using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public partial class StepTimingService
{
    public void AddCompletedStepTiming(string name, string description, TimeSpan duration)
    {
        lock (_lock)
        {
            _records.Add(new StepTimingRecord(Guid.NewGuid(), name, description, FormatDuration(duration), false));
        }

        OnChanged?.Invoke();
    }
}
