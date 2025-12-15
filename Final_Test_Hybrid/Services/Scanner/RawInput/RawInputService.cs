using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static Final_Test_Hybrid.Services.Scanner.RawInput.RawInputInterop;

namespace Final_Test_Hybrid.Services.Scanner.RawInput;

public sealed class ScanSession : IDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    internal ScanSession(Action onDispose) => _onDispose = onDispose;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _onDispose();
    }
}

public class RawInputService : IDisposable
{
    public event Action<string>? BarcodeScanned;
    private readonly string _targetVid;
    private readonly string _targetPid;
    private readonly ILogger<RawInputService> _logger;
    private readonly ScannerConnectionState _connectionState;
    private readonly ConcurrentDictionary<IntPtr, string> _deviceNameCache = new();
    private readonly StringBuilder _buffer = new();
    private readonly Lock _lock = new();
    private readonly Lock _handlerLock = new();
    private Action<string>? _activeHandler;
    private IntPtr _scannerDevice = IntPtr.Zero;
    private volatile bool _isRegistered;
    private volatile bool _disposed;

    public RawInputService(
        IConfiguration configuration,
        ILogger<RawInputService> logger,
        ScannerConnectionState connectionState)
    {
        _logger = logger;
        _connectionState = connectionState;
        _targetVid = configuration["Scanner:VendorId"] ?? "0000";
        _targetPid = configuration["Scanner:ProductId"] ?? "0000";
        _connectionState.ConnectionStateChanged += OnScannerConnectionChanged;
        _logger.LogInformation("RawInputService инициализирован (VID={Vid}, PID={Pid})", _targetVid, _targetPid);
    }

    private void OnScannerConnectionChanged(bool connected)
    {
        if (_disposed || connected)
        {
            return;
        }
        lock (_lock)
        {
            _scannerDevice = IntPtr.Zero;
            _buffer.Clear();
        }
        _deviceNameCache.Clear();
        _logger.LogInformation("Кэш устройства сканера сброшен (отключение)");
    }

    public bool Register(IntPtr hwnd)
    {
        if (_disposed || _isRegistered)
        {
            return _isRegistered;
        }
        try
        {
            var device = new RawInputDevice
            {
                UsagePage = HID_USAGE_PAGE_GENERIC,
                Usage = HID_USAGE_GENERIC_KEYBOARD,
                Flags = RIDEV_INPUTSINK,
                Target = hwnd
            };
            var result = RegisterRawInputDevices(
                [device],
                1,
                (uint)Marshal.SizeOf<RawInputDevice>());
            if (!result)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.LogError("Ошибка регистрации Raw Input: {Error}", error);
                return false;
            }
            _isRegistered = true;
            _logger.LogInformation("Raw Input зарегистрирован успешно");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при регистрации Raw Input");
            return false;
        }
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }
        try
        {
            var device = new RawInputDevice
            {
                UsagePage = HID_USAGE_PAGE_GENERIC,
                Usage = HID_USAGE_GENERIC_KEYBOARD,
                Flags = RIDEV_REMOVE,
                Target = IntPtr.Zero
            };
            RegisterRawInputDevices(
                [device],
                1,
                (uint)Marshal.SizeOf<RawInputDevice>());
            _isRegistered = false;
            _logger.LogInformation("Raw Input отменён");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка отмены регистрации Raw Input");
        }
    }

    public ScanSession RequestScan(Action<string> handler)
    {
        lock (_handlerLock)
        {
            _activeHandler = handler;
        }
        return new ScanSession(() =>
        {
            lock (_handlerLock)
            {
                if (_activeHandler == handler)
                {
                    _activeHandler = null;
                }
            }
        });
    }

    public void ProcessRawInput(IntPtr lParam)
    {
        if (_disposed)
        {
            return;
        }
        try
        {
            var raw = ReadRawInput(lParam);
            if (raw.HasValue && !_disposed)
            {
                ProcessKeyboardInput(raw.Value);
            }
        }
        catch (ObjectDisposedException)
        {
            // Service is being disposed, ignore
        }
        catch (Exception ex)
        {
            if (!_disposed)
            {
                _logger.LogError(ex, "Ошибка обработки Raw Input");
            }
        }
    }

    private RawInput? ReadRawInput(IntPtr lParam)
    {
        uint size = 0;
        GetRawInputData(
            lParam,
            RID_INPUT,
            IntPtr.Zero,
            ref size,
            (uint)Marshal.SizeOf<RawInputHeader>());
        if (size == 0)
        {
            return null;
        }
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var result = GetRawInputData(
                lParam,
                RID_INPUT,
                buffer,
                ref size,
                (uint)Marshal.SizeOf<RawInputHeader>());
            if (result == unchecked((uint)-1))
            {
                return null;
            }
            return Marshal.PtrToStructure<RawInput>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void ProcessKeyboardInput(RawInput raw)
    {
        if (_disposed || raw.Header.Type != RIM_TYPEKEYBOARD)
        {
            return;
        }
        if (!IsTargetDevice(raw.Header.Device))
        {
            return;
        }
        if (raw.Keyboard.Flags != 0)
        {
            return;
        }
        var vKey = raw.Keyboard.VKey;
        if (vKey == 0x0D)
        {
            CompleteBarcode();
            return;
        }
        var ch = VKeyToChar(vKey);
        if (ch.HasValue)
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    _buffer.Append(ch.Value);
                }
            }
        }
    }

    private bool IsTargetDevice(IntPtr hDevice)
    {
        IntPtr cached;
        lock (_lock)
        {
            cached = _scannerDevice;
        }
        if (cached != IntPtr.Zero)
        {
            return cached == hDevice;
        }
        var deviceName = GetDeviceName(hDevice);
        if (string.IsNullOrEmpty(deviceName))
        {
            return false;
        }
        var isTarget = deviceName.Contains($"VID_{_targetVid}", StringComparison.OrdinalIgnoreCase) &&
                       deviceName.Contains($"PID_{_targetPid}", StringComparison.OrdinalIgnoreCase);
        if (isTarget)
        {
            lock (_lock)
            {
                if (_scannerDevice == IntPtr.Zero)
                {
                    _scannerDevice = hDevice;
                    _logger.LogInformation("Сканер обнаружен: {DeviceName}", deviceName);
                }
            }
        }
        return isTarget;
    }

    private string? GetDeviceName(IntPtr hDevice)
    {
        if (_deviceNameCache.TryGetValue(hDevice, out var cached))
        {
            return cached;
        }
        try
        {
            var deviceName = ReadDeviceName(hDevice);
            if (!string.IsNullOrEmpty(deviceName))
            {
                _deviceNameCache.TryAdd(hDevice, deviceName);
            }
            return deviceName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка получения имени устройства");
            return null;
        }
    }

    private static string? ReadDeviceName(IntPtr hDevice)
    {
        uint size = 0;
        GetRawInputDeviceInfoW(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);
        if (size == 0)
        {
            return null;
        }
        var buffer = Marshal.AllocHGlobal((int)(size * 2));
        try
        {
            var result = GetRawInputDeviceInfoW(hDevice, RIDI_DEVICENAME, buffer, ref size);
            if (result == unchecked((uint)-1))
            {
                return null;
            }
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void CompleteBarcode()
    {
        if (_disposed)
        {
            return;
        }
        string barcode;
        lock (_lock)
        {
            barcode = _buffer.ToString();
            _buffer.Clear();
        }
        if (string.IsNullOrEmpty(barcode))
        {
            return;
        }
        _logger.LogInformation("Штрих-код получен: {Barcode}", barcode);
        Action<string>? handler;
        lock (_handlerLock)
        {
            handler = _activeHandler;
        }
        try
        {
            if (handler != null)
            {
                handler(barcode);
                return;
            }
            BarcodeScanned?.Invoke(barcode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике BarcodeScanned");
        }
    }

    private static char? VKeyToChar(ushort vKey)
    {
        if (vKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            return (char)vKey;
        }
        return vKey switch
        {
            0xBD => '-',
            0xBB => '+',
            0xBC => ',',
            0xBE => '.',
            0xBF => '/',
            0x20 => ' ',
            _ => null
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _buffer.Clear();
            _scannerDevice = IntPtr.Zero;
        }
        Unregister();
        _connectionState.ConnectionStateChanged -= OnScannerConnectionChanged;
        _deviceNameCache.Clear();
        _logger.LogInformation("RawInputService disposed");
        GC.SuppressFinalize(this);
    }
}
