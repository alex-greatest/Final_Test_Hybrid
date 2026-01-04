namespace Final_Test_Hybrid.Services.Common;

/// <summary>
/// Tracks which execution phases are currently active (PreExecution, TestExecution).
/// Thread-safe implementation that notifies subscribers when state changes.
/// </summary>
public class ExecutionActivityTracker
{
    private readonly Lock _lock = new();
    private bool _isPreExecutionActive;
    private bool _isTestExecutionActive;

    public event Action? OnChanged;

    public bool IsPreExecutionActive
    {
        get { lock (_lock) { return _isPreExecutionActive; } }
    }

    public bool IsTestExecutionActive
    {
        get { lock (_lock) { return _isTestExecutionActive; } }
    }

    public bool IsAnyActive
    {
        get { lock (_lock) { return _isPreExecutionActive || _isTestExecutionActive; } }
    }

    public void SetPreExecutionActive(bool active)
    {
        lock (_lock)
        {
            if (_isPreExecutionActive == active) { return; }
            _isPreExecutionActive = active;
        }
        OnChanged?.Invoke();
    }

    public void SetTestExecutionActive(bool active)
    {
        lock (_lock)
        {
            if (_isTestExecutionActive == active) { return; }
            _isTestExecutionActive = active;
        }
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        bool wasActive;
        lock (_lock)
        {
            wasActive = _isPreExecutionActive || _isTestExecutionActive;
            _isPreExecutionActive = false;
            _isTestExecutionActive = false;
        }
        if (wasActive)
        {
            OnChanged?.Invoke();
        }
    }
}
