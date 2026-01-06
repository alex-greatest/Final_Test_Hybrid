namespace Final_Test_Hybrid.Services.Main;

using Steps.Infrastructure.Execution;

public class SettingsInteractionState : IDisposable
{
    private readonly TestSequenseService _testSequenseService;

    public bool CanInteract => !_testSequenseService.Data.Any()
                            || _testSequenseService.IsOnActiveScanStep;

    public event Action? OnChange;

    public SettingsInteractionState(TestSequenseService testSequenseService)
    {
        _testSequenseService = testSequenseService;
        _testSequenseService.OnDataChanged += NotifyChange;
    }

    private void NotifyChange() => OnChange?.Invoke();

    public void Dispose() => _testSequenseService.OnDataChanged -= NotifyChange;
}
