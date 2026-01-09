using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Errors;

public sealed class ActiveErrorsService : IActiveErrorsService
{
    private readonly Lock _lock = new();
    private readonly List<ActiveError> _errors = [];

    public event Action? OnChanged;

    public IReadOnlyList<ActiveError> GetErrors()
    {
        lock (_lock)
        {
            return _errors.ToList();
        }
    }

    public void Add(string code, string description, string testName)
    {
        lock (_lock)
        {
            var error = new ActiveError
            {
                Time = DateTime.Now,
                Code = code,
                Description = description,
                TestName = testName
            };
            _errors.Add(error);
        }

        OnChanged?.Invoke();
    }

    public void Remove(string code)
    {
        var removed = false;

        lock (_lock)
        {
            var index = _errors.FindIndex(e => e.Code == code);
            if (index >= 0)
            {
                _errors.RemoveAt(index);
                removed = true;
            }
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
            hadItems = _errors.Count > 0;
            _errors.Clear();
        }
        if (hadItems)
        {
            OnChanged?.Invoke();
        }
    }
}
