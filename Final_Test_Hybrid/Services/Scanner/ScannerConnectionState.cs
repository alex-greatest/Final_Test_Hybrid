using System.Management;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Scanner;

public class ScannerConnectionState : IDisposable
{
    public bool IsConnected { get; private set; }
    public event Action<bool>? ConnectionStateChanged;
    private readonly string _vendorId;
    private readonly string _productId;
    private readonly ILogger<ScannerConnectionState> _logger;
    private readonly Lock _lock = new();
    private ManagementEventWatcher? _watcher;
    private volatile bool _disposed;
    private DateTime _lastCheck = DateTime.MinValue;

    public ScannerConnectionState(IConfiguration configuration, ILogger<ScannerConnectionState> logger)
    {
        _logger = logger;
        try
        {
            _vendorId = configuration["Scanner:VendorId"] ?? "0000";
            _productId = configuration["Scanner:ProductId"] ?? "0000";
            _logger.LogInformation("Инициализация ScannerConnectionState (VID={VendorId}, PID={ProductId})", _vendorId, _productId);
            CheckScannerPresent();
            StartWatching();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации ScannerConnectionState");
            _vendorId ??= "0000";
            _productId ??= "0000";
        }
    }

    private void CheckScannerPresent()
    {
        if (_disposed)
        {
            return;
        }
        try
        {
            var query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%VID_{_vendorId}%' AND DeviceID LIKE '%PID_{_productId}%'";
            using var searcher = new ManagementObjectSearcher(query);
            var results = searcher.Get();
            var newState = results.Count > 0;
            UpdateConnectionState(newState);
        }
        catch (ManagementException ex)
        {
            _logger.LogWarning(ex, "WMI ошибка при проверке сканера");
            UpdateConnectionState(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка проверки подключения сканера");
            UpdateConnectionState(false);
        }
    }

    private void StartWatching()
    {
        if (_disposed)
        {
            return;
        }
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2 OR EventType = 3");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnDeviceChanged;
            _watcher.Start();
            _logger.LogInformation("Мониторинг USB-устройств запущен");
        }
        catch (ManagementException ex)
        {
            _logger.LogWarning(ex, "WMI ошибка при запуске мониторинга USB");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка запуска мониторинга USB-устройств");
        }
    }

    private void OnDeviceChanged(object sender, EventArrivedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        try
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                var now = DateTime.UtcNow;
                if ((now - _lastCheck).TotalMilliseconds < 500)
                {
                    return;
                }
                _lastCheck = now;
            }
            CheckScannerPresent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки события USB");
        }
    }

    private void UpdateConnectionState(bool connected)
    {
        if (_disposed)
        {
            return;
        }
        lock (_lock)
        {
            if (_disposed || IsConnected == connected)
            {
                return;
            }
            IsConnected = connected;
        }
        _logger.LogInformation("Состояние сканера: {State}", connected ? "подключен" : "отключен");
        try
        {
            ConnectionStateChanged?.Invoke(connected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике ConnectionStateChanged");
        }
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
        }
        try
        {
            if (_watcher != null)
            {
                _watcher.EventArrived -= OnDeviceChanged;
                _watcher.Stop();
                _watcher.Dispose();
                _watcher = null;
            }
            ConnectionStateChanged = null;
            _logger.LogInformation("ScannerConnectionState disposed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при Dispose ScannerConnectionState");
        }
    }
}
