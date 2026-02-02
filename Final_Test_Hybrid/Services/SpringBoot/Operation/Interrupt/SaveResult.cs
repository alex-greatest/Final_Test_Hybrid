namespace Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

/// <summary>
/// Результат операции сохранения.
/// </summary>
public record SaveResult(bool IsSuccess, string? ErrorMessage = null)
{
    public static SaveResult Success() => new(true);
    public static SaveResult Fail(string error) => new(false, error);
}
