using System.Collections.Concurrent;
using Final_Test_Hybrid.Models;

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

    public int Count => _data.Count;
}
