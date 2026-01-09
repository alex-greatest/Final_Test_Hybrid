namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

public enum PreExecutionStatus
{
    Continue,
    TestStarted,
    Cancelled,
    Failed
}

public class PreExecutionResult
{
    public PreExecutionStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public string? UserMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public IPreExecutionErrorDetails? ErrorDetails { get; init; }
    public bool IsRetryable { get; init; }
    public bool CanSkip { get; init; }

    public static PreExecutionResult Continue(string? successMessage = null) =>
        new() { Status = PreExecutionStatus.Continue, SuccessMessage = successMessage };
    public static PreExecutionResult TestStarted() => new() { Status = PreExecutionStatus.TestStarted };
    public static PreExecutionResult Cancelled(string? errorMessage = null) =>
        new() { Status = PreExecutionStatus.Cancelled, ErrorMessage = errorMessage };
    public static PreExecutionResult Fail(string error, string? userMessage = null) =>
        new() { Status = PreExecutionStatus.Failed, ErrorMessage = error, UserMessage = userMessage };
    public static PreExecutionResult Fail(string error, IPreExecutionErrorDetails details, string? userMessage = null) =>
        new() { Status = PreExecutionStatus.Failed, ErrorMessage = error, ErrorDetails = details, UserMessage = userMessage };
    public static PreExecutionResult FailRetryable(string error, bool canSkip = false, string? userMessage = null) =>
        new() { Status = PreExecutionStatus.Failed, ErrorMessage = error, UserMessage = userMessage, IsRetryable = true, CanSkip = canSkip };

    public PreExecutionResult AsNonRetryable() => new()
    {
        Status = Status,
        ErrorMessage = ErrorMessage,
        UserMessage = UserMessage,
        SuccessMessage = SuccessMessage,
        ErrorDetails = ErrorDetails,
        IsRetryable = false,
        CanSkip = CanSkip
    };
}
