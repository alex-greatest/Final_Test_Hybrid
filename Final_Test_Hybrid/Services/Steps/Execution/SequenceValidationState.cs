using Final_Test_Hybrid.Services.Steps.Manage;

namespace Final_Test_Hybrid.Services.Steps.Execution;

public class SequenceValidationState(TestSequenseService testSequenseService)
{
    public event Action<string?>? OnErrorChanged;

    public void SetError(string error)
    {
        OnErrorChanged?.Invoke(error);
        testSequenseService.SetErrorOnCurrent(error);
    }

    public void ClearError()
    {
        OnErrorChanged?.Invoke(null);
    }
}
