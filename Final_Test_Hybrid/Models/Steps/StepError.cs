using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Models.Steps;

/// <summary>
/// Константы для источников ошибок шагов.
/// </summary>
public static class ErrorSourceDefaults
{
    /// <summary>
    /// Источник ошибки по умолчанию.
    /// </summary>
    public const string Stand = "Стенд";
}

/// <summary>
/// Represents an error that occurred during step execution.
/// </summary>
public record StepError(
    int ColumnIndex,
    string StepName,
    string StepDescription,
    string ErrorMessage,
    string ErrorSourceTitle,
    DateTime OccurredAt,
    Guid UiStepId,
    ITestStep? FailedStep,
    bool CanSkip = true
);

/// <summary>
/// Specifies how a step error should be resolved.
/// </summary>
public enum ErrorResolution
{
    None,
    Retry,
    Skip,
    Timeout,
    ConnectionLost
}
