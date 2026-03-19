using System.Diagnostics;
using Final_Test_Hybrid.Services.Common;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

/// <summary>
/// Per-context pacing для Modbus-heavy операций тестового шага.
/// </summary>
internal sealed class TestStepModbusPacing(TimeSpan window, PauseTokenSource pauseToken)
{
    private readonly Lock _lock = new();
    private long _lastOperationTimestamp;
    private bool _hasPreviousOperation;

    public async Task WaitBeforeOperationAsync(CancellationToken ct)
    {
        var delay = GetDelay();
        await WaitDelayAsync(delay, ct).ConfigureAwait(false);
        MarkOperationStarted();
    }

    private async Task WaitDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        var remaining = delay;
        while (remaining > TimeSpan.Zero)
        {
            await pauseToken.WaitWhilePausedAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            var chunk = TimeSpan.FromMilliseconds(Math.Min(100, remaining.TotalMilliseconds));
            await Task.Delay(chunk, ct).ConfigureAwait(false);
            remaining -= chunk;
        }
    }

    private TimeSpan GetDelay()
    {
        lock (_lock)
        {
            if (!_hasPreviousOperation)
            {
                return TimeSpan.Zero;
            }

            var elapsed = Stopwatch.GetElapsedTime(_lastOperationTimestamp);
            return elapsed >= window ? TimeSpan.Zero : window - elapsed;
        }
    }

    private void MarkOperationStarted()
    {
        lock (_lock)
        {
            _lastOperationTimestamp = Stopwatch.GetTimestamp();
            _hasPreviousOperation = true;
        }
    }
}
