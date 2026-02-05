using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

internal enum ExecutionEventKind
{
    StartRequested,
    ErrorDetected,
    RetryRequested,
    RetryStarted,
    RetryCompleted,
    SkipRequested,
    StopRequested,
    UnhandledException,
    MapStarted,
    MapCompleted,
    StateChanged,
    ErrorOccurred,
    SequenceCompleted
}

internal record ExecutionEvent(
    ExecutionEventKind Kind,
    StepError? StepError = null,
    ColumnExecutor? ColumnExecutor = null,
    ExecutionStopReason? StopReason = null,
    Exception? Exception = null,
    bool StopAsFailure = false);
