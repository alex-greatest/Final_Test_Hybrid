using Final_Test_Hybrid.Models.Results;

namespace Final_Test_Hybrid.Services.Results;

public sealed class TestResultsService : ITestResultsService
{
    private readonly Lock _lock = new();
    private readonly List<TestResultItem> _results = [];

    public event Action? OnChanged;

    public IReadOnlyList<TestResultItem> GetResults()
    {
        lock (_lock)
        {
            return _results.ToList();
        }
    }

    public void Add(string parameterName, string value, string min, string max, int status, bool isRanged, string unit, string test)
    {
        lock (_lock)
        {
            var item = new TestResultItem
            {
                Time = DateTime.Now,
                Test = test,
                ParameterName = parameterName,
                Value = value,
                Min = min,
                Max = max,
                Status = status,
                IsRanged = isRanged,
                Unit = unit
            };
            _results.Add(item);
        }

        OnChanged?.Invoke();
    }

    /// <summary>
    /// Удаляет результат по имени параметра.
    /// </summary>
    /// <param name="parameterName">Имя параметра для удаления.</param>
    public void Remove(string parameterName)
    {
        bool removed;
        lock (_lock)
        {
            removed = _results.RemoveAll(r => r.ParameterName == parameterName) > 0;
        }
        if (removed)
        {
            OnChanged?.Invoke();
        }
    }

    public void Clear()
    {
        bool hadItems;
        lock (_lock)
        {
            hadItems = _results.Count > 0;
            _results.Clear();
        }
        if (hadItems)
        {
            OnChanged?.Invoke();
        }
    }
}
