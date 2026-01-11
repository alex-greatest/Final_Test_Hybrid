using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class TestSequenseService
{
    private static class ScanModuleNames
    {
        public const string BarcodeScanner = "Сканирование штрихкода";
        public const string BarcodeScannerMes = "Сканирование штрихкода MES";
    }

    private readonly List<TestSequenseData> _steps = [];
    private readonly Lock _lock = new();
    public event Action? OnDataChanged;
    public IEnumerable<TestSequenseData> Data => GetStepsCopy();
    public int Count => GetCount();

    public bool IsOnActiveScanStep
    {
        get
        {
            lock (_lock)
            {
                return IsFirstStepActiveScan();
            }
        }
    }

    private bool IsFirstStepActiveScan()
    {
        var step = _steps.FirstOrDefault();
        if (step == null)
        {
            return false;
        }
        return IsScanModule(step.Module) && step.StepStatus != TestStepStatus.Success;
    }

    private static bool IsScanModule(string moduleName)
    {
        return moduleName is ScanModuleNames.BarcodeScanner or ScanModuleNames.BarcodeScannerMes;
    }

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
            Status = "Выполняется",
            StartTime = DateTime.Now
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
        UpdateStepAndNotify(id, step =>
        {
            step.Status = "Выполняется";
            step.StepStatus = TestStepStatus.Running;
            step.Result = "";
            step.StartTime = DateTime.Now;
            step.EndTime = null;
        });
    }

    public void SetSuccess(Guid id, string message = "", string? limits = null)
    {
        UpdateStepAndNotify(id, step =>
        {
            step.Status = "Готово";
            step.StepStatus = TestStepStatus.Success;
            step.Result = message;
            step.Range = limits ?? "";
            step.EndTime = DateTime.Now;
        });
    }

    public void SetError(Guid id, string errorMessage, string? limits = null)
    {
        UpdateStepAndNotify(id, step =>
        {
            step.Status = "Ошибка";
            step.StepStatus = TestStepStatus.Error;
            step.Result = errorMessage;
            step.Range = limits ?? "";
            step.EndTime = DateTime.Now;
        });
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _steps.Clear();
        }
        NotifyDataChanged();
    }

    private void UpdateStepAndNotify(Guid id, Action<TestSequenseData> updateAction)
    {
        var updated = TryUpdateStep(id, updateAction);
        if (updated)
        {
            NotifyDataChanged();
        }
    }

    private TestSequenseData CreateStepData(ITestStep step)
    {
        return new TestSequenseData
        {
            Module = step.Name,
            Description = step.Description,
            Status = "Выполняется",
            StartTime = DateTime.Now
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
                StepStatus = s.StepStatus,
                StartTime = s.StartTime,
                EndTime = s.EndTime
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
