using System.Runtime.InteropServices;

namespace Final_Test_Hybrid.Services.Scanner.RawInput;

[StructLayout(LayoutKind.Sequential)]
public struct RawInputDevice
{
    public ushort UsagePage;
    public ushort Usage;
    public uint Flags;
    public IntPtr Target;
}

[StructLayout(LayoutKind.Sequential)]
public struct RawInputHeader
{
    public uint Type;
    public uint Size;
    public IntPtr Device;
    public IntPtr WParam;
}

[StructLayout(LayoutKind.Sequential)]
public struct RawKeyboard
{
    public ushort MakeCode;
    public ushort Flags;
    public ushort Reserved;
    public ushort VKey;
    public uint Message;
    public uint ExtraInformation;
}

[StructLayout(LayoutKind.Sequential)]
public struct RawInput
{
    public RawInputHeader Header;
    public RawKeyboard Keyboard;
}
