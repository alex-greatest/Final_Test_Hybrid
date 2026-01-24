using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

/// <summary>
/// Хранит снимок шагов последнего завершённого теста.
/// </summary>
public class StepHistoryService
{
    private readonly Lock _lock = new();
    private List<TestSequenseData> _snapshot = [];

    public event Action? OnChanged;

    public IReadOnlyList<TestSequenseData> Snapshot
    {
        get
        {
            lock (_lock)
            {
                return _snapshot.ToList();
            }
        }
    }

    /// <summary>
    /// Создаёт снимок шагов.
    /// </summary>
    public void CaptureSnapshot(IEnumerable<TestSequenseData> steps)
    {
        lock (_lock)
        {
            _snapshot = steps.Select(s => new TestSequenseData
            {
                Id = s.Id,
                Module = s.Module,
                Description = s.Description,
                Status = s.Status,
                Result = s.Result,
                Range = s.Range,
                StepStatus = s.StepStatus,
                StartTime = s.StartTime,
                EndTime = s.EndTime
            }).ToList();
        }
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Очищает снимок.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _snapshot.Clear();
        }
        OnChanged?.Invoke();
    }
}
