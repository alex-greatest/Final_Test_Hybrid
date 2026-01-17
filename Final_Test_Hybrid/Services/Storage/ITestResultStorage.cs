namespace Final_Test_Hybrid.Services.Storage;

public interface ITestResultStorage
{
    Task<SaveResult> SaveAsync(int testResult, CancellationToken ct);
}

public class SaveResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }

    public static SaveResult Success() => new() { IsSuccess = true };
    public static SaveResult Fail(string error) => new() { IsSuccess = false, ErrorMessage = error };
}
