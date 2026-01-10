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

    public void Add(string parameterName, string value, string tolerances, string unit)
    {
        lock (_lock)
        {
            var item = new TestResultItem
            {
                Time = DateTime.Now,
                ParameterName = parameterName,
                Value = value,
                Tolerances = tolerances,
                Unit = unit
            };
            _results.Add(item);
        }

        OnChanged?.Invoke();
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
