namespace Final_Test_Hybrid.Models.Steps;

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

public record BarcodeStepResult
{
    public StepStatus Status { get; init; }
    public IReadOnlyList<string> MissingPlcTags { get; init; } = [];
    public IReadOnlyList<string> MissingRequiredTags { get; init; } = [];
    public List<RawTestMap>? RawMaps { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsCancelled { get; init; }
    public bool IsSuccess => Status == StepStatus.Pass && !IsCancelled;

    public static BarcodeStepResult Pass(List<RawTestMap> rawMaps) =>
        new() { Status = StepStatus.Pass, RawMaps = rawMaps };

    public static BarcodeStepResult FailPlcTags(string error, IReadOnlyList<string> missingTags) =>
        new() { Status = StepStatus.Fail, MissingPlcTags = missingTags, ErrorMessage = error };

    public static BarcodeStepResult FailRequiredTags(string error, IReadOnlyList<string> missingTags) =>
        new() { Status = StepStatus.Fail, MissingRequiredTags = missingTags, ErrorMessage = error };

    public static BarcodeStepResult Fail(string error) =>
        new() { Status = StepStatus.Fail, ErrorMessage = error };

    public static BarcodeStepResult WithError(string error) =>
        new() { Status = StepStatus.Error, ErrorMessage = error };

    public static BarcodeStepResult Cancelled() =>
        new() { Status = StepStatus.Pass, IsCancelled = true };
}

public record UnknownStepInfo(string StepName, int Row, int Column);
