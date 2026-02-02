using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static Final_Test_Hybrid.Services.Scanner.RawInput.RawInputInterop;

namespace Final_Test_Hybrid.Services.Scanner.RawInput;

/// <summary>
/// Detects and caches the target barcode scanner device by VID/PID.
/// Thread-safe device identification with lazy discovery.
/// </summary>
public sealed class ScannerDeviceDetector(IConfiguration configuration, ILogger<ScannerDeviceDetector> logger)
{
    private readonly ConcurrentDictionary<IntPtr, string> _deviceNameCache = new();
    private readonly Lock _lock = new();
    private IntPtr _cachedDevice = IntPtr.Zero;
    public string TargetVid { get; } = configuration["Scanner:VendorId"] ?? "0000";
    public string TargetPid { get; } = configuration["Scanner:ProductId"] ?? "0000";

    public bool IsTargetDevice(IntPtr hDevice)
    {
        return TryGetCachedDevice(hDevice) || CheckAndCacheDevice(hDevice);
    }

    public void ClearCache()
    {
        lock (_lock)
        {
            _cachedDevice = IntPtr.Zero;
        }
        _deviceNameCache.Clear();
    }

    private bool TryGetCachedDevice(IntPtr hDevice)
    {
        lock (_lock)
        {
            return _cachedDevice != IntPtr.Zero && _cachedDevice == hDevice;
        }
    }

    private bool CheckAndCacheDevice(IntPtr hDevice)
    {
        var deviceName = GetDeviceName(hDevice);
        if (string.IsNullOrEmpty(deviceName) || !IsTargetVidPid(deviceName))
        {
            return false;
        }
        CacheDevice(hDevice, deviceName);
        return true;
    }

    private bool IsTargetVidPid(string deviceName)
    {
        return deviceName.Contains($"VID_{TargetVid}", StringComparison.OrdinalIgnoreCase) &&
               deviceName.Contains($"PID_{TargetPid}", StringComparison.OrdinalIgnoreCase);
    }

    private void CacheDevice(IntPtr hDevice, string deviceName)
    {
        lock (_lock)
        {
            if (_cachedDevice != IntPtr.Zero)
            {
                return;
            }
            _cachedDevice = hDevice;
            logger.LogInformation("Scanner detected: {DeviceName}", deviceName);
        }
    }

    private string? GetDeviceName(IntPtr hDevice)
    {
        if (_deviceNameCache.TryGetValue(hDevice, out var cached))
        {
            return cached;
        }
        var deviceName = ReadDeviceNameFromSystem(hDevice);
        if (!string.IsNullOrEmpty(deviceName))
        {
            _deviceNameCache.TryAdd(hDevice, deviceName);
        }
        return deviceName;
    }

    private static string? ReadDeviceNameFromSystem(IntPtr hDevice)
    {
        uint size = 0;
        var result = GetRawInputDeviceInfoW(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);
        if (result == unchecked((uint)-1) || size == 0)
        {
            return null;
        }
        var buffer = Marshal.AllocHGlobal((int)(size * 2));
        try
        {
            var readResult = GetRawInputDeviceInfoW(hDevice, RIDI_DEVICENAME, buffer, ref size);
            return readResult == unchecked((uint)-1) ? null : Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}