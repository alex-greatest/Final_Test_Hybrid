namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Диагностические коды ошибок ЭБУ котла (п.4.5 протокола).
/// </summary>
public static class DiagnosticCodes
{
    // Unsigned коды ошибок
    private const ushort UpperLimitExceeded = 0x7FFF;
    private const ushort LowerLimitExceeded = 0x8000;
    private const ushort GeneralError = 0x8001;
    private const ushort AltUpperLimitExceeded = 0xFFFF;
    private const ushort AltLowerLimitExceeded = 0xFFFE;
    private const ushort AltGeneralError = 0xFFFD;

    // Signed коды ошибок
    private const short SignedUpperLimitExceeded = 0x7FFF;
    private const short SignedLowerLimitExceeded = unchecked((short)0x8000);
    private const short SignedGeneralError = unchecked((short)0x8001);

    /// <summary>
    /// Проверяет, является ли unsigned значение кодом ошибки.
    /// </summary>
    public static bool IsErrorCode(ushort value)
    {
        return value is UpperLimitExceeded or LowerLimitExceeded or GeneralError
            or AltUpperLimitExceeded or AltLowerLimitExceeded or AltGeneralError;
    }

    /// <summary>
    /// Проверяет, является ли signed значение кодом ошибки.
    /// </summary>
    public static bool IsSignedErrorCode(short value)
    {
        return value is SignedUpperLimitExceeded or SignedLowerLimitExceeded or SignedGeneralError;
    }

    /// <summary>
    /// Возвращает текст ошибки для unsigned кода.
    /// </summary>
    public static string GetErrorMessage(ushort value)
    {
        return value switch
        {
            UpperLimitExceeded or AltUpperLimitExceeded => "Выход за верхний предел",
            LowerLimitExceeded or AltLowerLimitExceeded => "Выход за нижний предел",
            GeneralError or AltGeneralError => "Общая ошибка",
            _ => $"Неизвестный код ошибки: 0x{value:X4}"
        };
    }

    /// <summary>
    /// Возвращает текст ошибки для signed кода.
    /// </summary>
    public static string GetSignedErrorMessage(short value)
    {
        return value switch
        {
            SignedUpperLimitExceeded => "Выход за верхний предел",
            SignedLowerLimitExceeded => "Выход за нижний предел",
            SignedGeneralError => "Общая ошибка",
            _ => $"Неизвестный код ошибки: 0x{(ushort)value:X4}"
        };
    }

    #region UInt32 коды ошибок

    // UInt32 коды ошибок (п.4.5 протокола)
    private const uint UInt32UpperLimit = 0xFFFFFFFF;
    private const uint UInt32LowerLimit = 0xFFFFFFFE;
    private const uint UInt32GeneralError = 0xFFFFFFFD;

    /// <summary>
    /// Проверяет, является ли UInt32 значение кодом ошибки.
    /// </summary>
    public static bool IsUInt32ErrorCode(uint value) =>
        value is UInt32UpperLimit or UInt32LowerLimit or UInt32GeneralError;

    /// <summary>
    /// Возвращает описание ошибки для UInt32 кода.
    /// </summary>
    public static string? GetUInt32ErrorDescription(uint value) => value switch
    {
        UInt32UpperLimit => "Выход за верхний предел",
        UInt32LowerLimit => "Выход за нижний предел",
        UInt32GeneralError => "Общая ошибка",
        _ => null
    };

    #endregion

    #region Float NaN коды ошибок

    // NaN коды (IEEE 754 quiet NaN с payload)
    private const uint NanUpperLimit = 0x7FC00001;
    private const uint NanLowerLimit = 0x7FC00002;
    private const uint NanGeneralError = 0x7FC00000;

    /// <summary>
    /// Проверяет, является ли float значение кодом ошибки NaN.
    /// Возвращает тип ошибки через out параметр.
    /// </summary>
    public static bool IsFloatErrorCode(float value, out string? errorType)
    {
        if (!float.IsNaN(value))
        {
            errorType = null;
            return false;
        }

        var bits = BitConverter.SingleToUInt32Bits(value);
        errorType = bits switch
        {
            NanUpperLimit => "Выход за верхний предел",
            NanLowerLimit => "Выход за нижний предел",
            NanGeneralError => "Общая ошибка",
            _ => "Неизвестная ошибка NaN"
        };
        return true;
    }

    #endregion
}
