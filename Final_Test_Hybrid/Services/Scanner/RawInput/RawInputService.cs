using System.Runtime.InteropServices;
using Final_Test_Hybrid.Services.Scanner.RawInput.Processing;
using Microsoft.Extensions.Logging;
using static Final_Test_Hybrid.Services.Scanner.RawInput.RawInputInterop;

namespace Final_Test_Hybrid.Services.Scanner.RawInput;

/// <summary>
/// Coordinates barcode scanning via Windows Raw Input API.
/// Delegates to specialized components for device detection, buffer management, and input mapping.
/// </summary>
public sealed class RawInputService : IDisposable
{
    public event Action<string>? BarcodeScanned;

    private readonly ScannerDeviceDetector _deviceDetector;
    private readonly BarcodeBuffer _buffer;
    private readonly ScanSessionHandler _sessionHandler;
    private readonly KeyboardInputMapper _inputMapper;
    private readonly ScannerConnectionState _connectionState;
    private readonly RawInputDataReader _dataReader;
    private readonly KeyboardInputProcessor _inputProcessor;
    private readonly BarcodeDispatcher _dispatcher;
    private readonly ILogger<RawInputService> _logger;
    private readonly System.Threading.Timer _staleBufferCleanupTimer;
    private volatile bool _isRegistered;
    private volatile bool _disposed;

    /// <summary>
    /// Таймаут для очистки совсем старых данных в буфере (мусор от сбоев сканера).
    /// </summary>
    private static readonly TimeSpan StaleBufferTimeout = TimeSpan.FromSeconds(2);

    public RawInputService(
        ILogger<RawInputService> logger,
        ScannerConnectionState connectionState,
        ScannerDeviceDetector deviceDetector)
    {
        _logger = logger;
        _connectionState = connectionState;
        _deviceDetector = deviceDetector;
        _buffer = new BarcodeBuffer();
        _sessionHandler = new ScanSessionHandler(_buffer);
        _inputMapper = new KeyboardInputMapper();
        _dataReader = new RawInputDataReader();
        _inputProcessor = new KeyboardInputProcessor(_deviceDetector, _inputMapper);
        _dispatcher = new BarcodeDispatcher(_sessionHandler, logger);
        _staleBufferCleanupTimer = new System.Threading.Timer(CleanupStaleBuffer, null, 3000, 3000);
        _connectionState.ConnectionStateChanged += OnScannerConnectionChanged;
        _logger.LogInformation(
            "RawInputService initialized (VID={Vid}, PID={Pid})",
            _deviceDetector.TargetVid,
            _deviceDetector.TargetPid);
    }

    public bool Register(IntPtr hwnd)
    {
        if (_disposed || _isRegistered)
        {
            return _isRegistered;
        }
        return TryRegisterRawInput(hwnd);
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }
        TryUnregisterRawInput();
    }

    /// <summary>
    /// Requests a scan session for the specified handler.
    /// </summary>
    /// <param name="handler">Handler to receive barcode scans.</param>
    /// <param name="takeOver">If true, becomes active handler; if false, only becomes active when no other handler exists.</param>
    public IDisposable RequestScan(Action<string> handler, bool takeOver = true) =>
        _sessionHandler.Acquire(handler, takeOver);

    public void ProcessRawInput(IntPtr lParam)
    {
        if (_disposed)
        {
            return;
        }
        try
        {
            ProcessRawInputSafe(lParam);
        }
        catch (ObjectDisposedException)
        {
            // Service is being disposed, ignore
        }
        catch (Exception ex)
        {
            LogErrorIfNotDisposed(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _staleBufferCleanupTimer.Dispose();
        _buffer.Clear();
        _deviceDetector.ClearCache();
        Unregister();
        _connectionState.ConnectionStateChanged -= OnScannerConnectionChanged;
        BarcodeScanned = null;
        _logger.LogInformation("RawInputService disposed");
    }

    private void ProcessRawInputSafe(IntPtr lParam)
    {
        var raw = _dataReader.Read(lParam);
        if (raw.HasValue && !_disposed)
        {
            HandleKeyboardInput(raw.Value);
        }
    }

    private void HandleKeyboardInput(RawInput raw)
    {
        var result = _inputProcessor.Process(raw);
        ExecuteAction(result);
    }

    private void ExecuteAction(KeyboardProcessResult result)
    {
        switch (result.Action)
        {
            case KeyboardAction.AppendCharacter:
                AppendCharacter(result.VKey);
                break;
            case KeyboardAction.CompleteBarcode:
                CompleteBarcode();
                break;
            case KeyboardAction.Ignore:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }
    }

    private void LogErrorIfNotDisposed(Exception ex)
    {
        if (!_disposed)
        {
            _logger.LogError(ex, "Error processing Raw Input");
        }
    }

    private void OnScannerConnectionChanged(bool connected)
    {
        if (_disposed || connected)
        {
            return;
        }
        _buffer.Clear();
        _deviceDetector.ClearCache();
        _logger.LogInformation("Scanner cache reset (disconnected)");
    }

    /// <summary>
    /// Периодически очищает буфер если данные слишком старые (> 2 сек).
    /// Защита от накопления мусора при сбоях сканера.
    /// </summary>
    private void CleanupStaleBuffer(object? state)
    {
        if (_disposed)
        {
            return;
        }
        if (!_buffer.IsWithinValidWindow(StaleBufferTimeout))
        {
            _buffer.Clear();
        }
    }

    private bool TryRegisterRawInput(IntPtr hwnd)
    {
        try
        {
            return RegisterRawInputDevice(hwnd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Raw Input registration");
            return false;
        }
    }

    private bool RegisterRawInputDevice(IntPtr hwnd)
    {
        var device = CreateKeyboardDevice(hwnd, RIDEV_INPUTSINK);
        var result = RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<RawInputDevice>());
        if (!result)
        {
            _logger.LogError("Raw Input registration failed: {Error}", Marshal.GetLastWin32Error());
            return false;
        }
        _isRegistered = true;
        _logger.LogInformation("Raw Input registered successfully");
        return true;
    }

    private void TryUnregisterRawInput()
    {
        try
        {
            UnregisterRawInputDevice();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during Raw Input unregistration");
        }
    }

    private void UnregisterRawInputDevice()
    {
        var device = CreateKeyboardDevice(IntPtr.Zero, RIDEV_REMOVE);
        RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<RawInputDevice>());
        _isRegistered = false;
        _logger.LogInformation("Raw Input unregistered");
    }

    private static RawInputDevice CreateKeyboardDevice(IntPtr hwnd, uint flags)
    {
        return new RawInputDevice
        {
            UsagePage = HID_USAGE_PAGE_GENERIC,
            Usage = HID_USAGE_GENERIC_KEYBOARD,
            Flags = flags,
            Target = hwnd
        };
    }

    private void AppendCharacter(ushort vKey)
    {
        var ch = _inputMapper.MapToChar(vKey);
        if (ch.HasValue && !_disposed)
        {
            _buffer.Append(ch.Value);
        }
    }

    private void CompleteBarcode()
    {
        if (_disposed)
        {
            return;
        }
        var barcode = _buffer.CompleteAndClear();
        if (string.IsNullOrEmpty(barcode))
        {
            return;
        }
        _logger.LogInformation("Barcode received: {Barcode}", barcode);
        DispatchBarcode(barcode);
    }

    private void DispatchBarcode(string barcode)
    {
        _dispatcher.SetFallbackHandler(BarcodeScanned);
        _dispatcher.Dispatch(barcode);
    }
}
