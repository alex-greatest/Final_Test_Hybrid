namespace Final_Test_Hybrid.Services.Diagnostic.Models;

/// <summary>
/// Результат записи в регистр ЭБУ котла.
/// </summary>
/// <param name="Address">Адрес регистра.</param>
/// <param name="Error">Текст ошибки (null при успехе).</param>
public record DiagnosticWriteResult(
    ushort Address,
    string? Error)
{
    /// <summary>
    /// Успешность операции записи.
    /// </summary>
    public bool Success => Error == null;

    /// <summary>
    /// Создаёт успешный результат.
    /// </summary>
    public static DiagnosticWriteResult Ok(ushort address) =>
        new(address, null);

    /// <summary>
    /// Создаёт результат с ошибкой.
    /// </summary>
    public static DiagnosticWriteResult Fail(ushort address, string error) =>
        new(address, error);
}
