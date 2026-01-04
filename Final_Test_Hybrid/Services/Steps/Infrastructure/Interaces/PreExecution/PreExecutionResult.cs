namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

public class PreExecutionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? UserMessage { get; init; }
    public bool ShouldStop { get; init; }
    public IPreExecutionErrorDetails? ErrorDetails { get; init; }

    public static PreExecutionResult Ok() => new() { Success = true };
    public static PreExecutionResult Stop() => new() { Success = true, ShouldStop = true };

    public static PreExecutionResult Fail(string error, string? userMessage = null) =>
        new() { Success = false, ErrorMessage = error, UserMessage = userMessage };

    public static PreExecutionResult Fail(string error, IPreExecutionErrorDetails details, string? userMessage = null) =>
        new() { Success = false, ErrorMessage = error, ErrorDetails = details, UserMessage = userMessage };
}
