using System.Text;

namespace Final_Test_Hybrid.Services.Scanner.RawInput;

/// <summary>
/// Thread-safe buffer for accumulating barcode characters.
/// Provides atomic append and complete operations.
/// </summary>
public sealed class BarcodeBuffer
{
    private readonly StringBuilder _buffer = new();
    private readonly Lock _lock = new();

    public void Append(char character)
    {
        lock (_lock)
        {
            _buffer.Append(character);
        }
    }

    public string CompleteAndClear()
    {
        lock (_lock)
        {
            var result = _buffer.ToString();
            _buffer.Clear();
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
        }
    }
}
