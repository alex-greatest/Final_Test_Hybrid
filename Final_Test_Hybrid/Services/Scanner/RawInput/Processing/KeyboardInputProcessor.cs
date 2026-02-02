using static Final_Test_Hybrid.Services.Scanner.RawInput.RawInputInterop;

namespace Final_Test_Hybrid.Services.Scanner.RawInput.Processing;

public enum KeyboardAction
{
    Ignore,
    AppendCharacter,
    CompleteBarcode
}

public readonly record struct KeyboardProcessResult(KeyboardAction Action, ushort VKey = 0);

/// <summary>
/// Processes keyboard raw input and determines the action to take.
/// Filters by device and key type. Session state is NOT checked here —
/// символы накапливаются в буфер даже без активной сессии.
/// </summary>
public sealed class KeyboardInputProcessor(ScannerDeviceDetector deviceDetector, KeyboardInputMapper inputMapper)
{
    public KeyboardProcessResult Process(RawInput raw)
    {
        return !IsValidKeyboardInput(raw) ? new KeyboardProcessResult(KeyboardAction.Ignore) : ProcessValidInput(raw);
    }

    private KeyboardProcessResult ProcessValidInput(RawInput raw)
    {
        var vKey = raw.Keyboard.VKey;
        var isKeyUp = IsKeyUpEvent(raw);
        inputMapper.UpdateShiftState(vKey, isKeyUp);
        return DetermineAction(vKey, isKeyUp);
    }

    private static KeyboardProcessResult DetermineAction(ushort vKey, bool isKeyUp)
    {
        return ShouldIgnoreKey(vKey, isKeyUp) ? new KeyboardProcessResult(KeyboardAction.Ignore) : GetKeyAction(vKey);
    }

    private static KeyboardProcessResult GetKeyAction(ushort vKey)
    {
        return KeyboardInputMapper.IsReturnKey(vKey)
            ? new KeyboardProcessResult(KeyboardAction.CompleteBarcode)
            : new KeyboardProcessResult(KeyboardAction.AppendCharacter, vKey);
    }

    private bool IsValidKeyboardInput(RawInput raw)
    {
        return raw.Header.Type == RIM_TYPEKEYBOARD && deviceDetector.IsTargetDevice(raw.Header.Device);
    }

    private static bool IsKeyUpEvent(RawInput raw) => (raw.Keyboard.Flags & 1) != 0;

    private static bool ShouldIgnoreKey(ushort vKey, bool isKeyUp)
    {
        return KeyboardInputMapper.IsShiftKey(vKey) || isKeyUp;
    }
}
