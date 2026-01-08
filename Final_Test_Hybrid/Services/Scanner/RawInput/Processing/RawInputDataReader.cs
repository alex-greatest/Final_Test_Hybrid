using System.Runtime.InteropServices;
using static Final_Test_Hybrid.Services.Scanner.RawInput.RawInputInterop;

namespace Final_Test_Hybrid.Services.Scanner.RawInput.Processing;

/// <summary>
/// Reads raw input data from Windows message lParam.
/// Encapsulates P/Invoke memory operations.
/// </summary>
public sealed class RawInputDataReader
{
    public RawInput? Read(IntPtr lParam)
    {
        var size = GetBufferSize(lParam);
        return size == 0 ? null : ReadFromBuffer(lParam, size);
    }

    private static uint GetBufferSize(IntPtr lParam)
    {
        uint size = 0;
        var result = GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
        return result == unchecked((uint)-1) ? 0 : size;
    }

    private static RawInput? ReadFromBuffer(IntPtr lParam, uint size)
    {
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            return ReadStructure(lParam, buffer, size);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static RawInput? ReadStructure(IntPtr lParam, IntPtr buffer, uint size)
    {
        var result = GetRawInputData(lParam, RID_INPUT, buffer, ref size, (uint)Marshal.SizeOf<RawInputHeader>());
        return result == unchecked((uint)-1) ? null : Marshal.PtrToStructure<RawInput>(buffer);
    }
}
