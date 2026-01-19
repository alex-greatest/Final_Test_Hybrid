namespace Final_Test_Hybrid.Services.Scanner.RawInput;

/// <summary>
/// Maps virtual key codes to characters with shift state handling.
/// Supports digits, letters, and common barcode symbols.
/// </summary>
public sealed class KeyboardInputMapper
{
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_LSHIFT = 0xA0;
    private const ushort VK_RSHIFT = 0xA1;
    private const ushort VK_RETURN = 0x0D;

    private volatile bool _shiftPressed;

    public static bool IsShiftKey(ushort vKey) => vKey is VK_SHIFT or VK_LSHIFT or VK_RSHIFT;
    public static bool IsReturnKey(ushort vKey) => vKey == VK_RETURN;

    public void UpdateShiftState(ushort vKey, bool isKeyUp)
    {
        if (IsShiftKey(vKey))
        {
            _shiftPressed = !isKeyUp;
        }
    }

    public char? MapToChar(ushort vKey)
    {
        if (IsDigit(vKey))
        {
            return (char)vKey;
        }
        if (IsLetter(vKey))
        {
            return _shiftPressed ? (char)vKey : (char)(vKey + 32);
        }
        return MapSpecialKey(vKey);
    }

    private static bool IsDigit(ushort vKey) => vKey is >= 0x30 and <= 0x39;
    private static bool IsLetter(ushort vKey) => vKey is >= 0x41 and <= 0x5A;

    /// <summary>
    /// Маппит OEM клавиши на символы с учётом состояния Shift.
    /// </summary>
    private char? MapSpecialKey(ushort vKey)
    {
        return vKey switch
        {
            0xBD => _shiftPressed ? '_' : '-',
            0xBB => _shiftPressed ? '+' : '=',
            0xBC => _shiftPressed ? '<' : ',',
            0xBE => _shiftPressed ? '>' : '.',
            0xBF => _shiftPressed ? '?' : '/',
            0x20 => ' ',
            _ => null
        };
    }
}