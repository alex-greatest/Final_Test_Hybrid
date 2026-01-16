using Final_Test_Hybrid.Models.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

/// <summary>
/// Interface for error coordination during test execution.
/// </summary>
public interface IErrorCoordinator
{
    event Action? OnReset;
    event Action? OnRecovered;
    event Action? OnInterruptChanged;

    InterruptReason? CurrentInterrupt { get; }

    Task HandleInterruptAsync(InterruptReason reason, CancellationToken ct = default);
    void Reset();
    void ForceStop();
    Task<ErrorResolution> WaitForResolutionAsync(CancellationToken ct);
    Task<ErrorResolution> WaitForResolutionAsync(string? blockEndTag, string? blockErrorTag, CancellationToken ct, TimeSpan? timeout = null);
    Task<ErrorResolution> WaitForResolutionAsync(string? blockEndTag, string? blockErrorTag, bool enableSkip, CancellationToken ct, TimeSpan? timeout = null);
    Task SendAskRepeatAsync(CancellationToken ct);
    Task SendAskRepeatAsync(string? blockErrorTag, CancellationToken ct);
    Task WaitForRetrySignalResetAsync(CancellationToken ct);
}
