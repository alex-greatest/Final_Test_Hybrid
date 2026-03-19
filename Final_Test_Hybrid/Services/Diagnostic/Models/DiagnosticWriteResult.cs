namespace Final_Test_Hybrid.Services.Diagnostic.Models;

/// <summary>
/// Результат записи в регистр ЭБУ котла.
/// </summary>
/// <param name="Address">Адрес регистра.</param>
/// <param name="Error">Текст ошибки (null при успехе).</param>
public record DiagnosticWriteResult(
    ushort Address,
    string? Error,
    DiagnosticFailureKind FailureKind)
{
    /// <summary>
    /// Успешность операции записи.
    /// </summary>
    public bool Success => Error == null;

    /// <summary>
    /// Создаёт успешный результат.
    /// </summary>
    public static DiagnosticWriteResult Ok(ushort address) =>
        new(address, null, DiagnosticFailureKind.None);

    /// <summary>
    /// Создаёт результат с ошибкой.
    /// </summary>
    public static DiagnosticWriteResult Fail(
        ushort address,
        string error,
        DiagnosticFailureKind failureKind = DiagnosticFailureKind.Functional) =>
        new(address, error, failureKind);
}
