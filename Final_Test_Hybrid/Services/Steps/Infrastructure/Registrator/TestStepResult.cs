namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

public class TestStepResult
{
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string Message { get; init; } = "";
    public Dictionary<string, object>? OutputData { get; init; }

    public static TestStepResult Pass(string message = "")
    {
        return new TestStepResult { Success = true, Message = message };
    }

    public static TestStepResult Fail(string message)
    {
        return new TestStepResult { Success = false, Message = message };
    }

    public static TestStepResult Skip(string message)
    {
        return new TestStepResult { Success = true, Skipped = true, Message = message };
    }
}
