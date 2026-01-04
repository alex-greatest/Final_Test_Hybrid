namespace Final_Test_Hybrid.Services.SpringBoot.Operation;

public class OperationStartResult
{
    public bool IsSuccess { get; init; }
    public bool RequiresRework { get; init; }
    public string? ErrorMessage { get; init; }
    public OperationStartResponse? Data { get; init; }

    public static OperationStartResult Success(OperationStartResponse data) =>
        new() { IsSuccess = true, Data = data };

    public static OperationStartResult NeedRework(string error) =>
        new() { IsSuccess = false, RequiresRework = true, ErrorMessage = error };

    public static OperationStartResult Fail(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
