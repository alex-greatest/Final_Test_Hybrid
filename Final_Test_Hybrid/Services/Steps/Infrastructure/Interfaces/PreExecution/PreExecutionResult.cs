using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

public enum PreExecutionStatus
{
    Continue,
    TestStarted,
    Cancelled,
    Failed
}

public record PreExecutionResult
{
    public PreExecutionStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public string? UserMessage { get; init; }
    public string? SuccessMessage { get; init; }
    public string? Limits { get; init; }
    public IPreExecutionErrorDetails? ErrorDetails { get; init; }
    public bool IsRetryable { get; init; }
    public bool CanSkip { get; init; }
    public List<ErrorDefinition>? Errors { get; init; }

    public static PreExecutionResult Continue(string? successMessage = null, string? limits = null) =>
        new() { Status = PreExecutionStatus.Continue, SuccessMessage = successMessage, Limits = limits };

    public static PreExecutionResult TestStarted() => new() { Status = PreExecutionStatus.TestStarted };

    public static PreExecutionResult Cancelled(string? errorMessage = null) =>
        new() { Status = PreExecutionStatus.Cancelled, ErrorMessage = errorMessage };

    public static PreExecutionResult Fail(string error, string? userMessage = null, string? limits = null) =>
        new() { Status = PreExecutionStatus.Failed, ErrorMessage = error, UserMessage = userMessage, Limits = limits };

    public static PreExecutionResult Fail(string error, IPreExecutionErrorDetails details, string? userMessage = null, string? limits = null) =>
        new() { Status = PreExecutionStatus.Failed, ErrorMessage = error, ErrorDetails = details, UserMessage = userMessage, Limits = limits };

    public static PreExecutionResult FailRetryable(
        string error,
        bool canSkip = false,
        string? userMessage = null,
        string? limits = null,
        List<ErrorDefinition>? errors = null) =>
        new()
        {
            Status = PreExecutionStatus.Failed,
            ErrorMessage = error,
            UserMessage = userMessage,
            IsRetryable = true,
            CanSkip = canSkip,
            Limits = limits,
            Errors = errors
        };
}
