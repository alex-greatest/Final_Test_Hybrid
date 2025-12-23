using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Steps.Manage;

namespace Final_Test_Hybrid.Services.Steps.Execution;

public class SequenceValidationState(
    INotificationService notificationService,
    TestSequenseService testSequenseService)
{
    public string? LastError { get; private set; }
    public event Action? OnErrorChanged;

    public void SetError(string error)
    {
        LastError = error;
        OnErrorChanged?.Invoke();
        notificationService.ShowError("Ошибка валидации", error, id: "validation-error");
        testSequenseService.SetErrorOnCurrent(error);
    }

    public void ClearError()
    {
        LastError = null;
        OnErrorChanged?.Invoke();
    }
}
