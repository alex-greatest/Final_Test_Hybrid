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
    private readonly List<TestSequenseData> _steps = [];
    private Guid? _scanStepId;
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
        var scanStep = GetScanStepUnsafe();
        if (scanStep == null)
        {
            return false;
        }

        var firstStep = _steps.FirstOrDefault();
        if (firstStep == null || firstStep.Id != scanStep.Id)
        {
            return false;
        }

        return scanStep.StepStatus != TestStepStatus.Success;
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
            step.ProgressMessage = "";
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
            step.ProgressMessage = "";
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
            step.ProgressMessage = "";
            if (limits != null)
            {
                step.Range = limits;
            }
            step.EndTime = DateTime.Now;
        });
    }

    /// <summary>
    /// Обновляет промежуточный прогресс шага без изменения статуса.
    /// Игнорируется если шаг уже завершён (Success/Error).
    /// </summary>
    public void SetProgress(Guid id, string message)
    {
        var updated = TrySetProgress(id, message);
        if (updated)
        {
            NotifyDataChanged();
        }
    }

    private bool TrySetProgress(Guid id, string message)
    {
        lock (_lock)
        {
            var step = _steps.FirstOrDefault(s => s.Id == id);
            if (step == null || step.StepStatus != TestStepStatus.Running)
            {
                return false;
            }
            step.ProgressMessage = message;
            return true;
        }
    }

    /// <summary>
    /// Устанавливает статус "Ошибка (Пропущен)" для шага.
    /// </summary>
    /// <param name="id">Идентификатор шага.</param>
    public void MarkAsSkipped(Guid id)
    {
        UpdateStepAndNotify(id, step =>
        {
            step.Status = "Ошибка (Пропущен)";
            step.IsSkipped = true;
        });
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _steps.Clear();
            _scanStepId = null;
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
            var scanStep = GetScanStepUnsafe();
            if (scanStep == null)
            {
                _steps.Clear();
                _scanStepId = null;
            }
            else
            {
                _steps.RemoveAll(s => s.Id != scanStep.Id);
                ResetScanStepToRunning();
            }
        }
        NotifyDataChanged();
    }

    private void ResetScanStepToRunning()
    {
        var scanStep = GetScanStepUnsafe();
        if (scanStep == null)
        {
            return;
        }
        scanStep.Status = "Выполняется";
        scanStep.StepStatus = TestStepStatus.Running;
        scanStep.Result = "";
        scanStep.Range = "";
        scanStep.ProgressMessage = "";
        scanStep.StartTime = DateTime.Now;
        scanStep.EndTime = null;
    }

    public void UpdateScanStep(TestStepStatus status, string message, string? limits = null)
    {
        lock (_lock)
        {
            var scanStep = GetScanStepUnsafe();
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
            var scanStep = GetScanStepUnsafe();
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
            scanStep.ProgressMessage = "";
            scanStep.StartTime = DateTime.Now;
            scanStep.EndTime = null;
        }
        NotifyDataChanged();
    }

    public Guid EnsureScanStepExists(string moduleName, string description)
    {
        Guid stepId;

        lock (_lock)
        {
            var existing = GetScanStepUnsafe();
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
            _scanStepId = stepData.Id;
            stepId = stepData.Id;
        }

        NotifyDataChanged();
        return stepId;
    }

    private static void ApplyScanStepUpdate(TestSequenseData step, TestStepStatus status, string message, string? limits)
    {
        step.StepStatus = status;
        step.Result = message;
        step.Range = limits ?? "";
        step.ProgressMessage = "";
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

    /// <summary>
    /// Возвращает scan-строку по внутреннему marker.
    /// Если marker устарел, сбрасывает его в null.
    /// </summary>
    private TestSequenseData? GetScanStepUnsafe()
    {
        if (_scanStepId is not { } scanStepId)
        {
            return null;
        }

        var scanStep = _steps.FirstOrDefault(s => s.Id == scanStepId);
        if (scanStep != null)
        {
            return scanStep;
        }

        _scanStepId = null;
        return null;
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
                EndTime = s.EndTime,
                IsSkipped = s.IsSkipped,
                ProgressMessage = s.ProgressMessage
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
