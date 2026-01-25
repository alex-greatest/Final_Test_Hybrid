using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Export;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class TestSequenseService(
    StepHistoryService stepHistoryService,
    StepHistoryExcelExporter stepHistoryExcelExporter,
    BoilerState boilerState)
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

    /// <summary>
    /// Добавляет шаг в последовательность.
    /// </summary>
    public Guid AddStep(ITestStep step)
    {
        return AddStep(step, limits: null);
    }

    /// <summary>
    /// Добавляет шаг в последовательность с предзаданными пределами.
    /// </summary>
    /// <param name="step">Шаг теста.</param>
    /// <param name="limits">Предзаданные пределы для отображения в гриде.</param>
    public Guid AddStep(ITestStep step, string? limits)
    {
        var stepData = CreateStepData(step, limits);
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

    /// <summary>
    /// Устанавливает статус "Выполняется" для шага.
    /// Range НЕ сбрасывается для сохранения предзаданных пределов при retry.
    /// </summary>
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

    /// <summary>
    /// Устанавливает статус "Готово" для шага.
    /// </summary>
    /// <param name="id">Идентификатор шага.</param>
    /// <param name="message">Сообщение результата.</param>
    /// <param name="limits">Пределы. Если null - сохраняются предзаданные пределы.</param>
    public void SetSuccess(Guid id, string message = "", string? limits = null)
    {
        UpdateStepAndNotify(id, step =>
        {
            step.Status = "Готово";
            step.StepStatus = TestStepStatus.Success;
            step.Result = message;
            if (limits != null)
            {
                step.Range = limits;
            }
            step.EndTime = DateTime.Now;
        });
    }

    /// <summary>
    /// Устанавливает статус "Ошибка" для шага.
    /// </summary>
    /// <param name="id">Идентификатор шага.</param>
    /// <param name="errorMessage">Сообщение об ошибке.</param>
    /// <param name="limits">Пределы. Если null - сохраняются предзаданные пределы.</param>
    public void SetError(Guid id, string errorMessage, string? limits = null)
    {
        UpdateStepAndNotify(id, step =>
        {
            step.Status = "Ошибка";
            step.StepStatus = TestStepStatus.Error;
            step.Result = errorMessage;
            if (limits != null)
            {
                step.Range = limits;
            }
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

    public void ClearAllExceptScan()
    {
        boilerState.SaveLastTestInfo();
        var stepsCopy = GetStepsCopy();
        var testSequenseDatas = stepsCopy as TestSequenseData[] ?? stepsCopy.ToArray();
        stepHistoryService.CaptureSnapshot(testSequenseDatas);
        stepHistoryExcelExporter.ExportIfEnabledAsync(testSequenseDatas);

        lock (_lock)
        {
            _steps.RemoveAll(s => !IsScanModule(s.Module));
            ResetScanStepToRunning();
        }
        NotifyDataChanged();
    }

    private void ResetScanStepToRunning()
    {
        var scanStep = _steps.FirstOrDefault(s => IsScanModule(s.Module));
        if (scanStep == null)
        {
            return;
        }
        scanStep.Status = "Выполняется";
        scanStep.StepStatus = TestStepStatus.Running;
        scanStep.Result = "";
        scanStep.Range = "";
        scanStep.StartTime = DateTime.Now;
        scanStep.EndTime = null;
    }

    public void UpdateScanStep(TestStepStatus status, string message, string? limits = null)
    {
        lock (_lock)
        {
            var scanStep = _steps.FirstOrDefault(s => IsScanModule(s.Module));
            if (scanStep == null)
            {
                return;
            }
            ApplyScanStepUpdate(scanStep, status, message, limits);
        }
        NotifyDataChanged();
    }

    public void MutateScanStep(string newModule, string newDescription)
    {
        lock (_lock)
        {
            var scanStep = _steps.FirstOrDefault(s => IsScanModule(s.Module));
            if (scanStep == null)
            {
                return;
            }
            scanStep.Module = newModule;
            scanStep.Description = newDescription;
            scanStep.Status = "Выполняется";
            scanStep.StepStatus = TestStepStatus.Running;
            scanStep.Result = "";
            scanStep.Range = "";
            scanStep.StartTime = DateTime.Now;
            scanStep.EndTime = null;
        }
        NotifyDataChanged();
    }

    public Guid EnsureScanStepExists(string moduleName, string description)
    {
        lock (_lock)
        {
            var existing = _steps.FirstOrDefault(s => IsScanModule(s.Module));
            if (existing != null)
            {
                return existing.Id;
            }
            var stepData = new TestSequenseData
            {
                Module = moduleName,
                Description = description,
                Status = "Выполняется",
                StartTime = DateTime.Now
            };
            _steps.Insert(0, stepData);
            NotifyDataChanged();
            return stepData.Id;
        }
    }

    private static void ApplyScanStepUpdate(TestSequenseData step, TestStepStatus status, string message, string? limits)
    {
        step.StepStatus = status;
        step.Result = message;
        step.Range = limits ?? "";
        step.Status = status switch
        {
            TestStepStatus.Running => "Выполняется",
            TestStepStatus.Success => "Готово",
            TestStepStatus.Error => "Ошибка",
            _ => step.Status
        };
        if (status is TestStepStatus.Success or TestStepStatus.Error)
        {
            step.EndTime = DateTime.Now;
        }
    }

    private void UpdateStepAndNotify(Guid id, Action<TestSequenseData> updateAction)
    {
        var updated = TryUpdateStep(id, updateAction);
        if (updated)
        {
            NotifyDataChanged();
        }
    }

    /// <summary>
    /// Создаёт данные шага для отображения в гриде.
    /// </summary>
    private static TestSequenseData CreateStepData(ITestStep step, string? limits)
    {
        return new TestSequenseData
        {
            Module = step.Name,
            Description = step.Description,
            Status = "Выполняется",
            Range = limits ?? "",
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
