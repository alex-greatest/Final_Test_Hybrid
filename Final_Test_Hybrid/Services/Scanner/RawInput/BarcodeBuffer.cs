using System.Text;

namespace Final_Test_Hybrid.Services.Scanner.RawInput;

/// <summary>
/// Thread-safe buffer for accumulating barcode characters.
/// Provides atomic append and complete operations with timestamp tracking.
/// </summary>
public sealed class BarcodeBuffer
{
    private readonly StringBuilder _buffer = new();
    private readonly Lock _lock = new();
    private DateTime? _firstCharacterTime;

    public void Append(char character)
    {
        lock (_lock)
        {
            if (_buffer.Length == 0)
            {
                _firstCharacterTime = DateTime.UtcNow;
            }
            _buffer.Append(character);
        }
    }

    /// <summary>
    /// Проверяет что данные в буфере "свежие" (в пределах указанного окна).
    /// </summary>
    public bool IsWithinValidWindow(TimeSpan window)
    {
        lock (_lock)
        {
            if (_buffer.Length == 0 || !_firstCharacterTime.HasValue)
            {
                return false;
            }
            return DateTime.UtcNow - _firstCharacterTime.Value < window;
        }
    }

    public string CompleteAndClear()
    {
        lock (_lock)
        {
            var result = _buffer.ToString();
            _buffer.Clear();
            _firstCharacterTime = null;
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
            _firstCharacterTime = null;
        }
    }
}
