namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

public class TestStepResult
{
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string Message { get; init; } = "";
    public Dictionary<string, object>? OutputData { get; init; }

    public static TestStepResult Pass(string value = "", string? limits = null)
    {
        return new TestStepResult
        {
            Success = true,
            Message = value,
            OutputData = CreateLimitsData(limits)
        };
    }

    public static TestStepResult Fail(string value, string? limits = null)
    {
        return new TestStepResult
        {
            Success = false,
            Message = value,
            OutputData = CreateLimitsData(limits)
        };
    }

    public static TestStepResult Skip(string value = "")
    {
        return new TestStepResult { Success = true, Skipped = true, Message = value };
    }

    private static Dictionary<string, object>? CreateLimitsData(string? limits)
    {
        return limits == null ? null : new Dictionary<string, object> { ["Limits"] = limits };
    }
}
