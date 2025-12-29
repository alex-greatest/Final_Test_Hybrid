using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class TestSequenseService
{
    private readonly List<TestSequenseData> _steps = [];
    private readonly Lock _lock = new();
    public event Action? OnDataChanged;
    public IEnumerable<TestSequenseData> Data => GetStepsCopy();
    public int Count => GetCount();

    public Guid AddStep(ITestStep step)
    {
        var stepData = CreateStepData(step);
        lock (_lock)
        {
            _steps.Add(stepData);
        }
        NotifyDataChanged();
        return stepData.Id;
    }

    public Guid AddStep(string name, string description)
    {
        var stepData = new TestSequenseData
        {
            Module = name,
            Description = description,
            Status = "Выполняется"
        };
        lock (_lock)
        {
            _steps.Add(stepData);
        }
        NotifyDataChanged();
        return stepData.Id;
    }

    public void SetRunning(Guid id)
    {
        var updated = TryUpdateStep(id, step =>
        {
            step.Status = "Выполняется";
            step.IsError = false;
            step.IsSuccess = false;
            step.Result = "";
        });
        if (updated)
        {
            NotifyDataChanged();
        }
    }

    public void SetSuccess(Guid id, string message = "")
    {
        var updated = TryUpdateStep(id, step =>
        {
            step.Status = "Готово";
            step.IsSuccess = true;
            step.IsError = false;
            step.Result = message;
        });
        if (updated)
        {
            NotifyDataChanged();
        }
    }

    public void SetError(Guid id, string errorMessage)
    {
        var updated = TryUpdateStep(id, step =>
        {
            step.Status = "Ошибка";
            step.IsError = true;
            step.IsSuccess = false;
            step.Result = errorMessage;
        });
        if (updated)
        {
            NotifyDataChanged();
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _steps.Clear();
        }
        NotifyDataChanged();
    }

    private TestSequenseData CreateStepData(ITestStep step)
    {
        return new TestSequenseData
        {
            Module = step.Name,
            Description = step.Description,
            Status = "Выполняется"
        };
    }

    private bool TryUpdateStep(Guid id, Action<TestSequenseData> updateAction)
    {
        lock (_lock)
        {
            var step = _steps.FirstOrDefault(s => s.Id == id);
            if (step == null)
            {
                return false;
            }
            updateAction(step);
            return true;
        }
    }

    private IEnumerable<TestSequenseData> GetStepsCopy()
    {
        lock (_lock)
        {
            return _steps.Select(s => new TestSequenseData
            {
                Id = s.Id,
                Module = s.Module,
                Description = s.Description,
                Status = s.Status,
                Result = s.Result,
                Range = s.Range,
                IsError = s.IsError,
                IsSuccess = s.IsSuccess
            }).ToList();
        }
    }

    private int GetCount()
    {
        lock (_lock)
        {
            return _steps.Count;
        }
    }

    private void NotifyDataChanged()
    {
        OnDataChanged?.Invoke();
    }
}
