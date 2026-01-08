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
/// Filters by device, key type, and session state.
/// </summary>
public sealed class KeyboardInputProcessor(ScannerDeviceDetector deviceDetector, KeyboardInputMapper inputMapper)
{
    public KeyboardProcessResult Process(RawInput raw, bool hasActiveSession)
    {
        return !IsValidKeyboardInput(raw) ? new KeyboardProcessResult(KeyboardAction.Ignore) : ProcessValidInput(raw, hasActiveSession);
    }

    private KeyboardProcessResult ProcessValidInput(RawInput raw, bool hasActiveSession)
    {
        var vKey = raw.Keyboard.VKey;
        var isKeyUp = IsKeyUpEvent(raw);
        inputMapper.UpdateShiftState(vKey, isKeyUp);
        return DetermineAction(vKey, isKeyUp, hasActiveSession);
    }

    private KeyboardProcessResult DetermineAction(ushort vKey, bool isKeyUp, bool hasActiveSession)
    {
        return ShouldIgnoreKey(vKey, isKeyUp, hasActiveSession) ? new KeyboardProcessResult(KeyboardAction.Ignore) : GetKeyAction(vKey);
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

    private static bool ShouldIgnoreKey(ushort vKey, bool isKeyUp, bool hasActiveSession)
    {
        return KeyboardInputMapper.IsShiftKey(vKey) || isKeyUp || !hasActiveSession;
    }
}
