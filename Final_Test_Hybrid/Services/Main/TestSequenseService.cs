using System.Collections.Concurrent;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Steps;

namespace Final_Test_Hybrid.Services.Main;

public class TestSequenseService
{
    private readonly ConcurrentQueue<TestSequenseData> _data = new();

    public event Action? OnDataChanged;

    public IEnumerable<TestSequenseData> Data => _data;

    public void Enqueue(TestSequenseData item)
    {
        _data.Enqueue(item);
        OnDataChanged?.Invoke();
    }

    public bool TryDequeue(out TestSequenseData? item)
    {
        var result = _data.TryDequeue(out item);
        if (result)
        {
            OnDataChanged?.Invoke();
        }
        return result;
    }

    public void Clear()
    {
        while (_data.TryDequeue(out _)) { }
        OnDataChanged?.Invoke();
    }

    public void SetCurrentStep(ITestStep? step)
    {
        Clear();
        if (step == null)
        {
            return;
        }
        _data.Enqueue(new TestSequenseData
        {
            Module = step.Name,
            Description = step.Description,
            Status = "Выполняется",
            Result = "",
            Range = ""
        });
        OnDataChanged?.Invoke();
    }

    public void ClearCurrentStep() => Clear();

    public int Count => _data.Count;
}
