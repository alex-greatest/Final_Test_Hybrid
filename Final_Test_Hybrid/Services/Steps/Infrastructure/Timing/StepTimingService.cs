using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public interface IStepTimingService
{
    event Action? OnChanged;
    void Record(string name, string description, TimeSpan duration);
    IReadOnlyList<StepTimingRecord> GetAll();
    void Clear();
}

public class StepTimingService : IStepTimingService
{
    private readonly List<StepTimingRecord> _records = [];
    private readonly Lock _lock = new();

    public event Action? OnChanged;

    public void Record(string name, string description, TimeSpan duration)
    {
        var formatted = $"{(int)duration.TotalMinutes:D2}.{duration.Seconds:D2}";
        lock (_lock)
        {
            _records.Add(new StepTimingRecord(name, description, formatted));
        }
        OnChanged?.Invoke();
    }

    public IReadOnlyList<StepTimingRecord> GetAll()
    {
        lock (_lock) { return [.. _records]; }
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
}
