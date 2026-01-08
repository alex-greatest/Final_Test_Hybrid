namespace Final_Test_Hybrid.Services.Diagnostic.Models;

/// <summary>
/// Уровни доступа для диагностического протокола ЭБУ котла.
/// </summary>
public enum AccessLevel
{
    /// <summary>
    /// Обычный режим - доступно только чтение.
    /// </summary>
    Normal,

    /// <summary>
    /// Инженерный режим - расширенный доступ (ключ 0xFA87_CD5E).
    /// </summary>
    Engineering,

    /// <summary>
    /// Режим стенда - полный доступ (ключ 0xD7F8_DB56).
    /// </summary>
    Stand
}
