using System.Diagnostics;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

/// <summary>
/// Per-context pacing для Modbus-heavy операций тестового шага.
/// </summary>
internal sealed class TestStepModbusPacing(TimeSpan window)
{
    private readonly Lock _lock = new();
    private long _lastOperationTimestamp;
    private bool _hasPreviousOperation;

    public async Task WaitBeforeOperationAsync(CancellationToken ct)
    {
        var delay = GetDelay();
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }

        MarkOperationStarted();
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
