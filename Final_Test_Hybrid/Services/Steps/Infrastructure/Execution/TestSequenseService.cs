using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class TestSequenseService
{
    private TestSequenseData? _currentStep;
    private readonly Lock _lock = new();
    public event Action? OnDataChanged;
    public IEnumerable<TestSequenseData> Data => GetCurrentStepAsList();
    public int Count => HasCurrentStep() ? 1 : 0;

    public void SetCurrentStep(ITestStep? step)
    {
        UpdateCurrentStep(CreateStepData(step));
        NotifyDataChanged();
    }

    public void SetErrorOnCurrent(string errorMessage)
    {
        var wasUpdated = TryMarkCurrentStepAsError(errorMessage);
        if (wasUpdated)
        {
            NotifyDataChanged();
        }
    }

    public void ClearCurrentStep()
    {
        UpdateCurrentStep(null);
        NotifyDataChanged();
    }

    private TestSequenseData? CreateStepData(ITestStep? step)
    {
        if (step == null)
        {
            return null;
        }
        return new TestSequenseData
        {
            Module = step.Name,
            Description = step.Description,
            Status = "Выполняется",
            Result = "",
            Range = ""
        };
    }

    private bool TryMarkCurrentStepAsError(string errorMessage)
    {
        lock (_lock)
        {
            if (_currentStep == null)
            {
                return false;
            }
            ApplyErrorState(_currentStep, errorMessage);
            return true;
        }
    }

    private void ApplyErrorState(TestSequenseData step, string errorMessage)
    {
        step.Status = "Ошибка";
        step.Result = errorMessage;
        step.IsError = true;
    }

    private void UpdateCurrentStep(TestSequenseData? step)
    {
        lock (_lock)
        {
            _currentStep = step;
        }
    }

    private IEnumerable<TestSequenseData> GetCurrentStepAsList()
    {
        lock (_lock)
        {
            if (_currentStep == null)
            {
                return [];
            }
            return [new TestSequenseData
            {
                Module = _currentStep.Module,
                Description = _currentStep.Description,
                Status = _currentStep.Status,
                Result = _currentStep.Result,
                Range = _currentStep.Range,
                IsError = _currentStep.IsError
            }];
        }
    }

    private bool HasCurrentStep()
    {
        lock (_lock)
        {
            return _currentStep != null;
        }
    }

    private void NotifyDataChanged()
    {
        OnDataChanged?.Invoke();
    }
}
