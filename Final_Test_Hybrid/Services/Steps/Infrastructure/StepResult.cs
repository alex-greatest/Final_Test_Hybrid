namespace Final_Test_Hybrid.Services.Steps.Infrastructure;

public enum StepStatus
{
    Pass,
    Fail,
    Skip,
    Error
}

public record StepResult<T>(
    StepStatus Status,
    T? Value = default,
    string? ErrorMessage = null,
    double? MinLimit = null,
    double? MaxLimit = null)
{
    public bool IsSuccess => Status == StepStatus.Pass;

    public static StepResult<T> Pass(T? value = default, double? min = null, double? max = null)
        => new(StepStatus.Pass, value, null, min, max);

    public static StepResult<T> Fail(string error, T? value = default, double? min = null, double? max = null)
        => new(StepStatus.Fail, value, error, min, max);

    public static StepResult<T> Skip(string? reason = null)
        => new(StepStatus.Skip, default, reason);

    public static StepResult<T> WithError(string error)
        => new(StepStatus.Error, default, error);
}

public record StepResult : StepResult<object>
{
    private StepResult(StepStatus status, string? error) : base(status, null, error) { }

    public static StepResult Pass() => new(StepStatus.Pass, null);
    public static StepResult Fail(string error) => new(StepStatus.Fail, error);
    public new static StepResult Skip(string? reason = null) => new(StepStatus.Skip, reason);
    public new static StepResult WithError(string error) => new(StepStatus.Error, error);
}

public record BarcodeStepResult(
    StepStatus Status,
    IReadOnlyList<string> MissingTags,
    string? ErrorMessage = null)
{
    public bool IsSuccess => Status == StepStatus.Pass;

    public static BarcodeStepResult Pass() => new(StepStatus.Pass, []);
    public static BarcodeStepResult Fail(string error, IReadOnlyList<string>? missingTags = null)
        => new(StepStatus.Fail, missingTags ?? [], error);
    public static BarcodeStepResult WithError(string error) => new(StepStatus.Error, [], error);
}
