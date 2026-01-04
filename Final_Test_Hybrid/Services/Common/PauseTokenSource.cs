namespace Final_Test_Hybrid.Services.Common;

/// <summary>
/// Provides cooperative pause/resume mechanism for async operations.
/// Callers use WaitWhilePausedAsync to check pause state at safe points.
/// </summary>
public class PauseTokenSource
{
    private readonly Lock _lock = new();
    private TaskCompletionSource? _pauseTcs;

    public bool IsPaused
    {
        get { lock (_lock) { return _pauseTcs != null; } }
    }

    public void Pause()
    {
        lock (_lock)
        {
            _pauseTcs ??= new TaskCompletionSource();
        }
    }

    public void Resume()
    {
        TaskCompletionSource? tcs;
        lock (_lock)
        {
            tcs = _pauseTcs;
            _pauseTcs = null;
        }
        tcs?.TrySetResult();
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
