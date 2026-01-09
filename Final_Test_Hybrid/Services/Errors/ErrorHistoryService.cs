using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Errors;

public sealed class ErrorHistoryService : IErrorHistoryService
{
    private readonly Lock _lock = new();
    private readonly List<ErrorHistoryItem> _history = [];

    public event Action? OnChanged;

    public IReadOnlyList<ErrorHistoryItem> GetHistory()
    {
        lock (_lock)
        {
            return _history.ToList();
        }
    }

    public void Add(string code, string description, string testName)
    {
        lock (_lock)
        {
            var item = new ErrorHistoryItem
            {
                StartTime = DateTime.Now,
                Code = code,
                Description = description,
                TestName = testName
            };
            _history.Add(item);
        }

        OnChanged?.Invoke();
    }

    public void MarkResolved(string code)
    {
        var found = false;

        lock (_lock)
        {
            var item = _history.FirstOrDefault(e => e.Code == code && e.EndTime == null);
            if (item != null)
            {
                var index = _history.IndexOf(item);
                _history[index] = item with { EndTime = DateTime.Now };
                found = true;
            }
        }

        if (found)
        {
            OnChanged?.Invoke();
        }
    }

    public void Clear()
    {
        var hadItems = false;

        lock (_lock)
        {
            hadItems = _history.Count > 0;
            _history.Clear();
        }

        if (hadItems)
        {
            OnChanged?.Invoke();
        }
    }
}
