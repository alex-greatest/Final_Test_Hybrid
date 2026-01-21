namespace Final_Test_Hybrid.Services.Common;

/// <summary>
/// Provides cooperative pause/resume mechanism for async operations.
/// Callers use WaitWhilePausedAsync to check pause state at safe points.
/// Exposes OnPaused/OnResumed events for pause-aware waiting.
/// </summary>
public class PauseTokenSource
{
    private readonly Lock _lock = new();
    private TaskCompletionSource? _pauseTcs;

    /// <summary>
    /// Fires when Pause() is called (outside lock).
    /// </summary>
    public event Action? OnPaused;

    /// <summary>
    /// Fires when Resume() is called (outside lock).
    /// </summary>
    public event Action? OnResumed;

    public bool IsPaused
    {
        get { lock (_lock) { return _pauseTcs != null; } }
    }

    public void Pause()
    {
        Action? onPaused;
        lock (_lock)
        {
            if (_pauseTcs != null) { return; }
            _pauseTcs = new TaskCompletionSource();
            onPaused = OnPaused;
        }
        SafeInvoke(onPaused);
    }

    public void Resume()
    {
        TaskCompletionSource? tcs;
        Action? onResumed;
        lock (_lock)
        {
            tcs = _pauseTcs;
            if (tcs == null) { return; }
            _pauseTcs = null;
            onResumed = OnResumed;
        }
        tcs.TrySetResult();
        SafeInvoke(onResumed);
    }

    private static void SafeInvoke(Action? action)
    {
        if (action == null) { return; }
        foreach (var handler in action.GetInvocationList().Cast<Action>())
        {
            try
            {
                handler();
            }
            catch
            {
                // Ignore exceptions in event handlers to prevent breaking pause/resume flow
            }
        }
    }

    public async Task WaitWhilePausedAsync(CancellationToken ct = default)
    {
        TaskCompletionSource? tcs;
        lock (_lock)
        {
            tcs = _pauseTcs;
        }
        if (tcs == null) { return; }
        await tcs.Task.WaitAsync(ct);
    }
}
