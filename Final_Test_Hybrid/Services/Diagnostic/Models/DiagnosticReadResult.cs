namespace Final_Test_Hybrid.Services.Diagnostic.Models;

/// <summary>
/// Результат чтения регистра из ЭБУ котла.
/// </summary>
/// <typeparam name="T">Тип прочитанного значения.</typeparam>
/// <param name="Address">Адрес регистра.</param>
/// <param name="Value">Прочитанное значение (null при ошибке).</param>
/// <param name="Error">Текст ошибки (null при успехе).</param>
public record DiagnosticReadResult<T>(
    ushort Address,
    T? Value,
    string? Error,
    DiagnosticFailureKind FailureKind)
{
    /// <summary>
    /// Успешность операции чтения.
    /// </summary>
    public bool Success => Error == null;

    /// <summary>
    /// Создаёт успешный результат.
    /// </summary>
    public static DiagnosticReadResult<T> Ok(ushort address, T value) =>
        new(address, value, null, DiagnosticFailureKind.None);

    /// <summary>
    /// Создаёт результат с ошибкой.
    /// </summary>
    public static DiagnosticReadResult<T> Fail(
        ushort address,
        string error,
        DiagnosticFailureKind failureKind = DiagnosticFailureKind.Functional) =>
        new(address, default, error, failureKind);
}
