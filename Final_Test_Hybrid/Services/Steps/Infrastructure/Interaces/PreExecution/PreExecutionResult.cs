namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

public class PreExecutionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public bool ShouldStop { get; init; }
    public IPreExecutionErrorDetails? ErrorDetails { get; init; }

    public static PreExecutionResult Ok() => new() { Success = true };
    public static PreExecutionResult Stop() => new() { Success = true, ShouldStop = true };
    public static PreExecutionResult Fail(string error) => new() { Success = false, ErrorMessage = error };

    public static PreExecutionResult Fail(string error, IPreExecutionErrorDetails details) =>
        new() { Success = false, ErrorMessage = error, ErrorDetails = details };
}
