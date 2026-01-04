namespace Final_Test_Hybrid.Services.SpringBoot.Operator;

public class OperatorAuthResult
{
    public bool Success { get; private init; }
    public string? Username { get; private init; }
    public string? ErrorMessage { get; private init; }
    public bool IsKnownError { get; init; }

    public static OperatorAuthResult Ok(string? username = null) => new() { Success = true, Username = username };

    public static OperatorAuthResult Fail(string message, bool isKnownError = true) => new()
    {
        Success = false,
        ErrorMessage = message,
        IsKnownError = isKnownError
    };
}
